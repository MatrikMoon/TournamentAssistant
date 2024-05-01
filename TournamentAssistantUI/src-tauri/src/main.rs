#![cfg_attr(
    all(not(debug_assertions), target_os = "windows"),
    windows_subsystem = "windows"
)]

mod screen;

use reqwest::Client;
use std::fs::{self, File};
use std::io::Write;
use std::process::{exit, Command as ProcessCommand};

use screen::draw::draw;
use screen::read::{get_monitors as get_monitors_inner, read_monitor_pixels};
use std::env;

#[tauri::command]
fn get_pixels(monitor_name: &str, window: tauri::Window) -> Vec<u8> {
    let monitors = get_monitors_inner(&window);
    let monitor = monitors
        .iter()
        .find(|x| x.name.eq(monitor_name))
        .expect(&format!(
            "Could not find monitor with name {}",
            monitor_name
        ));

    let pixels = read_monitor_pixels(&window, monitor).expect(&format!(
        "Failed to get pixels for monitor: {}",
        monitor_name
    ));

    draw();

    pixels
}

#[tauri::command]
fn get_monitors(window: tauri::Window) -> String {
    serde_json::to_string(&get_monitors_inner(&window)).unwrap()
}

#[tauri::command]
async fn update() -> Result<(), String> {
    async fn download_and_execute() -> Result<(), Box<dyn std::error::Error>> {
        // Output Path
        let output_path = "TAUpdater.exe";

        // Download the file
        let response = Client::new()
            .get("http://tournamentassistant.net/downloads/TAUpdater.exe")
            .send()
            .await?;
        {
            let mut file = File::create(output_path)?;
            let content = response.bytes().await?;
            file.write_all(&content)?;
        } // File is automatically closed here at the end of the block

        // Get the current executable path
        let current_exe_path = env::current_exe()?.to_str().unwrap_or("").to_string();

        // Execute the file with the current executable path as a parameter
        ProcessCommand::new(output_path)
            .arg("-taui")
            .arg(current_exe_path)
            .spawn()?;

        exit(0);
    }

    // Download the file
    download_and_execute().await.map_err(|e| e.to_string())?;

    Ok(())
}

#[tauri::command]
fn delete_updater() -> Result<(), String> {
    fs::remove_file("TAUpdater.exe").map_err(|e| e.to_string())?;
    Ok(())
}

fn main() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![
            get_pixels,
            get_monitors,
            update,
            delete_updater
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
