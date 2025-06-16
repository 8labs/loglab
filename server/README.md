# LogLab Server

A WebSocket server that handles log streaming sessions. It provides a REST API for session creation and WebSocket endpoints for log streaming.

## Features

- REST API endpoint for creating new sessions
- WebSocket endpoint for streaming logs
- Session management with unique UUIDs
- Console output of received logs
- Automatic session cleanup on disconnect

## Building

```bash
cargo build --release
```

The binary will be available at `target/release/loglab-server`

## Running

```bash
cargo run
```

The server will start on `http://localhost:8080` with the following endpoints:

- REST API: `GET http://localhost:8080/api/session`
  - Returns a JSON response with a `session_id`
- WebSocket: `ws://localhost:8080/ws/{session_id}`
  - Accepts WebSocket connections for streaming logs

## Usage with LogLab CLI

1. Start the server:
```bash
cargo run
```

2. In another terminal, use the LogLab CLI to stream logs:
```bash
tail -f somefile.log | ./loglab
```

The server will print received logs to the console with their session IDs.

## Requirements

- Rust 1.70 or later
- Cargo 