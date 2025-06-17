<script>
import { ref, onMounted, nextTick } from "vue";

export default {
  name: "LogBox",
  props: {
    messages: {
      type: Array,
      required: true,
    },
  },
  setup() {
    const logMessagesRef = ref(null);

    const scrollToBottom = async () => {
      await nextTick();
      if (logMessagesRef.value) {
        logMessagesRef.value.scrollTop = logMessagesRef.value.scrollHeight;
      }
    };

    onMounted(() => {
      scrollToBottom();
    });

    return {
      logMessagesRef,
    };
  },
};
</script>

<template>
  <div class="log-section">
    <div class="log-messages" ref="logMessagesRef">
      <div v-for="(log, index) in messages" :key="index" class="log-line">
        {{ log }}
      </div>
    </div>
  </div>
</template>

<style scoped>
.log-section {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow-y: auto;
  align-content: flex-end;
  font-family: "Jersey 10", sans-serif;
}

.log-messages {
  flex: 1;
  overflow-y: auto;
  padding: 1rem;
  margin-bottom: 1rem;
}

.log-line {
  font-family: monospace;
  margin-bottom: 4px;
  white-space: pre-wrap;
}
</style> 