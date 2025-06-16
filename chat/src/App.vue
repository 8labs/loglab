<script>
import { ref, onMounted, onUnmounted, nextTick } from "vue";

export default {
  name: "App",
  setup() {
    const ws = ref(null);
    const chatMessages = ref([]);
    const logMessages = ref([]);
    const newMessage = ref("");
    const chatMessagesRef = ref(null);
    const logMessagesRef = ref(null);
    const sessionId = ref("");

    const connectWebSocket = async () => {
      // Get session ID from URL
      const path = window.location.pathname;
      const urlSessionId = path.split("/").pop();

      if (
        urlSessionId &&
        urlSessionId.match(
          /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
        )
      ) {
        sessionId.value = urlSessionId;
      } else {
        // Update URL with new session ID
        window.history.replaceState({}, "", `/${sessionId.value}`);
      }

      // Connect to WebSocket
      ws.value = new WebSocket(`wss://localhost:5001/ws/${sessionId.value}`);

      ws.value.onmessage = (event) => {
        const data = event.data;
        try {
          const message = JSON.parse(data);
          if (message.sender && message.content && message.timestamp) {
            // Handle chat message
            chatMessages.value.push(message);
            scrollToBottom(chatMessagesRef);
          } else {
            // Handle pipe data
            logMessages.value.push(data);
            scrollToBottom(logMessagesRef);
          }
        } catch (e) {
          // If not JSON, treat as pipe data
          logMessages.value.push(data);
          scrollToBottom(logMessagesRef);
        }
      };

      ws.value.onclose = () => {
        console.log("WebSocket connection closed");
      };

      ws.value.onerror = (error) => {
        console.error("WebSocket error:", error);
      };
    };

    const sendMessage = () => {
      if (!newMessage.value.trim()) return;

      const message = {
        sender: "User", // You might want to make this configurable
        content: newMessage.value,
        timestamp: Date.now(),
      };

      if (ws.value && ws.value.readyState === WebSocket.OPEN) {
        ws.value.send(JSON.stringify(message));
        newMessage.value = "";
      } else {
        console.error("WebSocket is not connected");
      }
    };

    const scrollToBottom = async (element) => {
      await nextTick();
      if (element.value) {
        element.value.scrollTop = element.value.scrollHeight;
      }
    };

    const formatTime = (timestamp) => {
      return new Date(timestamp).toLocaleTimeString();
    };

    onMounted(() => {
      connectWebSocket();
    });

    onUnmounted(() => {
      if (ws.value) {
        ws.value.close();
      }
    });

    return {
      chatMessages,
      logMessages,
      newMessage,
      chatMessagesRef,
      logMessagesRef,
      sendMessage,
      formatTime,
    };
  },
};
</script>

<template>
  <div class="app">
    <div class="container">
      <div class="chat-section">
        <div class="chat-messages" ref="chatMessagesRef">
          <div v-for="msg in chatMessages" :key="msg.timestamp" class="message">
            <span class="timestamp">{{ formatTime(msg.timestamp) }}</span>
            <span class="sender">{{ msg.sender }}:</span>
            <span class="content">{{ msg.content }}</span>
          </div>
        </div>
        <div class="chat-input">
          <input
            v-model="newMessage"
            @keyup.enter="sendMessage"
            placeholder="Type a message..."
            type="text"
          />
          <button @click="sendMessage">Send</button>
        </div>
      </div>
      <div class="log-section">
        <div class="log-messages" ref="logMessagesRef">
          <div
            v-for="(log, index) in logMessages"
            :key="index"
            class="log-line"
          >
            {{ log }}
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style>
.app {
  height: 100vh;
  width: 100%;
  padding: 2rem;
  box-sizing: border-box;
}

.container {
  display: flex;
  gap: 2rem;
  height: 100%;
  width: 100%;
}

.chat-section,
.log-section {
  flex: 1;
  display: flex;
  flex-direction: column;
  background: #f5f5f5;
  border-radius: 8px;
  overflow: hidden;
}

.chat-messages,
.log-messages {
  flex: 1;
  overflow-y: auto;
  padding: 20px;
}

.message {
  margin-bottom: 10px;
  line-height: 1.4;
}

.timestamp {
  color: #666;
  font-size: 0.8em;
  margin-right: 8px;
}

.sender {
  font-weight: bold;
  margin-right: 8px;
}

.log-line {
  font-family: monospace;
  margin-bottom: 4px;
  white-space: pre-wrap;
}

.chat-input {
  display: flex;
  gap: 10px;
  padding: 20px;
  background: #fff;
  border-top: 1px solid #ddd;
}

input {
  flex: 1;
  padding: 8px 12px;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 14px;
}

button {
  padding: 8px 16px;
  background: #007bff;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}

button:hover {
  background: #0056b3;
}
</style>
