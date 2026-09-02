use std::{collections::HashMap, fs, sync::Arc};

use base64::{engine::general_purpose::URL_SAFE_NO_PAD, Engine};
use chrono::{Duration, Utc};
use native_tls::TlsConnector;
use openssl::{hash::MessageDigest, pkcs12::Pkcs12, sign::Signer};
use parking_lot::Mutex;
use serde::Serialize;
use tauri::{AppHandle, Emitter, Manager, State};
use tokio::{
    io::{AsyncReadExt, AsyncWriteExt},
    net::TcpStream,
    sync::mpsc,
};
use tokio_native_tls::TlsConnector as TokioTlsConnector;

const MAX_PACKET_SIZE: usize = 16 * 1024 * 1024;

enum WriterCommand {
    Send(Vec<u8>),
    Close,
}

#[derive(Default)]
struct SocketState {
    sockets: Mutex<HashMap<String, mpsc::Sender<WriterCommand>>>,
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct RawPacketEvent {
    client_id: String,
    payload: String,
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct SocketStatusEvent {
    client_id: String,
    status: String,
    message: String,
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct BeatSaverMap {
    hash: String,
    name: String,
    duration_seconds: u64,
    cover_url: String,
    download_url: String,
    cached_path: String,
}

fn emit_status(app: &AppHandle, client_id: &str, status: &str, message: impl Into<String>) {
    let _ = app.emit("mock-socket-status", SocketStatusEvent {
        client_id: client_id.to_owned(),
        status: status.to_owned(),
        message: message.into(),
    });
}

#[tauri::command]
async fn connect_socket(
    app: AppHandle,
    state: State<'_, Arc<SocketState>>,
    client_id: String,
    address: String,
    port: u16,
    accept_invalid_certificate: bool,
) -> Result<(), String> {
    let existing = { state.sockets.lock().remove(&client_id) };
    if let Some(existing) = existing {
        let _ = existing.send(WriterCommand::Close).await;
    }

    emit_status(&app, &client_id, "connecting", format!("Connecting to {address}:{port}"));
    let tcp = TcpStream::connect((address.as_str(), port))
        .await
        .map_err(|e| format!("TCP connection failed: {e}"))?;
    let mut builder = TlsConnector::builder();
    builder.danger_accept_invalid_certs(accept_invalid_certificate);
    builder.danger_accept_invalid_hostnames(accept_invalid_certificate);
    let connector = builder.build().map_err(|e| format!("TLS setup failed: {e}"))?;
    let stream = TokioTlsConnector::from(connector)
        .connect(&address, tcp)
        .await
        .map_err(|e| format!("TLS handshake failed: {e}"))?;

    let (mut reader, mut writer) = tokio::io::split(stream);
    let (tx, mut rx) = mpsc::channel::<WriterCommand>(128);
    state.sockets.lock().insert(client_id.clone(), tx);

    let writer_app = app.clone();
    let writer_id = client_id.clone();
    tauri::async_runtime::spawn(async move {
        while let Some(command) = rx.recv().await {
            match command {
                WriterCommand::Send(payload) => {
                    let mut frame = Vec::with_capacity(payload.len() + 8);
                    frame.extend_from_slice(b"moon");
                    frame.extend_from_slice(&(payload.len() as i32).to_le_bytes());
                    frame.extend_from_slice(&payload);
                    if let Err(error) = writer.write_all(&frame).await {
                        emit_status(&writer_app, &writer_id, "error", format!("Socket write failed: {error}"));
                        break;
                    }
                }
                WriterCommand::Close => break,
            }
        }
        let _ = writer.shutdown().await;
    });

    let reader_app = app.clone();
    let reader_id = client_id.clone();
    let sockets = Arc::clone(state.inner());
    tauri::async_runtime::spawn(async move {
        emit_status(&reader_app, &reader_id, "connected", "Raw TLS socket connected");
        loop {
            let mut header = [0u8; 8];
            if let Err(error) = reader.read_exact(&mut header).await {
                emit_status(&reader_app, &reader_id, "disconnected", format!("Socket closed: {error}"));
                break;
            }
            if &header[..4] != b"moon" {
                emit_status(&reader_app, &reader_id, "error", "Invalid packet magic");
                break;
            }
            let size = i32::from_le_bytes(header[4..8].try_into().unwrap());
            if size < 0 || size as usize > MAX_PACKET_SIZE {
                emit_status(&reader_app, &reader_id, "error", format!("Invalid packet size: {size}"));
                break;
            }
            let mut payload = vec![0u8; size as usize];
            if let Err(error) = reader.read_exact(&mut payload).await {
                emit_status(&reader_app, &reader_id, "error", format!("Packet read failed: {error}"));
                break;
            }
            let _ = reader_app.emit("mock-raw-packet", RawPacketEvent {
                client_id: reader_id.clone(),
                payload: base64::engine::general_purpose::STANDARD.encode(payload),
            });
        }
        sockets.sockets.lock().remove(&reader_id);
    });

    Ok(())
}

#[tauri::command]
async fn send_packet(
    state: State<'_, Arc<SocketState>>,
    client_id: String,
    payload: String,
) -> Result<(), String> {
    let bytes = base64::engine::general_purpose::STANDARD
        .decode(payload)
        .map_err(|e| format!("Invalid packet payload: {e}"))?;
    let sender = state.sockets.lock().get(&client_id).cloned()
        .ok_or_else(|| "Client socket is not connected".to_owned())?;
    sender.send(WriterCommand::Send(bytes)).await.map_err(|_| "Socket writer stopped".to_owned())
}

#[tauri::command]
async fn disconnect_socket(
    state: State<'_, Arc<SocketState>>,
    client_id: String,
) -> Result<(), String> {
    let sender = { state.sockets.lock().remove(&client_id) };
    if let Some(sender) = sender {
        sender.send(WriterCommand::Close).await.map_err(|_| "Socket writer stopped".to_owned())?;
    }
    Ok(())
}

#[tauri::command]
fn sign_mock_token(
    certificate_path: String,
    certificate_password: String,
    platform_id: String,
    username: String,
) -> Result<String, String> {
    let der = fs::read(&certificate_path).map_err(|e| format!("Could not read mock certificate: {e}"))?;
    let parsed = Pkcs12::from_der(&der)
        .and_then(|p12| p12.parse2(&certificate_password))
        .map_err(|e| format!("Could not unlock mock certificate: {e}"))?;
    let key = parsed.pkey.ok_or_else(|| "Mock certificate has no private key".to_owned())?;

    let now = Utc::now();
    let header = serde_json::json!({ "alg": "RS256", "typ": "JWT" });
    let claims = serde_json::json!({
        "sub": uuid::Uuid::new_v4().to_string(),
        "iat": now.timestamp(),
        "exp": (now + Duration::hours(12)).timestamp(),
        "iss": "ta_plugin_mock",
        "aud": "ta_users",
        "ta:platform_id": platform_id,
        "ta:platform_username": username
    });
    let encoded_header = URL_SAFE_NO_PAD.encode(serde_json::to_vec(&header).map_err(|e| e.to_string())?);
    let encoded_claims = URL_SAFE_NO_PAD.encode(serde_json::to_vec(&claims).map_err(|e| e.to_string())?);
    let signing_input = format!("{encoded_header}.{encoded_claims}");
    let mut signer = Signer::new(MessageDigest::sha256(), &key).map_err(|e| e.to_string())?;
    signer.update(signing_input.as_bytes()).map_err(|e| e.to_string())?;
    let signature = signer.sign_to_vec().map_err(|e| e.to_string())?;
    Ok(format!("{signing_input}.{}", URL_SAFE_NO_PAD.encode(signature)))
}

#[tauri::command]
fn save_logs(path: String, contents: String) -> Result<(), String> {
    fs::write(path, contents).map_err(|e| format!("Could not save logs: {e}"))
}

#[tauri::command]
async fn fetch_beatsaver_map(app: AppHandle, level_id: String) -> Result<BeatSaverMap, String> {
    let hash = level_id
        .split(|c: char| !c.is_ascii_hexdigit())
        .find(|part| part.len() == 40)
        .unwrap_or(level_id.as_str())
        .to_uppercase();
    let client = reqwest::Client::builder()
        .user_agent("TournamentAssistant-MockClient/0.1")
        .build()
        .map_err(|error| format!("BeatSaver client error: {error}"))?;
    let info: serde_json::Value = client
        .get(format!("https://api.beatsaver.com/maps/hash/{hash}"))
        .send().await.map_err(|error| format!("BeatSaver lookup failed: {error}"))?
        .error_for_status().map_err(|error| format!("BeatSaver rejected the map lookup: {error}"))?
        .json().await.map_err(|error| format!("Invalid BeatSaver response: {error}"))?;
    let version = info["versions"].as_array()
        .and_then(|versions| versions.iter().find(|version| version["hash"].as_str().map(|h| h.eq_ignore_ascii_case(&hash)).unwrap_or(false)))
        .or_else(|| info["versions"].as_array().and_then(|versions| versions.first()))
        .ok_or_else(|| "BeatSaver map has no downloadable version".to_owned())?;
    let download_url = version["downloadURL"].as_str().unwrap_or_default().to_owned();
    let bytes = client.get(&download_url).send().await
        .map_err(|error| format!("BeatSaver download failed: {error}"))?
        .error_for_status().map_err(|error| format!("BeatSaver download was rejected: {error}"))?
        .bytes().await.map_err(|error| format!("BeatSaver download failed: {error}"))?;
    let cache_dir = app.path().app_cache_dir().map_err(|error| error.to_string())?.join("mock-maps");
    fs::create_dir_all(&cache_dir).map_err(|error| format!("Could not create map cache: {error}"))?;
    let cached_path = cache_dir.join(format!("{hash}.zip"));
    fs::write(&cached_path, bytes).map_err(|error| format!("Could not cache map: {error}"))?;

    Ok(BeatSaverMap {
        hash,
        name: info["metadata"]["songName"].as_str().or_else(|| info["name"].as_str()).unwrap_or("Unknown map").to_owned(),
        duration_seconds: info["metadata"]["duration"].as_u64().unwrap_or(180).max(1),
        cover_url: version["coverURL"].as_str().unwrap_or_default().to_owned(),
        download_url,
        cached_path: cached_path.to_string_lossy().into_owned(),
    })
}

pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_dialog::init())
        .manage(Arc::new(SocketState::default()))
        .invoke_handler(tauri::generate_handler![
            connect_socket,
            send_packet,
            disconnect_socket,
            sign_mock_token,
            save_logs,
            fetch_beatsaver_map
        ])
        .run(tauri::generate_context!())
        .expect("error while running TournamentAssistant Mock Client");
}
