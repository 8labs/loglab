use anyhow::{Context, Result};
use clap::Parser;
use futures_util::{Sink, SinkExt, StreamExt};
use notify::{Config, Event, RecommendedWatcher, RecursiveMode, Watcher};
use serde::{Deserialize};
use std::fs::File;
use std::io::{self, BufRead, BufReader, Seek, SeekFrom};
use std::path::PathBuf;
use std::sync::mpsc::channel;
use tokio_tungstenite::{connect_async, tungstenite::Message};
use url::Url;

#[derive(Debug, Deserialize)]
struct SessionResponse {
    session_id: String,
}

#[derive(Parser, Debug)]
#[command(author, version, about, long_about = None)]
struct Args {
    /// File to watch (if not provided, reads from stdin)
    file: Option<PathBuf>,

    /// Show all existing content before watching for updates
    #[arg(short, long)]
    all: bool,
}

async fn stream_to_websocket(
    mut write: impl Sink<Message, Error = impl std::error::Error + Send + Sync + 'static> + Unpin,
    line: String,
) -> Result<()> {
    write
        .send(Message::Text(line))
        .await
        .context("Failed to send message to WebSocket")?;
    Ok(())
}

async fn handle_file_input(
    file_path: PathBuf,
    show_all: bool,
    mut write: impl Sink<Message, Error = impl std::error::Error + Send + Sync + 'static> + Unpin,
) -> Result<()> {
    let file = File::open(&file_path).context("Failed to open file")?;
    let mut reader = BufReader::new(file);

    // If --all is specified, read and send all existing content
    if show_all {
        let mut line = String::new();
        while reader.read_line(&mut line)? > 0 {
            stream_to_websocket(&mut write, line.trim_end().to_string()).await?;
            line.clear();
        }
    }

    // Set up file watching
    let (tx, rx) = channel();
    let mut watcher: RecommendedWatcher = Watcher::new(
        move |res: Result<Event, _>| {
            if let Ok(event) = res {
                tx.send(event).unwrap();
            }
        },
        Config::default(),
    )?;

    // Watch the file for changes
    watcher.watch(&file_path, RecursiveMode::NonRecursive)?;

    // Get the current file size for tracking new content
    let mut last_pos = reader.seek(SeekFrom::End(0))?;

    // Process file events
    for event in rx {
        if event.kind.is_modify() {
            let file = File::open(&file_path)?;
            let mut reader = BufReader::new(file);
            reader.seek(SeekFrom::Start(last_pos))?;

            let mut line = String::new();
            while reader.read_line(&mut line)? > 0 {
                stream_to_websocket(&mut write, line.trim_end().to_string()).await?;
                line.clear();
            }

            last_pos = reader.seek(SeekFrom::Current(0))?;
        }
    }

    Ok(())
}

async fn handle_stdin_input(
    mut write: impl Sink<Message, Error = impl std::error::Error + Send + Sync + 'static> + Unpin,
) -> Result<()> {
    let stdin = io::stdin();
    let reader = stdin.lock();

    for line in reader.lines() {
        let line = line.context("Failed to read line from stdin")?;
        stream_to_websocket(&mut write, line).await?;
    }

    Ok(())
}

#[tokio::main]
async fn main() -> Result<()> {
    let args = Args::parse();

    // Get session ID from REST API
    let client = reqwest::Client::new();
    let session: SessionResponse = client
        .get("https://logapi.8labs.com/api/session")
        .send()
        .await?
        .json()
        .await?;

    println!("Got a new session id.");

    // Connect to WebSocket
    let url = format!("wss://logapi.8labs.com/ws/{}", session.session_id);
    let url = url.parse::<Url>()?;
    let (ws_stream, _) = connect_async(url).await?;
    let (write, _) = ws_stream.split();

    println!("Started a new session at https://loglab.8labs.com/{}", session.session_id);

    // Handle input based on whether a file was specified
    match args.file {
        Some(file_path) => {
            handle_file_input(file_path, args.all, write).await?;
        }
        None => {
            handle_stdin_input(write).await?;
        }
    }

    Ok(())
} 