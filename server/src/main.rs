use futures_util::{SinkExt, StreamExt};
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::sync::{Arc, Mutex};
use tokio::sync::broadcast;
use uuid::Uuid;
use warp::{ws::Message, Filter};

// Store active sessions and their broadcast channels
type Sessions = Arc<Mutex<HashMap<String, SessionChannels>>>;

#[derive(Debug, Serialize)]
struct SessionResponse {
    session_id: String,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
struct ChatMessage {
    sender: String,
    content: String,
    timestamp: u64,
}

#[derive(Clone)]
struct SessionChannels {
    pipe_tx: broadcast::Sender<String>,
    chat_tx: broadcast::Sender<ChatMessage>,
}

#[tokio::main]
async fn main() {
    // Initialize tracing
    tracing_subscriber::fmt::init();

    // Create shared state for sessions
    let sessions: Sessions = Arc::new(Mutex::new(HashMap::new()));

    // REST API endpoint for creating a new session
    let sessions_clone = sessions.clone();
    let create_session = warp::path("api")
        .and(warp::path("session"))
        .and(warp::get())
        .map(move || {
            let session_id = Uuid::new_v4().to_string();
            let (pipe_tx, _) = broadcast::channel(100);
            let (chat_tx, _) = broadcast::channel(100);
            sessions_clone.lock().unwrap().insert(
                session_id.clone(),
                SessionChannels { pipe_tx, chat_tx },
            );
            warp::reply::json(&SessionResponse { session_id })
        });

    // WebSocket endpoint for streaming logs and chat
    let sessions_ws = sessions.clone();
    let ws_route = warp::path("ws")
        .and(warp::path::param())
        .and(warp::ws())
        .map(move |session_id: String, ws: warp::ws::Ws| {
            let sessions = sessions_ws.clone();
            ws.on_upgrade(move |socket| handle_ws_connection(socket, session_id, sessions))
        });

    // Combine routes
    let routes = create_session.or(ws_route);

    // Start server
    println!("Starting server on http://localhost:8080");
    warp::serve(routes).run(([127, 0, 0, 1], 8080)).await;
}

async fn handle_ws_connection(
    ws: warp::ws::WebSocket,
    session_id: String,
    sessions: Sessions,
) {
    let (mut ws_sender, mut ws_receiver) = ws.split();

    // Get the channels for this session
    let channels = {
        let sessions = sessions.lock().unwrap();
        match sessions.get(&session_id) {
            Some(channels) => channels.clone(),
            None => {
                eprintln!("Invalid session ID: {}", session_id);
                return;
            }
        }
    };

    // Clone session_id for the spawned task
    let session_id_clone = session_id.clone();

    // Spawn a task to handle incoming messages
    let mut pipe_rx = channels.pipe_tx.subscribe();
    let mut chat_rx = channels.chat_tx.subscribe();

    tokio::spawn(async move {
        while let Some(msg) = ws_receiver.next().await {
            match msg {
                Ok(msg) => {
                    if let Ok(text) = msg.to_str() {
                        // Try to parse as chat message
                        if let Ok(chat_msg) = serde_json::from_str::<ChatMessage>(text) {
                            // Broadcast chat message to all clients
                            if let Err(e) = channels.chat_tx.send(chat_msg.clone()) {
                                eprintln!("Failed to broadcast chat message: {}", e);
                            }
                        } else {
                            // Treat as pipe data
                            println!("[Session {}] Received pipe data: {}", session_id_clone, text);
                            if let Err(e) = channels.pipe_tx.send(text.to_string()) {
                                eprintln!("Failed to broadcast pipe data: {}", e);
                            }
                        }
                    }
                }
                Err(e) => {
                    eprintln!("WebSocket error: {}", e);
                    break;
                }
            }
        }
    });

    // Handle outgoing messages
    loop {
        tokio::select! {
            Ok(msg) = pipe_rx.recv() => {
                if ws_sender.send(Message::text(format!("pipe:{}", msg))).await.is_err() {
                    break;
                }
            }
            Ok(msg) = chat_rx.recv() => {
                if ws_sender.send(Message::text(serde_json::to_string(&msg).unwrap())).await.is_err() {
                    break;
                }
            }
        }
    }

    // Clean up the session when the connection is closed
    sessions.lock().unwrap().remove(&session_id);
    println!("Session {} closed", session_id);
} 