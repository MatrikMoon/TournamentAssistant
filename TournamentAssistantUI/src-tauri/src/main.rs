#![cfg_attr(
    all(not(debug_assertions), target_os = "windows"),
    windows_subsystem = "windows"
)]

mod screen;

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

fn main() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![get_pixels, get_monitors])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
