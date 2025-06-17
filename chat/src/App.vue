<script>
import { ref, onMounted, onUnmounted } from "vue";
import ChatBox from "./components/ChatBox.vue";
import LogBox from "./components/LogBox.vue";

export default {
  name: "App",
  components: {
    ChatBox,
    LogBox,
  },
  setup() {
    const ws = ref(null);
    const chatMessages = ref([]);
    const logMessages = ref([]);
    const sessionId = ref("");
    const highlights = ref([]);
    const username = ref("");
    const showUsernameDialog = ref(true);

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
      ws.value = new WebSocket(`wss://logapi.8labs.com/ws/${sessionId.value}`);

      ws.value.onmessage = (event) => {
        const data = event.data;
        try {
          const message = JSON.parse(data);
          if (message.sender && message.content && message.timestamp) {
            // Handle chat message
            chatMessages.value.push(message);
          } else {
            // Handle pipe data
            logMessages.value.push(data.substring(5));
          }
        } catch (e) {
          // If not JSON, treat as pipe data
          logMessages.value.push(data.substring(5));
        }
      };

      ws.value.onclose = () => {
        console.log("WebSocket connection closed");
      };

      ws.value.onerror = (error) => {
        console.error("WebSocket error:", error);
      };
    };

    const handleSendMessage = (message) => {
      const chatMessage = {
        sender: username.value,
        content: message,
        timestamp: Date.now(),
      };

      if (ws.value && ws.value.readyState === WebSocket.OPEN) {
        ws.value.send(JSON.stringify(chatMessage));
      } else {
        console.error("WebSocket is not connected");
      }
    };

    const handleHighlight = ({ text, highlightId, lineIndex }) => {
      // Add to highlights array
      const highlight = {
        id: highlightId,
        text,
        lineIndex,
        timestamp: Date.now(),
      };
      highlights.value.push(highlight);

      // Send system message
      const message = {
        sender: "System",
        content: `Highlighted text: "${text}"`,
        highlightId,
        timestamp: Date.now(),
      };

      if (ws.value && ws.value.readyState === WebSocket.OPEN) {
        ws.value.send(JSON.stringify(message));
      }
    };

    const handleScrollToHighlight = (highlightId) => {
      const highlight = highlights.value.find((h) => h.id === highlightId);
      if (highlight) {
        // Emit event to LogBox to scroll to highlight
        const logBox = document.querySelector(".log-section");
        if (logBox) {
          const element = document.getElementById(highlightId);
          if (element) {
            element.scrollIntoView({ behavior: "smooth", block: "center" });
            element.classList.add("highlight-pulse");
            setTimeout(() => {
              element.classList.remove("highlight-pulse");
            }, 2000);
          }
        }
      }
    };

    const handleJoin = () => {
      if (username.value.trim()) {
        showUsernameDialog.value = false;
        connectWebSocket();
      }
    };

    onMounted(() => {
      // Check if username exists in localStorage
      const savedUsername = localStorage.getItem("username");
      if (savedUsername) {
        username.value = savedUsername;
        showUsernameDialog.value = false;
        connectWebSocket();
      }
    });

    onUnmounted(() => {
      if (ws.value) {
        ws.value.close();
      }
    });

    return {
      chatMessages,
      logMessages,
      highlights,
      username,
      showUsernameDialog,
      handleSendMessage,
      handleHighlight,
      handleScrollToHighlight,
      handleJoin,
    };
  },
};
</script>

<template>
  <div class="app">
    <div v-if="showUsernameDialog" class="username-dialog-overlay">
      <div class="username-dialog">
        <div class="input-group">
          <input
            v-model="username"
            @keyup.enter="handleJoin"
            placeholder="Username"
            type="text"
            autofocus
          />
          <button @click="handleJoin" :disabled="!username.trim()">Join</button>
        </div>
      </div>
    </div>
    <div v-else class="container">
      <ChatBox
        :messages="chatMessages"
        :highlights="highlights"
        @send-message="handleSendMessage"
        @scroll-to-highlight="handleScrollToHighlight"
      />
      <LogBox
        :messages="logMessages"
        :highlights="highlights"
        @highlight="handleHighlight"
      />
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

.username-dialog-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.username-dialog {
  background: #001d27;

  border-radius: 8px;
  padding: 2rem;
  width: 100%;
  max-width: 400px;
}

.username-dialog h2 {
  color: #ffffff;
  margin: 0 0 1.5rem 0;
  text-align: center;
  font-size: 1.25rem;
}

.input-group {
  display: flex;
  gap: 1rem;
}

.input-group input {
  flex: 1;
  padding: 0.75rem;
  border: 1px solid #00bfd3;
  border-radius: 4px;
  background: #001d27;
  color: #ffffff;
  font-size: 1rem;
}

.input-group input:focus {
  outline: none;
  border-color: #00bfd3;
  box-shadow: 0 0 0 2px rgba(0, 191, 211, 0.2);
}

.input-group button {
  padding: 0.75rem 1.5rem;
  border: 1px solid #00bfd3;
  border-radius: 4px;
  background: #00bfd310;
  color: #ffffff;
  font-size: 1rem;
  cursor: pointer;
  transition: background-color 0.2s;
}

.input-group button:hover:not(:disabled) {
  background: #00bfd320;
}

.input-group button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
