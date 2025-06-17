<script>
import { ref, onMounted, nextTick } from "vue";

export default {
  name: "ChatBox",
  props: {
    messages: {
      type: Array,
      required: true,
    },
  },
  emits: ["send-message"],
  setup(props, { emit }) {
    const newMessage = ref("");
    const chatMessagesRef = ref(null);

    const sendMessage = () => {
      if (!newMessage.value.trim()) return;

      emit("send-message", newMessage.value);
      newMessage.value = "";
    };

    const scrollToBottom = async () => {
      await nextTick();
      if (chatMessagesRef.value) {
        chatMessagesRef.value.scrollTop = chatMessagesRef.value.scrollHeight;
      }
    };

    const formatTime = (timestamp) => {
      return new Date(timestamp).toLocaleTimeString();
    };

    onMounted(() => {
      scrollToBottom();
    });

    return {
      newMessage,
      chatMessagesRef,
      sendMessage,
      formatTime,
    };
  },
};
</script>

<template>
  <div class="chat-section">
    <div class="chat-messages" ref="chatMessagesRef">
      <div v-for="msg in messages" :key="msg.timestamp" class="message">
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
      <img src="/send.svg" @click="sendMessage" class="send-icon" />
    </div>
  </div>
</template>

<style scoped>
.chat-section {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow-y: auto;
  align-content: flex-end;
  font-family: "Gudea", sans-serif;
}

.chat-messages {
  flex: 1;
  overflow-y: auto;
  padding: 1rem;
  margin-bottom: 1rem;
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

.chat-input {
  display: flex;
  gap: 10px;
  border-radius: 4px;
  border: 1px solid #00bfd3;
  background-color: #00bfd310;
  color: #ffffff;
}

.chat-input input {
  flex: 1;
  padding: 8px 12px;
  font-size: 14px;
  background-color: transparent;
  border: none;
  color: #ffffff;
}

.chat-input input:focus {
  outline: none;
}

.send-icon {
  padding: 0 0.5rem;
  cursor: pointer;
}
</style> 