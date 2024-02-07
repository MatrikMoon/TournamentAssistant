extern crate winapi;

use std::ptr::null_mut;

use winapi::um::wingdi::{LineTo, MoveToEx};
use winapi::um::winuser::{GetDC, ReleaseDC};

pub fn draw() {
    unsafe {
        // Get the device context (DC) for the entire screen
        let hdc_screen = GetDC(null_mut());
        if hdc_screen.is_null() {
            eprintln!("Failed to get the device context.");
            return;
        }

        // Set the starting point for the line
        MoveToEx(hdc_screen, 100, 100, std::ptr::null_mut());
        // Draw the line to a new ending point
        LineTo(hdc_screen, 500, 500);

        // Release the device context
        ReleaseDC(null_mut(), hdc_screen);
    }
}
