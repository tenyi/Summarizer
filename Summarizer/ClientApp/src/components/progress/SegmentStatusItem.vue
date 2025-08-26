<!-- 單個分段狀態顯示元件 -->
<template>
  <div
    class="segment-status-item"
    :class="itemClasses"
  >
    <!-- 狀態指示器 -->
    <div class="status-indicator">
      <div
        class="status-icon"
        :class="statusIconClasses"
      >
        {{ statusIcon }}
      </div>
      <div v-if="showProgress" class="mini-progress">
        <div 
          class="mini-progress-fill"
          :style="{ width: `${segmentProgress}%` }"
        ></div>
      </div>
    </div>

    <!-- 主要內容 -->
    <div class="item-content">
      <!-- 標題行 -->
      <div class="title-row">
        <h4 class="segment-title">
          <span class="segment-index">#{{ segment.index + 1 }}</span>
          {{ segment.title || `分段 ${segment.index + 1}` }}
        </h4>
        <span class="status-text" :class="statusTextClasses">
          {{ statusText }}
        </span>
      </div>

      <!-- 詳細資訊（展開時顯示） -->
      <div v-if="showDetails && !compact" class="details-section">
        <div class="detail-row">
          <span class="detail-label">內容長度:</span>
          <span class="detail-value">{{ formatLength(segment.contentLength) }} 字符</span>
        </div>
        
        <div v-if="segment.processingTimeMs" class="detail-row">
          <span class="detail-label">處理時間:</span>
          <span class="detail-value">{{ formatTime(segment.processingTimeMs) }}</span>
        </div>
        
        <div v-if="segment.startTime" class="detail-row">
          <span class="detail-label">開始時間:</span>
          <span class="detail-value">{{ formatDateTime(segment.startTime) }}</span>
        </div>
        
        <div v-if="segment.resultLength" class="detail-row">
          <span class="detail-label">結果長度:</span>
          <span class="detail-value">{{ formatLength(segment.resultLength) }} 字符</span>
        </div>
      </div>

      <!-- 錯誤資訊 -->
      <div v-if="segment.errorMessage && segment.status === 'failed'" class="error-section">
        <div class="error-message">
          <span class="error-icon">⚠️</span>
          <span class="error-text">{{ segment.errorMessage }}</span>
        </div>
        
        <div v-if="segment.retryCount > 0" class="retry-info">
          已重試 {{ segment.retryCount }} 次
          <button
            v-if="canRetry"
            class="retry-button"
            @click="handleRetry"
          >
            重試
          </button>
        </div>
      </div>

      <!-- 重試中的動畫 -->
      <div v-if="segment.status === 'retrying'" class="retrying-animation">
        <div class="retrying-dots">
          <span class="dot"></span>
          <span class="dot"></span>
          <span class="dot"></span>
        </div>
        <span class="retrying-text">重試中...</span>
      </div>
    </div>

    <!-- 操作按鈕區域 -->
    <div v-if="showActions" class="action-buttons">
      <button
        v-if="segment.status === 'failed' && canRetry"
        class="action-btn retry"
        @click="handleRetry"
        title="重試此分段"
      >
        🔄
      </button>
      
      <button
        v-if="segment.errorMessage"
        class="action-btn error"
        @click="handleShowError"
        title="查看詳細錯誤"
      >
        ⚠️
      </button>
      
      <button
        v-if="segment.status === 'completed'"
        class="action-btn info"
        @click="toggleDetails"
        title="查看詳細資訊"
      >
        ℹ️
      </button>
    </div>

    <!-- 當前處理指示器 -->
    <div
      v-if="isCurrent"
      class="current-indicator"
      title="目前處理中"
    >
      <div class="current-pulse"></div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { SegmentStatus, SegmentProcessingStatus } from '@/types/progress'

// Props 定義
interface Props {
  segment: SegmentStatus              // 分段狀態資料
  isCurrent?: boolean                 // 是否為當前處理的分段
  showDetails?: boolean               // 是否顯示詳細資訊
  compact?: boolean                   // 緊湊模式
  showActions?: boolean               // 是否顯示操作按鈕
  maxRetryCount?: number              // 最大重試次數
}

const props = withDefaults(defineProps<Props>(), {
  isCurrent: false,
  showDetails: false,
  compact: false,
  showActions: true,
  maxRetryCount: 3
})

// Emits 定義
const emit = defineEmits<{
  retry: [segmentIndex: number]
  showError: [segment: SegmentStatus]
}>()

// 內部狀態
const detailsExpanded = ref(false)

// 樣式類別計算
const itemClasses = computed(() => [
  'base-item',
  `status-${props.segment.status}`,
  {
    'is-current': props.isCurrent,
    'compact': props.compact,
    'has-error': props.segment.status === 'failed',
    'retrying': props.segment.status === 'retrying',
    'processing': props.segment.isProcessing,
    'completed': props.segment.status === 'completed'
  }
])

const statusIconClasses = computed(() => [
  'icon-base',
  `icon-${props.segment.status}`
])

const statusTextClasses = computed(() => [
  'status-base',
  `text-${props.segment.status}`
])

// 狀態圖示和文字
const statusIcon = computed(() => {
  const icons: Record<SegmentProcessingStatus, string> = {
    'pending': '⏳',
    'processing': '⚡',
    'completed': '✅',
    'failed': '❌',
    'retrying': '🔄'
  }
  return icons[props.segment.status] || '❓'
})

const statusText = computed(() => {
  const texts: Record<SegmentProcessingStatus, string> = {
    'pending': '等待中',
    'processing': '處理中',
    'completed': '已完成',
    'failed': '失敗',
    'retrying': '重試中'
  }
  return texts[props.segment.status] || '未知'
})

// 進度相關
const showProgress = computed(() => 
  props.segment.status === 'processing' || props.segment.status === 'retrying'
)

const segmentProgress = computed(() => {
  // 這裡可以根據實際需求計算分段內部進度
  // 目前簡單根據是否在處理中返回動態值
  return props.segment.isProcessing ? 50 : 0
})

// 操作權限
const canRetry = computed(() => 
  props.segment.status === 'failed' && 
  props.segment.retryCount < props.maxRetryCount
)

// 格式化函數
const formatLength = (length: number): string => {
  if (length >= 1000) {
    return `${(length / 1000).toFixed(1)}K`
  }
  return length.toString()
}

const formatTime = (milliseconds: number): string => {
  const seconds = Math.floor(milliseconds / 1000)
  if (seconds >= 60) {
    const minutes = Math.floor(seconds / 60)
    return `${minutes}分${seconds % 60}秒`
  }
  return `${seconds}秒`
}

const formatDateTime = (isoString: string): string => {
  const date = new Date(isoString)
  return date.toLocaleTimeString('zh-TW', {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  })
}

// 事件處理
const handleRetry = () => {
  emit('retry', props.segment.index)
}

const handleShowError = () => {
  emit('showError', props.segment)
}

const toggleDetails = () => {
  detailsExpanded.value = !detailsExpanded.value
}
</script>

<style scoped>
.segment-status-item {
  @apply relative flex items-start p-3 border-b last:border-b-0 transition-all duration-200 hover:bg-gray-50;
}

.base-item {
  @apply bg-white;
}

.compact {
  @apply p-2;
}

.is-current {
  @apply bg-blue-50 border-l-4 border-l-blue-500;
}

.has-error {
  @apply bg-red-50;
}

.retrying {
  @apply bg-yellow-50;
}

.processing {
  @apply bg-blue-50;
}

.completed {
  @apply bg-green-50;
}

/* 狀態指示器 */
.status-indicator {
  @apply flex flex-col items-center mr-3 mt-0.5;
}

.status-icon {
  @apply text-lg leading-none;
}

.mini-progress {
  @apply w-6 h-1 bg-gray-200 rounded-full mt-1 overflow-hidden;
}

.mini-progress-fill {
  @apply h-full bg-blue-500 transition-all duration-300 animate-pulse;
}

/* 主要內容 */
.item-content {
  @apply flex-1 min-w-0;
}

.title-row {
  @apply flex justify-between items-center mb-1;
}

.segment-title {
  @apply text-sm font-medium text-gray-900 truncate;
}

.segment-index {
  @apply text-xs text-gray-500 mr-1;
}

.status-text {
  @apply text-xs font-medium px-2 py-1 rounded-full;
}

.text-pending {
  @apply text-gray-600 bg-gray-100;
}

.text-processing {
  @apply text-blue-600 bg-blue-100;
}

.text-completed {
  @apply text-green-600 bg-green-100;
}

.text-failed {
  @apply text-red-600 bg-red-100;
}

.text-retrying {
  @apply text-yellow-600 bg-yellow-100;
}

/* 詳細資訊 */
.details-section {
  @apply mt-2 space-y-1;
}

.detail-row {
  @apply flex justify-between text-xs text-gray-600;
}

.detail-label {
  @apply font-medium;
}

.detail-value {
  @apply text-gray-500;
}

/* 錯誤資訊 */
.error-section {
  @apply mt-2 p-2 bg-red-50 border border-red-200 rounded;
}

.error-message {
  @apply flex items-start text-xs text-red-700;
}

.error-icon {
  @apply mr-1 flex-shrink-0;
}

.error-text {
  @apply flex-1;
}

.retry-info {
  @apply flex justify-between items-center mt-1 text-xs text-red-600;
}

.retry-button {
  @apply px-2 py-1 bg-red-600 text-white rounded text-xs hover:bg-red-700 transition-colors;
}

/* 重試動畫 */
.retrying-animation {
  @apply flex items-center mt-2 text-xs text-yellow-600;
}

.retrying-dots {
  @apply flex space-x-1 mr-2;
}

.dot {
  @apply w-1 h-1 bg-yellow-500 rounded-full animate-bounce;
}

.dot:nth-child(2) {
  @apply animation-delay-75;
}

.dot:nth-child(3) {
  @apply animation-delay-150;
}

.retrying-text {
  @apply font-medium;
}

/* 操作按鈕 */
.action-buttons {
  @apply flex flex-col space-y-1 ml-2;
}

.action-btn {
  @apply text-xs p-1 rounded hover:bg-gray-100 transition-colors;
}

.action-btn.retry {
  @apply hover:bg-blue-100 text-blue-600;
}

.action-btn.error {
  @apply hover:bg-red-100 text-red-600;
}

.action-btn.info {
  @apply hover:bg-gray-100 text-gray-600;
}

/* 當前指示器 */
.current-indicator {
  @apply absolute -left-1 top-1/2 transform -translate-y-1/2;
}

.current-pulse {
  @apply w-2 h-2 bg-blue-500 rounded-full animate-ping;
}

/* 響應式設計 */
@media (max-width: 640px) {
  .segment-status-item {
    @apply flex-col space-y-2;
  }
  
  .status-indicator {
    @apply flex-row items-center mr-0 mb-2;
  }
  
  .mini-progress {
    @apply ml-2 w-12;
  }
  
  .title-row {
    @apply flex-col items-start space-y-1;
  }
  
  .action-buttons {
    @apply flex-row space-y-0 space-x-2 mt-2;
  }
}

/* 深色模式 */
@media (prefers-color-scheme: dark) {
  .base-item {
    @apply bg-gray-800 text-gray-200;
  }
  
  .segment-status-item:hover {
    @apply bg-gray-700;
  }
  
  .is-current {
    @apply bg-blue-900;
  }
  
  .has-error {
    @apply bg-red-900;
  }
  
  .processing {
    @apply bg-blue-900;
  }
  
  .completed {
    @apply bg-green-900;
  }
}

/* 動畫關鍵影格 */
@keyframes animation-delay-75 {
  0%, 20%, 50%, 80%, 100% {
    transform: translateY(0);
  }
  40% {
    transform: translateY(-6px);
  }
  60% {
    transform: translateY(-3px);
  }
}

@keyframes animation-delay-150 {
  0%, 40%, 50%, 80%, 100% {
    transform: translateY(0);
  }
  60% {
    transform: translateY(-6px);
  }
  80% {
    transform: translateY(-3px);
  }
}
</style>