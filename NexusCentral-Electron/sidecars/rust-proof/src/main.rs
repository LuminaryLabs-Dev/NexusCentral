use std::env;
use std::time::{SystemTime, UNIX_EPOCH};

fn main() {
    let json = env::args().any(|arg| arg == "--json");
    let now = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|duration| duration.as_secs())
        .unwrap_or_default();

    if json {
        println!(
            "{{\"ok\":true,\"sidecar\":\"nexuscentral-rust-proof\",\"unixTime\":{},\"message\":\"Rust sidecar ready\"}}",
            now
        );
        return;
    }

    println!("Rust sidecar ready at {}", now);
}
