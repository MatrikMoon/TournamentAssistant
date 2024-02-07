use scrap::{Capturer, Display};
use serde::ser::{Serialize, SerializeStruct, Serializer};
use std::{io::ErrorKind::WouldBlock, thread, time::Duration};

pub struct Monitor {
    pub name: String,
    pub width: i32,
    pub height: i32,
    pub x: i32,
    pub y: i32,
}

impl Serialize for Monitor {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        let mut state = serializer.serialize_struct("Monitor", 5)?;
        state.serialize_field("name", &self.name)?;
        state.serialize_field("width", &self.width)?;
        state.serialize_field("height", &self.height)?;
        state.serialize_field("x", &self.x)?;
        state.serialize_field("y", &self.y)?;
        state.end()
    }
}

pub fn get_monitors(window: &tauri::Window) -> Vec<Monitor> {
    let mut result = vec![];

    for monitor in window.available_monitors().unwrap() {
        result.push(Monitor {
            name: monitor
                .name()
                .unwrap_or(&"Monitior name not found".to_string())
                .to_string(),
            width: i32::try_from(monitor.size().width)
                .expect("Expected monitor size to be positive"),
            height: i32::try_from(monitor.size().height)
                .expect("Expected monitor size to be positive"),
            x: monitor.position().x,
            y: monitor.position().y,
        });
    }

    result
}

pub fn read_monitor_pixels(
    window: &tauri::Window,
    monitor: &Monitor,
    // x: Option<u32>,
    // y: Option<u32>,
) -> Result<Vec<u8>, Box<dyn std::error::Error>> {
    // Can't seem to get display names from scrap, so we'll just... Use the display at the same index
    // Someone tell me if this is wrong
    let monitor_index = get_monitors(window)
        .iter()
        .position(|x| x.name.eq(&monitor.name))
        .expect(&format!(
            "Could not find monitor with name: {}",
            monitor.name
        ));

    let all_displays: Vec<Display> = Display::all()?;
    let mut capturer = Capturer::new(
        all_displays
            .into_iter()
            .nth(monitor_index)
            .expect(&format!("Monitor at index {} not found", monitor_index)),
    )?;
    let (width, height) = (capturer.width(), capturer.height());

    println!("Capturing frame...");

    loop {
        match capturer.frame() {
            Ok(frame) => {
                println!("Captured frame!");

                let mut pixels = Vec::with_capacity(width * height * 4);
                for chunk in frame.chunks_exact(4) {
                    // Convert BGRA to RGBA
                    let rgba = [chunk[2], chunk[1], chunk[0], chunk[3]]; // Assuming the source is BGRA
                    pixels.extend_from_slice(&rgba);
                }

                println!("Converted to RGBA frame!");
                return Ok(pixels);
            }
            Err(error) if error.kind() == WouldBlock => {
                // Wait and try again
                thread::sleep(Duration::from_millis(100));
                continue;
            }
            Err(e) => return Err(Box::new(e)),
        }
    }
}
