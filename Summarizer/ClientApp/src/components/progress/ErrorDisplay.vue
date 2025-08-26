<!-- 錯誤顯示元件 - 統一的錯誤資訊顯示介面 -->
<template>
  <Transition
    name="error-display"
    :enter-active-class="enterTransition"
    :leave-active-class="leaveTransition"
    appear
  >
    <div 
      v-if="visible && error"
      :class="containerClasses"
      role="alert"
      :aria-live="ariaLive"
      :aria-atomic="true"
    >
      <!-- 錯誤圖示和關閉按鈕 -->
      <div class="error-header">
        <div class="error-icon-container" :class="iconContainerClasses">
          <component 
            :is="errorIcon" 
            class="error-icon"
            :class="iconClasses"
          />
        </div>
        
        <button
          v-if="showCloseButton"
          class="close-button"
          @click="handleClose"
          :title="closeButtonTitle"
          :aria-label="closeButtonAriaLabel"
        >
          <XMarkIcon class="w-4 h-4" />
        </button>
      </div>

      <!-- 錯誤內容 -->
      <div class="error-content">
        <!-- 主要錯誤訊息 -->
        <div class="error-message-section">
          <h4 v-if="error.title || computedTitle" class="error-title">
            {{ error.title || computedTitle }}
          </h4>
          
          <p class="error-message" :class="messageClasses">
            {{ displayMessage }}
          </p>
          
          <!-- 錯誤代碼和 ID -->
          <div v-if="showErrorCode && (error.errorCode || error.errorId)" class="error-metadata">
            <span v-if="error.errorCode" class="error-code">
              錯誤代碼: {{ error.errorCode }}
            </span>
            <span v-if="error.errorId" class="error-id">
              錯誤ID: {{ error.errorId }}
            </span>
          </div>
        </div>

        <!-- 技術詳情（可展開） -->
        <div v-if="showTechnicalDetails && technicalDetailsVisible" class="technical-details">
          <div class="technical-content">
            <div v-if="error.errorMessage && error.errorMessage !== displayMessage" class="technical-message">
              <span class="technical-label">技術詳情:</span>
              <span class="technical-value">{{ error.errorMessage }}</span>
            </div>
            
            <div v-if="error.errorContext && Object.keys(error.errorContext).length > 0" class="error-context">
              <span class="technical-label">錯誤上下文:</span>
              <div class="context-items">
                <div 
                  v-for="[key, value] in Object.entries(error.errorContext)" 
                  :key="key" 
                  class="context-item"
                >
                  <span class="context-key">{{ key }}:</span>
                  <span class="context-value">{{ formatContextValue(value) }}</span>
                </div>
              </div>
            </div>
            
            <div v-if="error.timestamp" class="error-timestamp">
              <span class="technical-label">發生時間:</span>
              <span class="technical-value">{{ formatTimestamp(error.timestamp) }}</span>
            </div>
          </div>
        </div>

        <!-- 建議解決方案 -->
        <div v-if="showSuggestions && error.suggestedActions?.length" class="suggestions-section">
          <h5 class="suggestions-title">建議解決方案:</h5>
          <ul class="suggestions-list">
            <li 
              v-for="(action, index) in error.suggestedActions" 
              :key="index" 
              class="suggestion-item"
            >
              <ChevronRightIcon class="suggestion-icon" />
              <span class="suggestion-text">{{ action }}</span>
            </li>
          </ul>
        </div>

        <!-- 操作按鈕區域 -->
        <div v-if="showActions" class="error-actions" :class="actionsClasses">
          <!-- 重試按鈕 -->
          <button
            v-if="showRetryButton && error.isRecoverable"
            class="action-button retry-button"
            :class="retryButtonClasses"
            @click="handleRetry"
            :disabled="isRetrying"
            :title="retryButtonTitle"
          >
            <ArrowPathIcon v-if="!isRetrying" class="w-4 h-4" />
            <div v-else class="loading-spinner"></div>
            {{ isRetrying ? '重試中...' : '重試' }}
          </button>

          <!-- 恢復按鈕 -->
          <button
            v-if="showRecoveryButton && error.isRecoverable"
            class="action-button recovery-button"
            :class="recoveryButtonClasses"
            @click="handleRecovery"
            :disabled="isRecovering"
            :title="recoveryButtonTitle"
          >
            <WrenchScrewdriverIcon v-if="!isRecovering" class="w-4 h-4" />
            <div v-else class="loading-spinner"></div>
            {{ isRecovering ? '恢復中...' : '系統恢復' }}
          </button>

          <!-- 技術詳情切換按鈕 -->
          <button
            v-if="showTechnicalDetails"
            class="action-button details-button"
            @click="toggleTechnicalDetails"
            :title="detailsToggleTitle"
          >
            <ChevronDownIcon 
              class="w-4 h-4 transition-transform"
              :class="{ 'rotate-180': technicalDetailsVisible }"
            />
            {{ technicalDetailsVisible ? '隱藏' : '顯示' }}詳情
          </button>

          <!-- 自訂操作按鈕 -->
          <slot name="actions" :error="error" :retry="handleRetry" :recover="handleRecovery" />
        </div>
      </div>

      <!-- 進度條（恢復/重試進度） -->
      <div 
        v-if="showProgress && (isRetrying || isRecovering) && progress > 0"
        class="error-progress"
      >
        <div class="progress-bar">
          <div 
            class="progress-fill" 
            :style="{ width: `${progress}%` }"
            :class="progressClasses"
          ></div>
        </div>
        <span class="progress-text">{{ progressText }}</span>
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { computed, ref, onMounted, watch } from 'vue'
import {
  XMarkIcon,
  ExclamationTriangleIcon,
  ExclamationCircleIcon,
  InformationCircleIcon,
  CheckCircleIcon,
  ChevronRightIcon,
  ChevronDownIcon,
  ArrowPathIcon,
  WrenchScrewdriverIcon
} from '@heroicons/vue/24/outline'

/**
 * 錯誤嚴重程度類型
 */
export type ErrorSeverity = 'info' | 'warning' | 'error' | 'critical' | 'fatal'

/**
 * 錯誤顯示變體類型
 */
export type ErrorDisplayVariant = 'inline' | 'toast' | 'banner' | 'modal' | 'card'

/**
 * 錯誤資料介面
 */
export interface ProcessingError {
  /** 錯誤 ID */
  errorId?: string
  /** 錯誤標題 */
  title?: string
  /** 使用者友善訊息 */
  userFriendlyMessage: string
  /** 技術錯誤訊息 */
  errorMessage?: string
  /** 錯誤代碼 */
  errorCode?: string
  /** 錯誤嚴重程度 */
  severity?: ErrorSeverity
  /** 是否可恢復 */
  isRecoverable?: boolean
  /** 建議解決方案 */
  suggestedActions?: string[]
  /** 錯誤上下文 */
  errorContext?: Record<string, any>
  /** 時間戳 */
  timestamp?: Date | string
  /** 重試次數 */
  retryCount?: number
}

interface Props {
  /** 錯誤資料 */
  error: ProcessingError | null
  /** 是否可見 */
  visible?: boolean
  /** 顯示變體 */
  variant?: ErrorDisplayVariant
  /** 是否顯示關閉按鈕 */
  showCloseButton?: boolean
  /** 是否顯示操作按鈕 */
  showActions?: boolean
  /** 是否顯示重試按鈕 */
  showRetryButton?: boolean
  /** 是否顯示恢復按鈕 */
  showRecoveryButton?: boolean
  /** 是否顯示建議解決方案 */
  showSuggestions?: boolean
  /** 是否顯示技術詳情 */
  showTechnicalDetails?: boolean
  /** 是否顯示錯誤代碼 */
  showErrorCode?: boolean
  /** 是否顯示進度條 */
  showProgress?: boolean
  /** 進度百分比 */
  progress?: number
  /** 進度文字 */
  progressText?: string
  /** 是否自動隱藏 */
  autoHide?: boolean
  /** 自動隱藏延遲時間（毫秒） */
  autoHideDelay?: number
  /** 是否啟用動畫 */
  enableAnimations?: boolean
  /** 最大重試次數 */
  maxRetries?: number
}

const props = withDefaults(defineProps<Props>(), {
  error: null,
  visible: true,
  variant: 'inline',
  showCloseButton: true,
  showActions: true,
  showRetryButton: true,
  showRecoveryButton: true,
  showSuggestions: true,
  showTechnicalDetails: false,
  showErrorCode: false,
  showProgress: true,
  progress: 0,
  progressText: '',
  autoHide: false,
  autoHideDelay: 5000,
  enableAnimations: true,
  maxRetries: 3
})

const emit = defineEmits<{
  /** 關閉事件 */
  close: []
  /** 重試事件 */
  retry: [error: ProcessingError]
  /** 恢復事件 */
  recover: [error: ProcessingError]
  /** 顯示技術詳情事件 */
  showDetails: [error: ProcessingError]
  /** 隱藏技術詳情事件 */
  hideDetails: [error: ProcessingError]
}>()

// 內部狀態
const technicalDetailsVisible = ref(false)
const isRetrying = ref(false)
const isRecovering = ref(false)
const autoHideTimer = ref<ReturnType<typeof setTimeout> | null>(null)

// 計算屬性
const computedTitle = computed(() => {
  if (!props.error?.severity) return '系統錯誤'
  
  const titles = {
    info: '系統訊息',
    warning: '警告訊息',
    error: '處理錯誤',
    critical: '嚴重錯誤',
    fatal: '系統故障'
  }
  
  return titles[props.error.severity] || '系統錯誤'
})

const displayMessage = computed(() => {
  return props.error?.userFriendlyMessage || '發生未知錯誤，請稍後再試或聯繫系統管理員'
})

const errorIcon = computed(() => {
  if (!props.error?.severity) return ExclamationCircleIcon
  
  const icons = {
    info: InformationCircleIcon,
    warning: ExclamationTriangleIcon,
    error: ExclamationCircleIcon,
    critical: ExclamationTriangleIcon,
    fatal: ExclamationTriangleIcon
  }
  
  return icons[props.error.severity] || ExclamationCircleIcon
})

const ariaLive = computed(() => {
  if (!props.error?.severity) return 'polite'
  return props.error.severity === 'critical' || props.error.severity === 'fatal' ? 'assertive' : 'polite'
})

// 樣式計算
const containerClasses = computed(() => [
  'error-display',
  `error-display--${props.variant}`,
  `error-display--${props.error?.severity || 'error'}`,
  {
    'error-display--animated': props.enableAnimations,
    'error-display--with-progress': props.showProgress && props.progress > 0
  }
])

const iconContainerClasses = computed(() => [
  'icon-container',
  {
    'text-blue-500': props.error?.severity === 'info',
    'text-yellow-500': props.error?.severity === 'warning',
    'text-red-500': ['error', 'critical', 'fatal'].includes(props.error?.severity || 'error')
  }
])

const iconClasses = computed(() => [
  'w-5 h-5',
  {
    'animate-bounce': props.error?.severity === 'critical' && props.enableAnimations,
    'animate-pulse': props.error?.severity === 'fatal' && props.enableAnimations
  }
])

const messageClasses = computed(() => [
  {
    'text-gray-700': props.error?.severity === 'info',
    'text-yellow-800': props.error?.severity === 'warning',
    'text-red-800': ['error', 'critical', 'fatal'].includes(props.error?.severity || 'error')
  }
])

const actionsClasses = computed(() => [
  'action-buttons',
  {
    'justify-center': props.variant === 'modal',
    'justify-start': props.variant !== 'modal'
  }
])

const retryButtonClasses = computed(() => [
  {
    'opacity-75 cursor-wait': isRetrying.value
  }
])

const recoveryButtonClasses = computed(() => [
  {
    'opacity-75 cursor-wait': isRecovering.value
  }
])

const progressClasses = computed(() => [
  {
    'bg-blue-500': isRecovering.value,
    'bg-yellow-500': isRetrying.value
  }
])

// 動畫類別
const enterTransition = computed(() => {
  if (!props.enableAnimations) return ''
  
  const transitions = {
    inline: 'animate-slide-down',
    toast: 'animate-slide-in-right',
    banner: 'animate-fade-in',
    modal: 'animate-zoom-in',
    card: 'animate-fade-in-up'
  }
  
  return transitions[props.variant] || 'animate-fade-in'
})

const leaveTransition = computed(() => {
  if (!props.enableAnimations) return ''
  
  const transitions = {
    inline: 'animate-slide-up',
    toast: 'animate-slide-out-right',
    banner: 'animate-fade-out',
    modal: 'animate-zoom-out',
    card: 'animate-fade-out-down'
  }
  
  return transitions[props.variant] || 'animate-fade-out'
})

// 工具提示和標籤
const closeButtonTitle = computed(() => '關閉錯誤訊息')
const closeButtonAriaLabel = computed(() => '關閉錯誤訊息')
const retryButtonTitle = computed(() => 
  props.error?.retryCount ? `重試 (已重試 ${props.error.retryCount} 次)` : '重試操作'
)
const recoveryButtonTitle = computed(() => '嘗試系統恢復')
const detailsToggleTitle = computed(() => 
  technicalDetailsVisible.value ? '隱藏技術詳情' : '顯示技術詳情'
)

// 方法
const handleClose = () => {
  clearAutoHideTimer()
  emit('close')
}

const handleRetry = async () => {
  if (isRetrying.value || !props.error) return
  
  // 檢查重試限制
  const retryCount = props.error.retryCount || 0
  if (retryCount >= props.maxRetries) {
    return
  }
  
  isRetrying.value = true
  clearAutoHideTimer()
  
  try {
    emit('retry', props.error)
  } finally {
    // 延遲重置狀態，讓使用者能看到反饋
    setTimeout(() => {
      isRetrying.value = false
    }, 1000)
  }
}

const handleRecovery = async () => {
  if (isRecovering.value || !props.error) return
  
  isRecovering.value = true
  clearAutoHideTimer()
  
  try {
    emit('recover', props.error)
  } finally {
    // 延遲重置狀態
    setTimeout(() => {
      isRecovering.value = false
    }, 2000)
  }
}

const toggleTechnicalDetails = () => {
  technicalDetailsVisible.value = !technicalDetailsVisible.value
  
  if (technicalDetailsVisible.value) {
    emit('showDetails', props.error!)
  } else {
    emit('hideDetails', props.error!)
  }
}

const formatContextValue = (value: any): string => {
  if (typeof value === 'object') {
    return JSON.stringify(value, null, 2)
  }
  return String(value)
}

const formatTimestamp = (timestamp: Date | string): string => {
  const date = timestamp instanceof Date ? timestamp : new Date(timestamp)
  return date.toLocaleString('zh-TW', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  })
}

const startAutoHideTimer = () => {
  if (!props.autoHide) return
  
  clearAutoHideTimer()
  autoHideTimer.value = setTimeout(() => {
    handleClose()
  }, props.autoHideDelay)
}

const clearAutoHideTimer = () => {
  if (autoHideTimer.value) {
    clearTimeout(autoHideTimer.value)
    autoHideTimer.value = null
  }
}

// 監聽器
watch(() => props.visible, (newVisible) => {
  if (newVisible && props.autoHide) {
    startAutoHideTimer()
  } else {
    clearAutoHideTimer()
  }
})

watch(() => props.error, (newError) => {
  if (newError && props.autoHide) {
    startAutoHideTimer()
  }
}, { immediate: true })

// 生命週期
onMounted(() => {
  if (props.visible && props.autoHide) {
    startAutoHideTimer()
  }
})
</script>

<style scoped>
/* 基礎容器樣式 */
.error-display {
  @apply relative rounded-lg border shadow-sm transition-all duration-300;
}

/* 變體樣式 */
.error-display--inline {
  @apply p-4 mb-4;
}

.error-display--toast {
  @apply fixed top-4 right-4 z-50 max-w-md p-4 shadow-lg;
}

.error-display--banner {
  @apply w-full p-3 border-l-4;
}

.error-display--modal {
  @apply p-6 max-w-lg mx-auto;
}

.error-display--card {
  @apply p-5 border rounded-xl shadow-md;
}

/* 嚴重程度樣式 */
.error-display--info {
  @apply bg-blue-50 border-blue-200 text-blue-900;
}

.error-display--warning {
  @apply bg-yellow-50 border-yellow-200 text-yellow-900;
}

.error-display--error,
.error-display--critical,
.error-display--fatal {
  @apply bg-red-50 border-red-200 text-red-900;
}

.error-display--banner.error-display--info {
  @apply border-l-blue-500;
}

.error-display--banner.error-display--warning {
  @apply border-l-yellow-500;
}

.error-display--banner.error-display--error,
.error-display--banner.error-display--critical,
.error-display--banner.error-display--fatal {
  @apply border-l-red-500;
}

/* 頭部樣式 */
.error-header {
  @apply flex items-start justify-between mb-3;
}

.icon-container {
  @apply flex-shrink-0 mr-3;
}

.close-button {
  @apply flex-shrink-0 p-1 rounded-full text-gray-400 
         hover:text-gray-600 hover:bg-gray-100 
         transition-colors duration-150;
}

/* 內容樣式 */
.error-content {
  @apply space-y-3;
}

.error-title {
  @apply text-lg font-semibold mb-1;
}

.error-message {
  @apply text-sm leading-relaxed;
}

.error-metadata {
  @apply flex flex-wrap gap-3 text-xs text-gray-600 mt-2;
}

.error-code,
.error-id {
  @apply bg-gray-100 px-2 py-1 rounded font-mono;
}

/* 技術詳情 */
.technical-details {
  @apply bg-gray-50 border rounded-lg p-3 text-xs;
}

.technical-content {
  @apply space-y-2;
}

.technical-message,
.error-timestamp {
  @apply flex flex-wrap gap-2;
}

.technical-label {
  @apply font-semibold text-gray-700 min-w-20;
}

.technical-value {
  @apply text-gray-600 font-mono break-all;
}

.error-context {
  @apply space-y-1;
}

.context-items {
  @apply space-y-1 ml-4;
}

.context-item {
  @apply flex gap-2;
}

.context-key {
  @apply font-medium text-gray-700 min-w-24;
}

.context-value {
  @apply text-gray-600 font-mono break-all;
}

/* 建議解決方案 */
.suggestions-section {
  @apply space-y-2;
}

.suggestions-title {
  @apply text-sm font-semibold text-gray-800;
}

.suggestions-list {
  @apply space-y-1;
}

.suggestion-item {
  @apply flex items-start gap-2 text-sm text-gray-700;
}

.suggestion-icon {
  @apply w-3 h-3 mt-0.5 text-gray-500 flex-shrink-0;
}

.suggestion-text {
  @apply leading-relaxed;
}

/* 操作按鈕 */
.error-actions {
  @apply flex flex-wrap gap-2 pt-2;
}

.action-button {
  @apply inline-flex items-center gap-2 px-3 py-1.5 text-xs font-medium 
         rounded-lg transition-all duration-150 
         focus:outline-none focus:ring-2 focus:ring-offset-1;
}

.retry-button {
  @apply bg-blue-500 text-white hover:bg-blue-600 
         focus:ring-blue-500 active:scale-95;
}

.recovery-button {
  @apply bg-green-500 text-white hover:bg-green-600 
         focus:ring-green-500 active:scale-95;
}

.details-button {
  @apply bg-gray-200 text-gray-700 hover:bg-gray-300 
         focus:ring-gray-500 active:scale-95;
}

.action-button:disabled {
  @apply opacity-75 cursor-not-allowed;
}

/* 載入中動畫 */
.loading-spinner {
  @apply w-3 h-3 border border-current border-t-transparent 
         rounded-full animate-spin;
}

/* 進度條 */
.error-progress {
  @apply mt-4 space-y-2;
}

.progress-bar {
  @apply w-full h-2 bg-gray-200 rounded-full overflow-hidden;
}

.progress-fill {
  @apply h-full transition-all duration-300 rounded-full;
}

.progress-text {
  @apply text-xs text-gray-600 text-center;
}

/* 動畫定義 */
@keyframes slide-down {
  from {
    opacity: 0;
    transform: translateY(-10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@keyframes slide-up {
  from {
    opacity: 1;
    transform: translateY(0);
  }
  to {
    opacity: 0;
    transform: translateY(-10px);
  }
}

@keyframes slide-in-right {
  from {
    opacity: 0;
    transform: translateX(100%);
  }
  to {
    opacity: 1;
    transform: translateX(0);
  }
}

@keyframes slide-out-right {
  from {
    opacity: 1;
    transform: translateX(0);
  }
  to {
    opacity: 0;
    transform: translateX(100%);
  }
}

@keyframes fade-in {
  from { opacity: 0; }
  to { opacity: 1; }
}

@keyframes fade-out {
  from { opacity: 1; }
  to { opacity: 0; }
}

@keyframes zoom-in {
  from {
    opacity: 0;
    transform: scale(0.95);
  }
  to {
    opacity: 1;
    transform: scale(1);
  }
}

@keyframes zoom-out {
  from {
    opacity: 1;
    transform: scale(1);
  }
  to {
    opacity: 0;
    transform: scale(0.95);
  }
}

@keyframes fade-in-up {
  from {
    opacity: 0;
    transform: translateY(20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@keyframes fade-out-down {
  from {
    opacity: 1;
    transform: translateY(0);
  }
  to {
    opacity: 0;
    transform: translateY(20px);
  }
}

.animate-slide-down {
  animation: slide-down 0.3s ease-out;
}

.animate-slide-up {
  animation: slide-up 0.3s ease-in;
}

.animate-slide-in-right {
  animation: slide-in-right 0.3s ease-out;
}

.animate-slide-out-right {
  animation: slide-out-right 0.3s ease-in;
}

.animate-fade-in {
  animation: fade-in 0.3s ease-out;
}

.animate-fade-out {
  animation: fade-out 0.3s ease-in;
}

.animate-zoom-in {
  animation: zoom-in 0.3s ease-out;
}

.animate-zoom-out {
  animation: zoom-out 0.3s ease-in;
}

.animate-fade-in-up {
  animation: fade-in-up 0.3s ease-out;
}

.animate-fade-out-down {
  animation: fade-out-down 0.3s ease-in;
}

/* 響應式設計 */
@media (max-width: 768px) {
  .error-display--toast {
    @apply left-4 right-4 max-w-none;
  }
  
  .error-actions {
    @apply flex-col;
  }
  
  .action-button {
    @apply w-full justify-center;
  }
}

/* 高對比度模式 */
@media (prefers-contrast: high) {
  .error-display {
    @apply border-2;
  }
  
  .action-button {
    @apply border-2 border-current;
  }
}

/* 減少動畫模式 */
@media (prefers-reduced-motion: reduce) {
  .error-display--animated,
  .loading-spinner,
  .progress-fill,
  .animate-slide-down,
  .animate-slide-up,
  .animate-slide-in-right,
  .animate-slide-out-right,
  .animate-fade-in,
  .animate-fade-out,
  .animate-zoom-in,
  .animate-zoom-out,
  .animate-fade-in-up,
  .animate-fade-out-down {
    @apply transition-none animate-none;
  }
  
  .icon-classes {
    @apply animate-none;
  }
}

/* 深色模式 */
@media (prefers-color-scheme: dark) {
  .error-display--info {
    @apply bg-blue-900 border-blue-700 text-blue-100;
  }
  
  .error-display--warning {
    @apply bg-yellow-900 border-yellow-700 text-yellow-100;
  }
  
  .error-display--error,
  .error-display--critical,
  .error-display--fatal {
    @apply bg-red-900 border-red-700 text-red-100;
  }
  
  .technical-details {
    @apply bg-gray-800 border-gray-600;
  }
  
  .technical-label {
    @apply text-gray-300;
  }
  
  .technical-value,
  .context-value {
    @apply text-gray-400;
  }
  
  .close-button {
    @apply text-gray-400 hover:text-gray-200 hover:bg-gray-700;
  }
  
  .details-button {
    @apply bg-gray-700 text-gray-200 hover:bg-gray-600;
  }
}
</style>