# LogLab CLI

A command-line tool for streaming log data to a WebSocket server. Available in both Rust and Go implementations.

## Features

- Reads data from stdin (e.g., from pipe)
- Connects to a REST API to get a session ID
- Streams data over WebSocket using the session ID
- Supports Mac/Linux (Windows support coming soon)

## Building

### Rust Version

```bash
cd rust
cargo build --release
```

The binary will be available at `target/release/loglab`

### Go Version

```bash
cd go
go build
```

The binary will be available as `loglab` in the current directory

## Usage

Both versions can be used the same way:

```bash
# Using with tail
tail -f somefile.log | ./loglab

# Using with any other command that outputs to stdout
some-command | ./loglab
```

## Configuration

The application currently uses the following default endpoints:
- REST API: `http://localhost:8080/api/session`
- WebSocket: `ws://localhost:8080/ws/{session_id}`

These endpoints can be configured by modifying the source code.

## Requirements

### Rust Version
- Rust 1.70 or later
- Cargo

### Go Version
- Go 1.21 or later 