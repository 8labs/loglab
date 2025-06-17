<script>
import { ref, onMounted, nextTick, onUnmounted, watch, computed } from "vue";

export default {
  name: "LogBox",
  props: {
    messages: {
      type: Array,
      required: true,
    },
    highlights: {
      type: Array,
      required: true,
    },
  },
  emits: ["highlight"],
  setup(props, { emit }) {
    const logMessagesRef = ref(null);
    const popupRef = ref(null);
    const selectedText = ref("");
    const popupPosition = ref({ x: 0, y: 0 });
    const showPopup = ref(false);
    const highlightedLines = ref(new Set());
    const filterText = ref("");
    const useRegex = ref(false);
    const filterError = ref("");

    // Computed property for filtered messages
    const filteredMessages = computed(() => {
      if (!filterText.value) return props.messages;

      try {
        if (useRegex.value) {
          const regex = new RegExp(filterText.value);
          return props.messages.filter((msg) => regex.test(msg.content));
        } else {
          const searchText = filterText.value.toLowerCase();
          return props.messages.filter((msg) =>
            msg.content.toLowerCase().includes(searchText)
          );
        }
      } catch (e) {
        filterError.value = "Invalid regular expression";
        return props.messages;
      }
    });

    // Watch for filter changes to clear error
    watch(filterText, () => {
      filterError.value = "";
    });

    const scrollToBottom = async () => {
      await nextTick();
      if (logMessagesRef.value) {
        logMessagesRef.value.scrollTop = logMessagesRef.value.scrollHeight;
      }
    };

    const handleTextSelection = () => {
      const selection = window.getSelection();
      if (!selection.toString().trim()) {
        showPopup.value = false;
        return;
      }

      // Check if selection is within log messages
      const logMessagesElement = logMessagesRef.value;
      if (!logMessagesElement) {
        showPopup.value = false;
        return;
      }

      const range = selection.getRangeAt(0);
      if (!logMessagesElement.contains(range.commonAncestorContainer)) {
        showPopup.value = false;
        return;
      }

      const rect = range.getBoundingClientRect();

      // Position popup below the selection
      popupPosition.value = {
        x: rect.left + rect.width / 2,
        y: rect.bottom + window.scrollY + 5,
      };

      selectedText.value = selection.toString();
      showPopup.value = true;
    };

    const handleHighlight = () => {
      const selection = window.getSelection();
      if (!selection.rangeCount) return;

      const range = selection.getRangeAt(0);
      const lineElement = range.startContainer.parentElement;
      const lineIndex = lineElement.dataset.messageId;
      const logMessageId = lineElement.dataset.messageId;

      // Calculate start and end positions within the log message
      const startPosition = range.startOffset;
      const endPosition = range.endOffset;

      // Create highlight span
      const highlightId = `highlight-${Date.now()}`;
      const span = document.createElement("span");
      span.className = "highlight";
      span.id = highlightId;
      range.surroundContents(span);

      // Add to highlighted lines
      highlightedLines.value.add(lineElement.dataset.messageId);

      // Emit highlight event
      emit("highlight", {
        text: selectedText.value,
        highlightId,
        lineIndex,
        logMessageId,
        startPosition,
        endPosition,
      });

      // Clear selection and hide popup
      selection.removeAllRanges();
      showPopup.value = false;
    };

    // Watch for new highlights and apply them to the log messages
    watch(
      () => props.highlights,
      (newHighlights) => {
        if (newHighlights.length > 0) {
          const latestHighlight = newHighlights[newHighlights.length - 1];
          const lineElement = document.querySelector(
            `.log-line[data-message-id="${latestHighlight.logMessageId}"]`
          );
          if (lineElement) {
            // Get the text content before any highlights were applied
            const originalText = lineElement.textContent;
            const before = originalText.substring(
              0,
              latestHighlight.startPosition
            );
            const after = originalText.substring(latestHighlight.endPosition);
            const highlightedText = originalText.substring(
              latestHighlight.startPosition,
              latestHighlight.endPosition
            );

            // Only apply the highlight if it hasn't been applied yet
            if (!lineElement.querySelector(`#${latestHighlight.id}`)) {
              lineElement.innerHTML = `${before}<span class="highlight" id="${latestHighlight.id}">${highlightedText}</span>${after}`;
            }
          }
        }
      },
      { deep: true }
    );

    onMounted(() => {
      scrollToBottom();
      document.addEventListener("selectionchange", handleTextSelection);
    });

    onUnmounted(() => {
      document.removeEventListener("selectionchange", handleTextSelection);
    });

    return {
      logMessagesRef,
      popupRef,
      showPopup,
      popupPosition,
      handleHighlight,
      highlightedLines,
      filterText,
      useRegex,
      filterError,
      filteredMessages,
    };
  },
};
</script>

<template>
  <div class="log-section">
    <div class="filter-container">
      <div class="filter-input-group">
        <input
          v-model="filterText"
          placeholder="Filter logs"
          type="text"
          class="filter-input"
        />
        <label
          class="regex-toggle"
          :class="{ active: useRegex }"
          @click="useRegex = !useRegex"
          title="Regular Expressions"
        >
          .*
        </label>
      </div>
      <div v-if="filterError" class="filter-error">{{ filterError }}</div>
    </div>
    <div class="log-messages" ref="logMessagesRef">
      <div
        v-for="(log, index) in filteredMessages"
        :key="log.id || index"
        class="log-line"
        :data-index="index"
        :data-message-id="log.id"
        v-html="log.content"
      ></div>
    </div>

    <!-- Highlight Popup -->
    <div
      v-if="showPopup"
      ref="popupRef"
      class="highlight-popup"
      :style="{
        left: `${popupPosition.x}px`,
        top: `${popupPosition.y}px`,
      }"
    >
      <button @click="handleHighlight" class="highlight-button">
        Highlight Selection
      </button>
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
  position: relative;
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

.highlight-popup {
  position: fixed;
  transform: translateX(-50%);
  border-radius: 4px;
  padding: 4px;
  z-index: 1000;
}

.highlight-button {
  background: #001d27dd;
  border: 1px solid #00bfd3;
  color: #ffffff;
  padding: 0.5rem 1rem;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.75rem;
}

.highlight-button:hover {
  background: #001d27;
}

:deep(.highlight) {
  color: #00bfd3;
  text-decoration: underline;
  border-radius: 2px;
  padding: 0 2px;
}

:deep(.highlight-pulse) {
  animation: highlight-pulse 2s ease-out;
}

@keyframes highlight-pulse {
  0% {
    background-color: #00bfd340;
  }
  100% {
    background-color: #00bfd320;
  }
}

.filter-container {
  padding: 0.5rem;
  background: #001d27;
}

.filter-input-group {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  padding: 0.5rem;
  border-radius: 4px;
  border: 1px solid #00bfd3;
}

.filter-input {
  flex: 1;
  border: none;
  padding: 0;
  background: #001d27;
  color: #ffffff;
}

.filter-input:focus {
  outline: none;
}

.regex-toggle {
  font-family: "Gudea", sans-serif;
  color: #ffffff;
  font-size: 1rem;
  padding: 0 0.25rem;
  border-radius: 4px;
  cursor: pointer;
}

.regex-toggle.active {
  background-color: #00bfd3;
  color: #ffffff;
}

.filter-error {
  color: #ff6b6b;
  font-size: 0.75rem;
  margin-top: 0.25rem;
}
</style> 