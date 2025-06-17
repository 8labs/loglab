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
        sender: "User",
        content: message,
        timestamp: Date.now(),
      };

      if (ws.value && ws.value.readyState === WebSocket.OPEN) {
        ws.value.send(JSON.stringify(chatMessage));
      } else {
        console.error("WebSocket is not connected");
      }
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
      handleSendMessage,
    };
  },
};
</script>

<template>
  <div class="app">
    <div class="container">
      <ChatBox :messages="chatMessages" @send-message="handleSendMessage" />
      <LogBox :messages="logMessages" />
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
</style>
