<!-- 錯誤恢復操作面板 - 提供系統恢復和重試功能 -->
<template>
  <Transition
    name="recovery-panel"
    :enter-active-class="'animate-fade-in-up'"
    :leave-active-class="'animate-fade-out-down'"
    appear
  >
    <div 
      v-if="visible"
      class="recovery-panel"
      :class="panelClasses"
      role="region"
      aria-label="錯誤恢復操作面板"
    >
      <!-- 面板標題 -->
      <div class="panel-header">
        <div class="header-content">
          <div class="header-icon" :class="headerIconClasses">
            <WrenchScrewdriverIcon class="w-5 h-5" />
          </div>
          <div class="header-text">
            <h3 class="panel-title">系統恢復與重試</h3>
            <p class="panel-subtitle">選擇適當的恢復策略以解決當前問題</p>
          </div>
        </div>
        
        <button
          v-if="showCloseButton"
          class="close-button"
          @click="handleClose"
          title="關閉恢復面板"
          aria-label="關閉恢復面板"
        >
          <XMarkIcon class="w-4 h-4" />
        </button>
      </div>

      <!-- 錯誤摘要 -->
      <div v-if="error && showErrorSummary" class="error-summary">
        <div class="summary-content">
          <div class="error-info">
            <span class="error-type">{{ getErrorTypeText(error.severity) }}</span>
            <span class="error-message">{{ error.userFriendlyMessage }}</span>
          </div>
          <div v-if="error.errorCode" class="error-code">
            錯誤代碼: {{ error.errorCode }}
          </div>
        </div>
      </div>

      <!-- 恢復選項 -->
      <div class="recovery-options">
        <h4 class="options-title">可用的恢復選項：</h4>
        
        <div class="options-grid">
          <!-- 快速重試 -->
          <div 
            class="recovery-option"
            :class="{ 'option-disabled': !canRetry, 'option-recommended': isRetryRecommended }"
          >
            <div class="option-header">
              <ArrowPathIcon class="option-icon" />
              <div class="option-info">
                <h5 class="option-title">快速重試</h5>
                <p class="option-description">立即重新執行失敗的操作</p>
              </div>
            </div>
            
            <div class="option-details">
              <div class="option-meta">
                <span class="retry-count">已重試: {{ retryCount }}/{{ maxRetries }} 次</span>
                <span v-if="estimatedRetryTime" class="estimated-time">
                  預估時間: {{ formatDuration(estimatedRetryTime) }}
                </span>
              </div>
              
              <div class="option-actions">
                <button
                  class="action-button retry-button"
                  :class="retryButtonClasses"
                  @click="handleRetry"
                  :disabled="!canRetry || isProcessing"
                  title="執行快速重試"
                >
                  <ArrowPathIcon v-if="!isRetrying" class="w-4 h-4" />
                  <div v-else class="loading-spinner"></div>
                  {{ isRetrying ? '重試中...' : '重試' }}
                </button>
              </div>
            </div>
          </div>

          <!-- 系統恢復 -->
          <div 
            class="recovery-option"
            :class="{ 'option-disabled': !canRecover, 'option-recommended': isRecoveryRecommended }"
          >
            <div class="option-header">
              <CogIcon class="option-icon" />
              <div class="option-info">
                <h5 class="option-title">系統恢復</h5>
                <p class="option-description">清理狀態並重置系統環境</p>
              </div>
            </div>
            
            <div class="option-details">
              <div class="option-meta">
                <span class="recovery-type">{{ getRecoveryTypeText() }}</span>
                <span v-if="estimatedRecoveryTime" class="estimated-time">
                  預估時間: {{ formatDuration(estimatedRecoveryTime) }}
                </span>
              </div>
              
              <div class="recovery-steps" v-if="showRecoverySteps">
                <p class="steps-title">恢復步驟：</p>
                <ul class="steps-list">
                  <li 
                    v-for="(step, index) in recoverySteps" 
                    :key="index"
                    class="step-item"
                    :class="getStepClasses(index)"
                  >
                    <CheckIcon v-if="step.completed" class="step-icon completed" />
                    <div v-else-if="step.inProgress" class="step-icon in-progress">
                      <div class="spinner-mini"></div>
                    </div>
                    <div v-else class="step-icon pending"></div>
                    <span class="step-text">{{ step.description }}</span>
                  </li>
                </ul>
              </div>
              
              <div class="option-actions">
                <button
                  class="action-button recovery-button"
                  :class="recoveryButtonClasses"
                  @click="handleRecovery"
                  :disabled="!canRecover || isProcessing"
                  title="執行系統恢復"
                >
                  <CogIcon v-if="!isRecovering" class="w-4 h-4" />
                  <div v-else class="loading-spinner"></div>
                  {{ isRecovering ? '恢復中...' : '系統恢復' }}
                </button>
              </div>
            </div>
          </div>

          <!-- 手動介入 -->
          <div class="recovery-option manual-option">
            <div class="option-header">
              <UserIcon class="option-icon" />
              <div class="option-info">
                <h5 class="option-title">手動介入</h5>
                <p class="option-description">需要人工檢查和處理</p>
              </div>
            </div>
            
            <div class="option-details">
              <div class="manual-actions">
                <button
                  class="action-button manual-button"
                  @click="handleManualIntervention"
                  :disabled="isProcessing"
                  title="聯繫系統管理員"
                >
                  <PhoneIcon class="w-4 h-4" />
                  聯繫管理員
                </button>
                
                <button
                  class="action-button log-button"
                  @click="handleDownloadLog"
                  :disabled="isProcessing"
                  title="下載錯誤日誌"
                >
                  <DocumentArrowDownIcon class="w-4 h-4" />
                  下載日誌
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- 進度顯示 -->
      <div v-if="showProgress && isProcessing" class="progress-section">
        <div class="progress-header">
          <span class="progress-title">{{ currentOperationText }}</span>
          <span class="progress-percentage">{{ Math.round(progress) }}%</span>
        </div>
        
        <div class="progress-bar">
          <div 
            class="progress-fill"
            :style="{ width: `${progress}%` }"
            :class="progressClasses"
          ></div>
        </div>
        
        <div class="progress-details">
          <span class="current-step">{{ currentStepText }}</span>
          <span v-if="remainingTime" class="remaining-time">
            剩餘時間: {{ formatDuration(remainingTime) }}
          </span>
        </div>
      </div>

      <!-- 成功/錯誤反饋 -->
      <Transition name="feedback" appear>
        <div v-if="feedback" class="feedback-section" :class="feedbackClasses">
          <div class="feedback-content">
            <component 
              :is="feedbackIcon" 
              class="feedback-icon"
              :class="feedbackIconClasses"
            />
            <div class="feedback-text">
              <h5 class="feedback-title">{{ feedback.title }}</h5>
              <p class="feedback-message">{{ feedback.message }}</p>
            </div>
          </div>
          
          <div v-if="feedback.actions?.length" class="feedback-actions">
            <button
              v-for="action in feedback.actions"
              :key="action.key"
              class="feedback-button"
              :class="action.variant || 'primary'"
              @click="handleFeedbackAction(action)"
            >
              {{ action.text }}
            </button>
          </div>
        </div>
      </Transition>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { computed, ref, watch, onMounted } from 'vue'
import {
  XMarkIcon,
  WrenchScrewdriverIcon,
  ArrowPathIcon,
  CogIcon,
  UserIcon,
  PhoneIcon,
  DocumentArrowDownIcon,
  CheckIcon,
  CheckCircleIcon,
  XCircleIcon,
  InformationCircleIcon
} from '@heroicons/vue/24/outline'

import type { ProcessingError, ErrorSeverity } from './ErrorDisplay.vue'

/**
 * 恢復步驟介面
 */
interface RecoveryStep {
  description: string
  completed: boolean
  inProgress: boolean
  estimatedDurationMs?: number
}

/**
 * 反饋訊息介面
 */
interface FeedbackMessage {
  type: 'success' | 'error' | 'info' | 'warning'
  title: string
  message: string
  actions?: Array<{
    key: string
    text: string
    variant?: string
    handler?: () => void
  }>
}

interface Props {
  /** 是否顯示面板 */
  visible?: boolean
  /** 錯誤資訊 */
  error?: ProcessingError | null
  /** 最大重試次數 */
  maxRetries?: number
  /** 當前重試次數 */
  retryCount?: number
  /** 是否可以重試 */
  canRetry?: boolean
  /** 是否可以恢復 */
  canRecover?: boolean
  /** 是否推薦重試 */
  isRetryRecommended?: boolean
  /** 是否推薦恢復 */
  isRecoveryRecommended?: boolean
  /** 預估重試時間（毫秒） */
  estimatedRetryTime?: number
  /** 預估恢復時間（毫秒） */
  estimatedRecoveryTime?: number
  /** 是否顯示關閉按鈕 */
  showCloseButton?: boolean
  /** 是否顯示錯誤摘要 */
  showErrorSummary?: boolean
  /** 是否顯示恢復步驟 */
  showRecoverySteps?: boolean
  /** 是否顯示進度 */
  showProgress?: boolean
  /** 當前進度（0-100） */
  progress?: number
  /** 剩餘時間（毫秒） */
  remainingTime?: number
  /** 當前步驟文字 */
  currentStepText?: string
  /** 面板變體 */
  variant?: 'default' | 'compact' | 'detailed'
}

const props = withDefaults(defineProps<Props>(), {
  visible: true,
  error: null,
  maxRetries: 3,
  retryCount: 0,
  canRetry: true,
  canRecover: true,
  isRetryRecommended: false,
  isRecoveryRecommended: false,
  estimatedRetryTime: 0,
  estimatedRecoveryTime: 0,
  showCloseButton: true,
  showErrorSummary: true,
  showRecoverySteps: false,
  showProgress: true,
  progress: 0,
  remainingTime: 0,
  currentStepText: '',
  variant: 'default'
})

const emit = defineEmits<{
  /** 關閉面板 */
  close: []
  /** 執行重試 */
  retry: [error: ProcessingError | null]
  /** 執行系統恢復 */
  recover: [error: ProcessingError | null]
  /** 手動介入 */
  manualIntervention: [error: ProcessingError | null]
  /** 下載日誌 */
  downloadLog: [error: ProcessingError | null]
  /** 操作開始 */
  operationStart: [operation: 'retry' | 'recover']
  /** 操作完成 */
  operationComplete: [operation: 'retry' | 'recover', success: boolean]
}>()

// 內部狀態
const isRetrying = ref(false)
const isRecovering = ref(false)
const feedback = ref<FeedbackMessage | null>(null)
const recoverySteps = ref<RecoveryStep[]>([
  { description: '檢查系統狀態', completed: false, inProgress: false },
  { description: '清理暫存資料', completed: false, inProgress: false },
  { description: '重置連線狀態', completed: false, inProgress: false },
  { description: '恢復系統配置', completed: false, inProgress: false },
  { description: '驗證系統健康', completed: false, inProgress: false }
])

// 計算屬性
const isProcessing = computed(() => isRetrying.value || isRecovering.value)

const currentOperationText = computed(() => {
  if (isRetrying.value) return '正在重試操作'
  if (isRecovering.value) return '正在執行系統恢復'
  return ''
})

const panelClasses = computed(() => [
  'recovery-panel-base',
  `recovery-panel--${props.variant}`,
  {
    'recovery-panel--processing': isProcessing.value
  }
])

const headerIconClasses = computed(() => [
  'header-icon-base',
  {
    'text-blue-500': !isProcessing.value,
    'text-orange-500 animate-spin': isProcessing.value
  }
])

const retryButtonClasses = computed(() => [
  {
    'cursor-wait opacity-75': isRetrying.value,
    'bg-blue-500 hover:bg-blue-600': props.isRetryRecommended,
    'bg-gray-500 hover:bg-gray-600': !props.isRetryRecommended
  }
])

const recoveryButtonClasses = computed(() => [
  {
    'cursor-wait opacity-75': isRecovering.value,
    'bg-green-500 hover:bg-green-600': props.isRecoveryRecommended,
    'bg-orange-500 hover:bg-orange-600': !props.isRecoveryRecommended
  }
])

const progressClasses = computed(() => [
  {
    'bg-blue-500': isRetrying.value,
    'bg-green-500': isRecovering.value
  }
])

const feedbackClasses = computed(() => {
  if (!feedback.value) return []
  
  const typeClasses = {
    success: 'bg-green-50 border-green-200 text-green-900',
    error: 'bg-red-50 border-red-200 text-red-900',
    warning: 'bg-yellow-50 border-yellow-200 text-yellow-900',
    info: 'bg-blue-50 border-blue-200 text-blue-900'
  }
  
  return [typeClasses[feedback.value.type] || typeClasses.info]
})

const feedbackIcon = computed(() => {
  if (!feedback.value) return InformationCircleIcon
  
  const icons = {
    success: CheckCircleIcon,
    error: XCircleIcon,
    warning: XCircleIcon,
    info: InformationCircleIcon
  }
  
  return icons[feedback.value.type] || InformationCircleIcon
})

const feedbackIconClasses = computed(() => {
  if (!feedback.value) return ['text-blue-500']
  
  const classes = {
    success: ['text-green-500'],
    error: ['text-red-500'],
    warning: ['text-yellow-500'],
    info: ['text-blue-500']
  }
  
  return classes[feedback.value.type] || classes.info
})

// 方法
const handleClose = () => {
  emit('close')
}

const handleRetry = async () => {
  if (isProcessing.value || !props.canRetry) return
  
  isRetrying.value = true
  feedback.value = null
  
  try {
    emit('operationStart', 'retry')
    emit('retry', props.error)
    
    // 模擬重試過程
    await simulateOperation('retry')
    
    feedback.value = {
      type: 'success',
      title: '重試成功',
      message: '操作已成功重試，系統正在恢復正常運作',
      actions: [
        { key: 'continue', text: '繼續', variant: 'success' }
      ]
    }
    
    emit('operationComplete', 'retry', true)
  } catch (error) {
    feedback.value = {
      type: 'error',
      title: '重試失敗',
      message: '重試操作失敗，請嘗試系統恢復或聯繫管理員',
      actions: [
        { key: 'recover', text: '系統恢復', variant: 'warning' },
        { key: 'contact', text: '聯繫管理員', variant: 'secondary' }
      ]
    }
    
    emit('operationComplete', 'retry', false)
  } finally {
    isRetrying.value = false
  }
}

const handleRecovery = async () => {
  if (isProcessing.value || !props.canRecover) return
  
  isRecovering.value = true
  feedback.value = null
  
  // 重置恢復步驟
  recoverySteps.value.forEach(step => {
    step.completed = false
    step.inProgress = false
  })
  
  try {
    emit('operationStart', 'recover')
    emit('recover', props.error)
    
    // 執行恢復步驟
    await simulateRecoverySteps()
    
    feedback.value = {
      type: 'success',
      title: '系統恢復完成',
      message: '系統已成功恢復，所有組件運作正常',
      actions: [
        { key: 'continue', text: '繼續', variant: 'success' }
      ]
    }
    
    emit('operationComplete', 'recover', true)
  } catch (error) {
    feedback.value = {
      type: 'error',
      title: '系統恢復失敗',
      message: '自動恢復失敗，需要手動介入處理',
      actions: [
        { key: 'manual', text: '手動介入', variant: 'warning' },
        { key: 'contact', text: '聯繫管理員', variant: 'secondary' }
      ]
    }
    
    emit('operationComplete', 'recover', false)
  } finally {
    isRecovering.value = false
  }
}

const handleManualIntervention = () => {
  emit('manualIntervention', props.error)
}

const handleDownloadLog = () => {
  emit('downloadLog', props.error)
}

const handleFeedbackAction = (action: any) => {
  switch (action.key) {
    case 'continue':
      handleClose()
      break
    case 'recover':
      handleRecovery()
      break
    case 'manual':
      handleManualIntervention()
      break
    case 'contact':
      handleManualIntervention()
      break
  }
  
  if (action.handler) {
    action.handler()
  }
}

const simulateOperation = async (operation: 'retry' | 'recover'): Promise<void> => {
  const duration = operation === 'retry' ? 3000 : 5000
  const steps = 10
  const stepDuration = duration / steps
  
  for (let i = 0; i <= steps; i++) {
    await new Promise(resolve => setTimeout(resolve, stepDuration))
    // 這裡實際上會觸發真實的操作進度更新
  }
}

const simulateRecoverySteps = async (): Promise<void> => {
  for (let i = 0; i < recoverySteps.value.length; i++) {
    const step = recoverySteps.value[i]
    step.inProgress = true
    
    // 模擬步驟執行時間
    await new Promise(resolve => setTimeout(resolve, 1000))
    
    step.inProgress = false
    step.completed = true
  }
}

const getErrorTypeText = (severity?: ErrorSeverity): string => {
  const texts = {
    info: '系統訊息',
    warning: '警告',
    error: '錯誤',
    critical: '嚴重錯誤',
    fatal: '系統故障'
  }
  
  return texts[severity || 'error']
}

const getRecoveryTypeText = (): string => {
  const severity = props.error?.severity
  
  if (!severity) return '標準恢復'
  
  const types = {
    info: '輕量恢復',
    warning: '標準恢復',
    error: '完整恢復',
    critical: '深度恢復',
    fatal: '完全重建'
  }
  
  return types[severity] || '標準恢復'
}

const getStepClasses = (index: number) => {
  const step = recoverySteps.value[index]
  
  return [
    'step-base',
    {
      'step-completed': step.completed,
      'step-in-progress': step.inProgress,
      'step-pending': !step.completed && !step.inProgress
    }
  ]
}

const formatDuration = (ms: number): string => {
  if (ms < 1000) return '< 1秒'
  if (ms < 60000) return `${Math.round(ms / 1000)}秒`
  if (ms < 3600000) return `${Math.round(ms / 60000)}分鐘`
  return `${Math.round(ms / 3600000)}小時`
}

// 監聽器
watch(() => props.visible, (visible) => {
  if (visible) {
    feedback.value = null
  }
})
</script>

<style scoped>
/* 基礎面板樣式 */
.recovery-panel-base {
  @apply bg-white border border-gray-200 rounded-xl shadow-lg p-6 space-y-6;
}

.recovery-panel--compact {
  @apply p-4 space-y-4;
}

.recovery-panel--detailed {
  @apply p-8 space-y-8;
}

.recovery-panel--processing {
  @apply border-blue-300 shadow-blue-100;
}

/* 面板頭部 */
.panel-header {
  @apply flex items-start justify-between;
}

.header-content {
  @apply flex items-start gap-3;
}

.header-icon-base {
  @apply flex-shrink-0 p-2 bg-gray-100 rounded-lg;
}

.header-text {
  @apply space-y-1;
}

.panel-title {
  @apply text-lg font-semibold text-gray-900;
}

.panel-subtitle {
  @apply text-sm text-gray-600;
}

.close-button {
  @apply p-1 rounded-full text-gray-400 hover:text-gray-600 
         hover:bg-gray-100 transition-colors;
}

/* 錯誤摘要 */
.error-summary {
  @apply bg-gray-50 border border-gray-200 rounded-lg p-4;
}

.summary-content {
  @apply space-y-2;
}

.error-info {
  @apply flex flex-wrap items-center gap-2;
}

.error-type {
  @apply text-xs font-medium bg-red-100 text-red-800 
         px-2 py-1 rounded-full;
}

.error-message {
  @apply text-sm text-gray-700 flex-1 min-w-0;
}

.error-code {
  @apply text-xs text-gray-500 font-mono bg-gray-100 px-2 py-1 rounded;
}

/* 恢復選項 */
.recovery-options {
  @apply space-y-4;
}

.options-title {
  @apply text-base font-medium text-gray-900;
}

.options-grid {
  @apply space-y-4;
}

.recovery-option {
  @apply border border-gray-200 rounded-lg p-4 transition-all duration-200;
}

.recovery-option:hover {
  @apply border-gray-300 shadow-sm;
}

.option-recommended {
  @apply border-blue-300 bg-blue-50;
}

.option-disabled {
  @apply opacity-60 cursor-not-allowed;
}

.option-header {
  @apply flex items-start gap-3 mb-3;
}

.option-icon {
  @apply w-5 h-5 text-gray-600 flex-shrink-0 mt-0.5;
}

.option-info {
  @apply flex-1 min-w-0;
}

.option-title {
  @apply text-sm font-semibold text-gray-900;
}

.option-description {
  @apply text-xs text-gray-600 mt-1;
}

.option-details {
  @apply space-y-3;
}

.option-meta {
  @apply flex flex-wrap items-center gap-3 text-xs text-gray-600;
}

.retry-count,
.recovery-type {
  @apply font-medium;
}

.estimated-time {
  @apply text-gray-500;
}

/* 恢復步驟 */
.recovery-steps {
  @apply space-y-2;
}

.steps-title {
  @apply text-xs font-medium text-gray-700;
}

.steps-list {
  @apply space-y-1;
}

.step-item {
  @apply flex items-center gap-2 text-xs;
}

.step-icon {
  @apply w-3 h-3 flex-shrink-0;
}

.step-icon.completed {
  @apply text-green-500;
}

.step-icon.in-progress {
  @apply flex items-center justify-center;
}

.step-icon.pending {
  @apply bg-gray-300 rounded-full;
}

.spinner-mini {
  @apply w-3 h-3 border border-blue-500 border-t-transparent 
         rounded-full animate-spin;
}

.step-text {
  @apply text-gray-700;
}

.step-completed .step-text {
  @apply text-green-700;
}

.step-in-progress .step-text {
  @apply text-blue-700 font-medium;
}

/* 操作按鈕 */
.option-actions {
  @apply pt-2 border-t border-gray-100;
}

.action-button {
  @apply inline-flex items-center gap-2 px-4 py-2 text-sm font-medium 
         rounded-lg transition-all duration-150 
         focus:outline-none focus:ring-2 focus:ring-offset-1 
         active:scale-95;
}

.retry-button {
  @apply text-white focus:ring-blue-500;
}

.recovery-button {
  @apply text-white focus:ring-green-500;
}

.manual-button {
  @apply bg-orange-500 text-white hover:bg-orange-600 focus:ring-orange-500;
}

.log-button {
  @apply bg-gray-500 text-white hover:bg-gray-600 focus:ring-gray-500;
}

.action-button:disabled {
  @apply opacity-60 cursor-not-allowed;
}

.manual-actions {
  @apply flex flex-wrap gap-2;
}

/* 載入動畫 */
.loading-spinner {
  @apply w-4 h-4 border-2 border-current border-t-transparent 
         rounded-full animate-spin;
}

/* 進度區域 */
.progress-section {
  @apply space-y-3 p-4 bg-gray-50 rounded-lg border;
}

.progress-header {
  @apply flex justify-between items-center;
}

.progress-title {
  @apply text-sm font-medium text-gray-900;
}

.progress-percentage {
  @apply text-sm font-semibold text-gray-700;
}

.progress-bar {
  @apply w-full h-2 bg-gray-200 rounded-full overflow-hidden;
}

.progress-fill {
  @apply h-full transition-all duration-300 rounded-full;
}

.progress-details {
  @apply flex justify-between items-center text-xs text-gray-600;
}

/* 反饋區域 */
.feedback-section {
  @apply border rounded-lg p-4 space-y-3;
}

.feedback-content {
  @apply flex items-start gap-3;
}

.feedback-icon {
  @apply w-5 h-5 flex-shrink-0;
}

.feedback-text {
  @apply flex-1 min-w-0;
}

.feedback-title {
  @apply text-sm font-semibold;
}

.feedback-message {
  @apply text-sm mt-1;
}

.feedback-actions {
  @apply flex flex-wrap gap-2;
}

.feedback-button {
  @apply px-3 py-1 text-xs font-medium rounded-lg transition-colors;
}

.feedback-button.success {
  @apply bg-green-500 text-white hover:bg-green-600;
}

.feedback-button.warning {
  @apply bg-yellow-500 text-white hover:bg-yellow-600;
}

.feedback-button.secondary {
  @apply bg-gray-500 text-white hover:bg-gray-600;
}

/* 動畫定義 */
.animate-fade-in-up {
  animation: fadeInUp 0.3s ease-out;
}

.animate-fade-out-down {
  animation: fadeOutDown 0.3s ease-in;
}

@keyframes fadeInUp {
  from {
    opacity: 0;
    transform: translateY(20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@keyframes fadeOutDown {
  from {
    opacity: 1;
    transform: translateY(0);
  }
  to {
    opacity: 0;
    transform: translateY(20px);
  }
}

/* 反饋動畫 */
.feedback-enter-active,
.feedback-leave-active {
  transition: all 0.3s ease;
}

.feedback-enter-from {
  opacity: 0;
  transform: translateY(-10px);
}

.feedback-leave-to {
  opacity: 0;
  transform: translateY(-10px);
}

/* 響應式設計 */
@media (max-width: 768px) {
  .recovery-panel-base {
    @apply p-4 space-y-4;
  }
  
  .options-grid {
    @apply space-y-3;
  }
  
  .recovery-option {
    @apply p-3;
  }
  
  .manual-actions,
  .feedback-actions {
    @apply flex-col;
  }
  
  .action-button,
  .feedback-button {
    @apply w-full justify-center;
  }
}

/* 高對比度模式 */
@media (prefers-contrast: high) {
  .recovery-panel-base {
    @apply border-2;
  }
  
  .recovery-option {
    @apply border-2;
  }
  
  .action-button {
    @apply border-2 border-current;
  }
}

/* 減少動畫模式 */
@media (prefers-reduced-motion: reduce) {
  .animate-fade-in-up,
  .animate-fade-out-down,
  .feedback-enter-active,
  .feedback-leave-active,
  .loading-spinner,
  .spinner-mini,
  .progress-fill {
    @apply transition-none animate-none;
  }
  
  .header-icon-base {
    @apply animate-none;
  }
}

/* 深色模式 */
@media (prefers-color-scheme: dark) {
  .recovery-panel-base {
    @apply bg-gray-800 border-gray-700;
  }
  
  .panel-title {
    @apply text-gray-100;
  }
  
  .panel-subtitle,
  .option-description {
    @apply text-gray-400;
  }
  
  .error-summary {
    @apply bg-gray-700 border-gray-600;
  }
  
  .recovery-option {
    @apply border-gray-600;
  }
  
  .recovery-option:hover {
    @apply border-gray-500;
  }
  
  .option-recommended {
    @apply border-blue-500 bg-blue-900;
  }
  
  .progress-section {
    @apply bg-gray-700 border-gray-600;
  }
}
</style>