# LogLab .NET Server

A .NET implementation of the LogLab server using ASP.NET Core and SignalR for WebSocket support.

## Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022 or Visual Studio Code with C# extensions

## Building and Running

1. Navigate to the server directory:
   ```bash
   cd server-dotnet
   ```

2. Build the project:
   ```bash
   dotnet build
   ```

3. Run the server:
   ```bash
   dotnet run
   ```

The server will start on `http://localhost:5000` by default.

## API Endpoints

### Create Session
- **URL**: `/api/session`
- **Method**: `GET`
- **Response**: 
  ```json
  {
    "session_id": "guid-string"
  }
  ```

### WebSocket Connection
- **URL**: `/ws/{sessionId}`
- **Protocol**: WebSocket
- **Events**:
  - `SendMessage`: Send a message to the server
  - `ReceiveChatMessage`: Receive a chat message
  - `ReceivePipeData`: Receive pipe data

## Features

- Session management with unique IDs
- Real-time WebSocket communication using SignalR
- Support for both chat messages and pipe data
- CORS enabled for Vue.js development server
- Concurrent message handling with thread-safe collections 