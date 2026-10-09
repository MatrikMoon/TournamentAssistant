use std::{collections::HashMap, fs, sync::Arc};

use base64::{engine::general_purpose::URL_SAFE_NO_PAD, Engine};
use chrono::{Duration, Utc};
use native_tls::TlsConnector;
use openssl::{hash::MessageDigest, pkcs12::Pkcs12, provider::Provider, sign::Signer};
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
const MOCK_CERTIFICATE: &[u8] = include_bytes!("../../static/mock.pfx");
const MOCK_CERTIFICATE_PASSWORD: &str = "password";

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
    key: String,
    name: String,
    description: String,
    song_sub_name: String,
    song_author_name: String,
    level_author_name: String,
    bpm: f64,
    duration_seconds: u64,
    upvotes: u64,
    downvotes: u64,
    rating: f64,
    created_at: String,
    version_created_at: String,
    cover_url: String,
    download_url: String,
    cached_path: String,
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct BsorVector3 {
    x: f32,
    y: f32,
    z: f32,
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct BsorQuaternion {
    x: f32,
    y: f32,
    z: f32,
    w: f32,
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct BsorPose {
    position: BsorVector3,
    rotation: BsorQuaternion,
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct BsorFrame {
    time: f32,
    fps: i32,
    head: BsorPose,
    left: BsorPose,
    right: BsorPose,
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct BsorCutInfo {
    direction_ok: bool,
    saber_speed: f32,
    saber_direction: BsorVector3,
    saber_type: i32,
    time_deviation: f32,
    cut_direction_deviation: f32,
    cut_point: BsorVector3,
    cut_normal: BsorVector3,
    cut_distance_to_center: f32,
    cut_angle: f32,
    before_cut_rating: f32,
    after_cut_rating: f32,
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct BsorNote {
    note_id: i32,
    event_time: f32,
    spawn_time: f32,
    event_type: i32,
    cut: Option<BsorCutInfo>,
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct BsorWall {
    energy: f32,
    time: f32,
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct BsorHeight {
    height: f32,
    time: f32,
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct BsorPause {
    duration: i64,
    time: f32,
}

#[derive(Clone, Default, Serialize)]
#[serde(rename_all = "camelCase")]
struct BsorInfo {
    version: String,
    game_version: String,
    timestamp: String,
    player_id: String,
    player_name: String,
    platform: String,
    tracking_system: String,
    hmd: String,
    controller: String,
    hash: String,
    song_name: String,
    mapper: String,
    difficulty: String,
    score: i32,
    mode: String,
    environment: String,
    modifiers: String,
    jump_distance: f32,
    left_handed: bool,
    height: f32,
    start_time: f32,
    fail_time: f32,
    speed: f32,
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct BsorReplay {
    source_url: String,
    source_rank: usize,
    cached_path: String,
    info: BsorInfo,
    frames: Vec<BsorFrame>,
    notes: Vec<BsorNote>,
    walls: Vec<BsorWall>,
    heights: Vec<BsorHeight>,
    pauses: Vec<BsorPause>,
}

struct BsorReader<'a> {
    data: &'a [u8],
    position: usize,
}

impl<'a> BsorReader<'a> {
    fn new(data: &'a [u8]) -> Self {
        Self { data, position: 0 }
    }
    fn take<const N: usize>(&mut self) -> Result<[u8; N], String> {
        if self.position + N > self.data.len() {
            return Err("Unexpected end of BSOR data".to_owned());
        }
        let result = self.data[self.position..self.position + N]
            .try_into()
            .unwrap();
        self.position += N;
        Ok(result)
    }
    fn u8(&mut self) -> Result<u8, String> {
        Ok(self.take::<1>()?[0])
    }
    fn bool(&mut self) -> Result<bool, String> {
        Ok(self.u8()? != 0)
    }
    fn i32(&mut self) -> Result<i32, String> {
        Ok(i32::from_le_bytes(self.take()?))
    }
    fn i64(&mut self) -> Result<i64, String> {
        Ok(i64::from_le_bytes(self.take()?))
    }
    fn f32(&mut self) -> Result<f32, String> {
        Ok(f32::from_le_bytes(self.take()?))
    }
    fn string(&mut self) -> Result<String, String> {
        let length = self.i32()?;
        if !(0..=1_048_576).contains(&length) || self.position + length as usize > self.data.len() {
            return Err(format!("Invalid BSOR string length: {length}"));
        }
        let value =
            String::from_utf8_lossy(&self.data[self.position..self.position + length as usize])
                .into_owned();
        self.position += length as usize;
        Ok(value)
    }
    fn count(&mut self, section: &str) -> Result<usize, String> {
        let count = self.i32()?;
        if !(0..=2_000_000).contains(&count) {
            return Err(format!("Invalid {section} count: {count}"));
        }
        Ok(count as usize)
    }
    fn vector(&mut self) -> Result<BsorVector3, String> {
        Ok(BsorVector3 {
            x: self.f32()?,
            y: self.f32()?,
            z: self.f32()?,
        })
    }
    fn quaternion(&mut self) -> Result<BsorQuaternion, String> {
        Ok(BsorQuaternion {
            x: self.f32()?,
            y: self.f32()?,
            z: self.f32()?,
            w: self.f32()?,
        })
    }
    fn pose(&mut self) -> Result<BsorPose, String> {
        Ok(BsorPose {
            position: self.vector()?,
            rotation: self.quaternion()?,
        })
    }
}

fn decode_bsor(
    data: &[u8],
    source_url: String,
    source_rank: usize,
    cached_path: String,
) -> Result<BsorReplay, String> {
    let mut reader = BsorReader::new(data);
    if reader.i32()? != 0x442d3d69 || reader.u8()? != 1 {
        return Err("Replay is not a BSOR v1 file".to_owned());
    }
    let mut replay = BsorReplay {
        source_url,
        source_rank,
        cached_path,
        info: BsorInfo::default(),
        frames: vec![],
        notes: vec![],
        walls: vec![],
        heights: vec![],
        pauses: vec![],
    };
    while reader.position < data.len() {
        match reader.u8()? {
            0 => {
                replay.info = BsorInfo {
                    version: reader.string()?,
                    game_version: reader.string()?,
                    timestamp: reader.string()?,
                    player_id: reader.string()?,
                    player_name: reader.string()?,
                    platform: reader.string()?,
                    tracking_system: reader.string()?,
                    hmd: reader.string()?,
                    controller: reader.string()?,
                    hash: reader.string()?,
                    song_name: reader.string()?,
                    mapper: reader.string()?,
                    difficulty: reader.string()?,
                    score: reader.i32()?,
                    mode: reader.string()?,
                    environment: reader.string()?,
                    modifiers: reader.string()?,
                    jump_distance: reader.f32()?,
                    left_handed: reader.bool()?,
                    height: reader.f32()?,
                    start_time: reader.f32()?,
                    fail_time: reader.f32()?,
                    speed: reader.f32()?,
                };
            }
            1 => {
                for _ in 0..reader.count("frame")? {
                    let time = reader.f32()?;
                    let frame = BsorFrame {
                        time,
                        fps: reader.i32()?,
                        head: reader.pose()?,
                        left: reader.pose()?,
                        right: reader.pose()?,
                    };
                    if time != 0.0
                        && replay
                            .frames
                            .last()
                            .map(|item| item.time != time)
                            .unwrap_or(true)
                    {
                        replay.frames.push(frame);
                    }
                }
            }
            2 => {
                for _ in 0..reader.count("note")? {
                    let note_id = reader.i32()?;
                    let event_time = reader.f32()?;
                    let spawn_time = reader.f32()?;
                    let event_type = reader.i32()?;
                    let cut = if event_type == 0 || event_type == 1 {
                        let _speed_ok = reader.bool()?;
                        let direction_ok = reader.bool()?;
                        let _saber_type_ok = reader.bool()?;
                        let _was_cut_too_soon = reader.bool()?;
                        Some(BsorCutInfo {
                            saber_speed: reader.f32()?,
                            saber_direction: reader.vector()?,
                            saber_type: reader.i32()?,
                            time_deviation: reader.f32()?,
                            cut_direction_deviation: reader.f32()?,
                            cut_point: reader.vector()?,
                            cut_normal: reader.vector()?,
                            cut_distance_to_center: reader.f32()?,
                            cut_angle: reader.f32()?,
                            before_cut_rating: reader.f32()?,
                            after_cut_rating: reader.f32()?,
                            direction_ok,
                        })
                    } else {
                        None
                    };
                    replay.notes.push(BsorNote {
                        note_id,
                        event_time,
                        spawn_time,
                        event_type,
                        cut,
                    });
                }
            }
            3 => {
                for _ in 0..reader.count("wall")? {
                    let _wall_id = reader.i32()?;
                    replay.walls.push(BsorWall {
                        energy: reader.f32()?,
                        time: reader.f32()?,
                    });
                    let _spawn_time = reader.f32()?;
                }
            }
            4 => {
                for _ in 0..reader.count("height")? {
                    replay.heights.push(BsorHeight {
                        height: reader.f32()?,
                        time: reader.f32()?,
                    });
                }
            }
            5 => {
                for _ in 0..reader.count("pause")? {
                    replay.pauses.push(BsorPause {
                        duration: reader.i64()?,
                        time: reader.f32()?,
                    });
                }
            }
            6 => {
                for _ in 0..2 {
                    let _position = reader.vector()?;
                    let _rotation = reader.quaternion()?;
                }
            }
            7 => {
                for _ in 0..reader.count("custom data")? {
                    let _key = reader.string()?;
                    let length = reader.count("custom data byte")?;
                    if reader.position + length > data.len() {
                        return Err("Invalid BSOR custom data length".to_owned());
                    }
                    reader.position += length;
                }
            }
            section => return Err(format!("Unknown BSOR section: {section}")),
        }
    }
    if replay.frames.is_empty() {
        return Err("BSOR replay contains no pose frames".to_owned());
    }
    Ok(replay)
}

fn emit_status(app: &AppHandle, client_id: &str, status: &str, message: impl Into<String>) {
    let _ = app.emit(
        "mock-socket-status",
        SocketStatusEvent {
            client_id: client_id.to_owned(),
            status: status.to_owned(),
            message: message.into(),
        },
    );
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

    emit_status(
        &app,
        &client_id,
        "connecting",
        format!("Connecting to {address}:{port}"),
    );
    let tcp = TcpStream::connect((address.as_str(), port))
        .await
        .map_err(|e| format!("TCP connection failed: {e}"))?;
    let mut builder = TlsConnector::builder();
    builder.danger_accept_invalid_certs(accept_invalid_certificate);
    builder.danger_accept_invalid_hostnames(accept_invalid_certificate);
    let connector = builder
        .build()
        .map_err(|e| format!("TLS setup failed: {e}"))?;
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
                        emit_status(
                            &writer_app,
                            &writer_id,
                            "error",
                            format!("Socket write failed: {error}"),
                        );
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
        emit_status(
            &reader_app,
            &reader_id,
            "connected",
            "Raw TLS socket connected",
        );
        loop {
            let mut header = [0u8; 8];
            if let Err(error) = reader.read_exact(&mut header).await {
                emit_status(
                    &reader_app,
                    &reader_id,
                    "disconnected",
                    format!("Socket closed: {error}"),
                );
                break;
            }
            if &header[..4] != b"moon" {
                emit_status(&reader_app, &reader_id, "error", "Invalid packet magic");
                break;
            }
            let size = i32::from_le_bytes(header[4..8].try_into().unwrap());
            if size < 0 || size as usize > MAX_PACKET_SIZE {
                emit_status(
                    &reader_app,
                    &reader_id,
                    "error",
                    format!("Invalid packet size: {size}"),
                );
                break;
            }
            let mut payload = vec![0u8; size as usize];
            if let Err(error) = reader.read_exact(&mut payload).await {
                emit_status(
                    &reader_app,
                    &reader_id,
                    "error",
                    format!("Packet read failed: {error}"),
                );
                break;
            }
            let _ = reader_app.emit(
                "mock-raw-packet",
                RawPacketEvent {
                    client_id: reader_id.clone(),
                    payload: base64::engine::general_purpose::STANDARD.encode(payload),
                },
            );
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
    let sender = state
        .sockets
        .lock()
        .get(&client_id)
        .cloned()
        .ok_or_else(|| "Client socket is not connected".to_owned())?;
    sender
        .send(WriterCommand::Send(bytes))
        .await
        .map_err(|_| "Socket writer stopped".to_owned())
}

#[tauri::command]
async fn disconnect_socket(
    state: State<'_, Arc<SocketState>>,
    client_id: String,
) -> Result<(), String> {
    let sender = { state.sockets.lock().remove(&client_id) };
    if let Some(sender) = sender {
        sender
            .send(WriterCommand::Close)
            .await
            .map_err(|_| "Socket writer stopped".to_owned())?;
    }
    Ok(())
}

#[tauri::command]
fn sign_mock_token(platform_id: String, username: String) -> Result<String, String> {
    // The established MockClientConsole certificate is encrypted with
    // RC2-40-CBC. OpenSSL 3 keeps that cipher in its legacy provider.
    // Keep both providers alive until the key has finished signing.
    let _default_provider = Provider::load(None, "default")
        .map_err(|e| format!("Could not load the OpenSSL default provider: {e}"))?;
    let _legacy_provider = Provider::load(None, "legacy")
        .map_err(|e| format!("Could not load the OpenSSL legacy provider: {e}"))?;
    let parsed = Pkcs12::from_der(MOCK_CERTIFICATE)
        .and_then(|p12| p12.parse2(MOCK_CERTIFICATE_PASSWORD))
        .map_err(|e| format!("Could not unlock mock certificate: {e}"))?;
    let key = parsed
        .pkey
        .ok_or_else(|| "Mock certificate has no private key".to_owned())?;

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
    let encoded_header =
        URL_SAFE_NO_PAD.encode(serde_json::to_vec(&header).map_err(|e| e.to_string())?);
    let encoded_claims =
        URL_SAFE_NO_PAD.encode(serde_json::to_vec(&claims).map_err(|e| e.to_string())?);
    let signing_input = format!("{encoded_header}.{encoded_claims}");
    let mut signer = Signer::new(MessageDigest::sha256(), &key).map_err(|e| e.to_string())?;
    signer
        .update(signing_input.as_bytes())
        .map_err(|e| e.to_string())?;
    let signature = signer.sign_to_vec().map_err(|e| e.to_string())?;
    Ok(format!(
        "{signing_input}.{}",
        URL_SAFE_NO_PAD.encode(signature)
    ))
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
        .send()
        .await
        .map_err(|error| format!("BeatSaver lookup failed: {error}"))?
        .error_for_status()
        .map_err(|error| format!("BeatSaver rejected the map lookup: {error}"))?
        .json()
        .await
        .map_err(|error| format!("Invalid BeatSaver response: {error}"))?;
    let version = info["versions"]
        .as_array()
        .and_then(|versions| {
            versions.iter().find(|version| {
                version["hash"]
                    .as_str()
                    .map(|h| h.eq_ignore_ascii_case(&hash))
                    .unwrap_or(false)
            })
        })
        .or_else(|| {
            info["versions"]
                .as_array()
                .and_then(|versions| versions.first())
        })
        .ok_or_else(|| "BeatSaver map has no downloadable version".to_owned())?;
    let download_url = version["downloadURL"]
        .as_str()
        .unwrap_or_default()
        .to_owned();
    let bytes = client
        .get(&download_url)
        .send()
        .await
        .map_err(|error| format!("BeatSaver download failed: {error}"))?
        .error_for_status()
        .map_err(|error| format!("BeatSaver download was rejected: {error}"))?
        .bytes()
        .await
        .map_err(|error| format!("BeatSaver download failed: {error}"))?;
    let cache_dir = app
        .path()
        .app_cache_dir()
        .map_err(|error| error.to_string())?
        .join("mock-maps");
    fs::create_dir_all(&cache_dir)
        .map_err(|error| format!("Could not create map cache: {error}"))?;
    let cached_path = cache_dir.join(format!("{hash}.zip"));
    fs::write(&cached_path, bytes).map_err(|error| format!("Could not cache map: {error}"))?;

    Ok(BeatSaverMap {
        hash,
        key: info["id"].as_str().unwrap_or_default().to_owned(),
        name: info["metadata"]["songName"]
            .as_str()
            .or_else(|| info["name"].as_str())
            .unwrap_or("Unknown map")
            .to_owned(),
        description: info["description"].as_str().unwrap_or_default().to_owned(),
        song_sub_name: info["metadata"]["songSubName"]
            .as_str()
            .unwrap_or_default()
            .to_owned(),
        song_author_name: info["metadata"]["songAuthorName"]
            .as_str()
            .unwrap_or_default()
            .to_owned(),
        level_author_name: info["metadata"]["levelAuthorName"]
            .as_str()
            .unwrap_or_default()
            .to_owned(),
        bpm: info["metadata"]["bpm"].as_f64().unwrap_or_default(),
        duration_seconds: info["metadata"]["duration"]
            .as_f64()
            .unwrap_or(180.0)
            .round()
            .max(1.0) as u64,
        upvotes: info["stats"]["upvotes"].as_u64().unwrap_or_default(),
        downvotes: info["stats"]["downvotes"].as_u64().unwrap_or_default(),
        rating: info["stats"]["score"].as_f64().unwrap_or_default(),
        created_at: info["createdAt"].as_str().unwrap_or_default().to_owned(),
        version_created_at: version["createdAt"].as_str().unwrap_or_default().to_owned(),
        cover_url: version["coverURL"].as_str().unwrap_or_default().to_owned(),
        download_url,
        cached_path: cached_path.to_string_lossy().into_owned(),
    })
}

#[tauri::command]
async fn fetch_beatleader_replays(
    app: AppHandle,
    level_id: String,
    difficulty: String,
    characteristic: String,
    count: usize,
) -> Result<Vec<BsorReplay>, String> {
    let hash = level_id
        .split(|c: char| !c.is_ascii_hexdigit())
        .find(|part| part.len() == 40)
        .unwrap_or(level_id.as_str())
        .to_uppercase();
    let count = count.clamp(1, 16);
    let client = reqwest::Client::builder()
        .user_agent("TournamentAssistant-MockClient/0.1")
        .build()
        .map_err(|error| format!("BeatLeader client error: {error}"))?;

    let mut leaderboard_url = reqwest::Url::parse("https://api.beatleader.com/leaderboard")
        .map_err(|error| error.to_string())?;
    leaderboard_url
        .path_segments_mut()
        .map_err(|_| "Invalid BeatLeader base URL")?
        .push(&hash)
        .push(&difficulty)
        .push(&characteristic);
    let leaderboard: serde_json::Value = client
        .get(leaderboard_url)
        .send()
        .await
        .map_err(|error| format!("BeatLeader leaderboard lookup failed: {error}"))?
        .error_for_status()
        .map_err(|error| format!("BeatLeader has no matching leaderboard: {error}"))?
        .json()
        .await
        .map_err(|error| format!("Invalid BeatLeader leaderboard response: {error}"))?;
    let leaderboard_id = leaderboard["id"]
        .as_str()
        .ok_or_else(|| "BeatLeader leaderboard response has no ID".to_owned())?;
    let scores: serde_json::Value = client
        .get(format!(
            "https://api.beatleader.com/leaderboard/scores/{leaderboard_id}"
        ))
        .query(&[("page", 1usize), ("count", (count * 3).max(10))])
        .send()
        .await
        .map_err(|error| format!("BeatLeader score lookup failed: {error}"))?
        .error_for_status()
        .map_err(|error| format!("BeatLeader rejected the score lookup: {error}"))?
        .json()
        .await
        .map_err(|error| format!("Invalid BeatLeader score response: {error}"))?;
    let score_list = scores["scores"]
        .as_array()
        .or_else(|| leaderboard["scores"].as_array())
        .ok_or_else(|| "BeatLeader returned no score list".to_owned())?;
    let replay_urls: Vec<String> = score_list
        .iter()
        .filter_map(|score| score["replay"].as_str())
        .filter(|url| !url.is_empty())
        .take(count)
        .map(str::to_owned)
        .collect();
    if replay_urls.is_empty() {
        return Err("BeatLeader returned no downloadable replays for this map".to_owned());
    }

    let cache_dir = app
        .path()
        .app_cache_dir()
        .map_err(|error| error.to_string())?
        .join("beatleader-replays");
    fs::create_dir_all(&cache_dir)
        .map_err(|error| format!("Could not create replay cache: {error}"))?;
    let mut result = Vec::with_capacity(replay_urls.len());
    for (index, replay_url) in replay_urls.into_iter().enumerate() {
        let bytes = client
            .get(&replay_url)
            .send()
            .await
            .map_err(|error| format!("BeatLeader replay {} download failed: {error}", index + 1))?
            .error_for_status()
            .map_err(|error| format!("BeatLeader replay {} was rejected: {error}", index + 1))?
            .bytes()
            .await
            .map_err(|error| format!("BeatLeader replay {} download failed: {error}", index + 1))?;
        let safe_mode: String = characteristic
            .chars()
            .filter(|character| character.is_ascii_alphanumeric())
            .collect();
        let path = cache_dir.join(format!(
            "{hash}-{difficulty}-{safe_mode}-{}.bsor",
            index + 1
        ));
        fs::write(&path, &bytes)
            .map_err(|error| format!("Could not cache replay {}: {error}", index + 1))?;
        result.push(decode_bsor(
            &bytes,
            replay_url,
            index + 1,
            path.to_string_lossy().into_owned(),
        )?);
    }
    Ok(result)
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
            fetch_beatsaver_map,
            fetch_beatleader_replays
        ])
        .run(tauri::generate_context!())
        .expect("error while running TournamentAssistant Mock Client");
}

#[cfg(test)]
mod tests {
    use super::{decode_bsor, sign_mock_token};

    #[test]
    fn bundled_mock_certificate_signs_a_token() {
        let token = sign_mock_token("mock-test-id".into(), "Mock Test".into())
            .expect("the bundled MockClientConsole certificate should unlock with password");
        assert_eq!(token.split('.').count(), 3);
    }

    #[test]
    fn bsor_decoder_handles_fixture_when_provided() {
        let Ok(path) = std::env::var("TA_BSOR_TEST_FILE") else {
            return;
        };
        let bytes = std::fs::read(path).expect("could not read BSOR fixture");
        let replay = decode_bsor(&bytes, "fixture".into(), 1, "fixture".into())
            .expect("official BSOR fixture should decode");
        assert!(!replay.frames.is_empty());
        assert!(!replay.notes.is_empty());
        assert!(!replay.info.hash.is_empty());
    }
}
