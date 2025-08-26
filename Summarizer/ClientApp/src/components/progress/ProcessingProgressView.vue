<!-- 處理進度主檢視元件 - 整合所有進度相關功能 -->
<template>
  <div class="processing-progress-view" :class="viewClasses">
    <!-- 載入狀態遮罩 -->
    <div v-if="isLoading" class="loading-overlay">
      <div class="loading-content">
        <div class="loading-spinner"></div>
        <p class="loading-text">{{ loadingText }}</p>
      </div>
    </div>

    <!-- 錯誤狀態顯示 -->
    <div v-if="hasError && !isLoading" class="error-state">
      <div class="error-icon">⚠️</div>
      <h3 class="error-title">無法載入進度資訊</h3>
      <p class="error-message">{{ errorMessage }}</p>
      <div class="error-actions">
        <button class="retry-btn" @click="retry">重試</button>
        <button class="close-btn" @click="closeProgressView">關閉</button>
      </div>
    </div>

    <!-- 主要進度內容 -->
    <div v-if="!isLoading && !hasError" class="progress-content">
      <!-- 頭部：整體進度和控制 -->
      <div class="progress-header">
        <div class="header-left">
          <h2 class="progress-title">{{ progressTitle }}</h2>
          <p class="progress-subtitle">{{ progressSubtitle }}</p>
        </div>
        
        <div class="header-controls">
          <!-- 檢視模式切換 -->
          <div class="view-mode-toggle">
            <button
              v-for="mode in availableViewModes"
              :key="mode.value"
              class="mode-btn"
              :class="{ 'active': viewMode === mode.value }"
              @click="setViewMode(mode.value as 'compact' | 'normal' | 'detailed')"
              :title="mode.description"
            >
              {{ mode.icon }} {{ mode.label }}
            </button>
          </div>

          <!-- 其他控制按鈕 -->
          <button
            class="control-btn"
            @click="toggleAutoRefresh"
            :class="{ 'active': autoRefreshEnabled }"
            title="自動刷新"
          >
            🔄
          </button>
          
          <button
            class="control-btn"
            @click="toggleFullscreen"
            title="全螢幕模式"
          >
            {{ isFullscreen ? '🗗' : '🗖' }}
          </button>
          
          <button
            class="control-btn close-btn"
            @click="closeProgressView"
            title="關閉進度檢視"
          >
            ✕
          </button>
        </div>
      </div>

      <!-- 階段指示器 -->
      <div class="stage-section">
        <ProcessingStageIndicator
          :current-stage="progress.currentStage"
          :stage-progress="progress.stageProgress"
          :overall-progress="progress.overallProgress"
          :show-description="viewMode !== 'compact'"
          :show-timing="showDetailedTiming"
          :show-current-stage-details="viewMode === 'detailed'"
          :compact="viewMode === 'compact'"
          :orientation="stageIndicatorOrientation"
          @stage-click="handleStageClick"
        />
      </div>

      <!-- 主要進度區域 -->
      <div class="main-progress-section" :class="progressSectionClasses">
        <!-- 左側：進度條和統計 -->
        <div class="progress-left-panel">
          <!-- 整體進度條 -->
          <div class="overall-progress">
            <ProgressBar
              :progress="progress.overallProgress"
              :title="progressBarTitle"
              :variant="progressBarVariant"
              :size="progressBarSize"
              :show-animation="true"
              :show-details="viewMode !== 'compact'"
              :current-stage="progress.currentStage"
              :estimated-time="progress.estimatedRemainingTimeMs"
              :stage-markers="stageMarkers"
              :show-stage-markers="viewMode === 'detailed'"
            />
          </div>

          <!-- 時間預估面板 -->
          <div v-if="showTimeEstimation" class="time-estimation">
            <TimeEstimationPanel
              :elapsed-time-ms="progress.elapsedTimeMs"
              :estimated-remaining-time-ms="progress.estimatedRemainingTimeMs"
              :processing-speed="progress.processingSpeed"
              :estimated-completion-time="progress.estimatedCompletionTime"
              :show-historical-comparison="showHistoricalComparison"
              :show-detailed-stats="viewMode === 'detailed'"
              :historical-data="historicalData"
              :completed-segments="progress.completedSegments"
              :total-segments="progress.totalSegments"
              :current-stage="progress.currentStage"
            />
          </div>
        </div>

        <!-- 右側：分段狀態 -->
        <div v-if="showSegmentList" class="progress-right-panel">
          <SegmentStatusList
            :segments="segmentStatuses"
            :current-segment="progress.currentSegment"
            :show-details="viewMode === 'detailed'"
            :compact="viewMode === 'compact'"
            :max-visible-items="maxVisibleSegments"
            :use-virtual-scrolling="useVirtualScrolling"
            :show-load-more="segmentStatuses.length > maxVisibleSegments"
            @retry="handleSegmentRetry"
            @show-error="handleShowSegmentError"
            @load-more="handleLoadMoreSegments"
            ref="segmentListRef"
          />
        </div>
      </div>

      <!-- 底部：即時資訊和操作 -->
      <div v-if="showBottomPanel" class="bottom-panel">
        <!-- 連線狀態指示器 -->
        <div class="connection-status" :class="connectionStatusClasses">
          <div class="status-indicator"></div>
          <span class="status-text">{{ connectionStatusText }}</span>
          <span v-if="lastUpdateTime" class="last-update">
            最後更新: {{ formatRelativeTime(lastUpdateTime) }}
          </span>
        </div>

        <!-- 快速操作按鈕 */
        <div class="quick-actions">
          <button
            v-if="canPauseProgress"
            class="action-btn pause-btn"
            @click="pauseProgress"
            :disabled="progress.currentStage === 'completed' || progress.currentStage === 'failed'"
          >
            {{ isPaused ? '▶️ 繼續' : '⏸️ 暫停' }}
          </button>

          <button
            v-if="canCancelProgress"
            class="action-btn cancel-btn"
            @click="confirmCancelProgress"
            :disabled="progress.currentStage === 'completed' || progress.currentStage === 'failed'"
          >
            ❌ 取消
          </button>

          <button
            class="action-btn download-btn"
            @click="downloadProgressReport"
            :disabled="progress.overallProgress < 100"
          >
            📊 下載報告
          </button>
        </div>
      </div>
    </div>

    <!-- 分段錯誤詳情彈窗 -->
    <div v-if="selectedSegmentError" class="modal-overlay" @click="closeSegmentErrorModal">
      <div class="modal-content" @click.stop>
        <div class="modal-header">
          <h3 class="modal-title">分段處理錯誤詳情</h3>
          <button class="modal-close" @click="closeSegmentErrorModal">✕</button>
        </div>
        
        <div class="modal-body">
          <div class="error-info">
            <div class="info-row">
              <span class="info-label">分段編號:</span>
              <span class="info-value">#{{ selectedSegmentError.index + 1 }}</span>
            </div>
            <div class="info-row">
              <span class="info-label">分段標題:</span>
              <span class="info-value">{{ selectedSegmentError.title }}</span>
            </div>
            <div class="info-row">
              <span class="info-label">錯誤訊息:</span>
              <span class="info-value error-text">{{ selectedSegmentError.errorMessage }}</span>
            </div>
            <div class="info-row">
              <span class="info-label">重試次數:</span>
              <span class="info-value">{{ selectedSegmentError.retryCount }}</span>
            </div>
          </div>
        </div>
        
        <div class="modal-footer">
          <button class="retry-segment-btn" @click="handleSegmentRetry(selectedSegmentError.index)">
            重試此分段
          </button>
          <button class="close-modal-btn" @click="closeSegmentErrorModal">
            關閉
          </button>
        </div>
      </div>
    </div>

    <!-- 取消確認對話框 -->
    <CancelConfirmationDialog
      :is-visible="showCancelDialog"
      :completed-segments="progress.completedSegments || 0"
      :total-segments="progress.totalSegments || 0"
      :allow-partial-result-saving="true"
      :default-save-partial-results="false"
      @cancel="handleCancelDialogConfirm"
      @close="handleCancelDialogClose"
    />

    <!-- 部分結果預覽對話框 -->
    <PartialResultPreviewDialog
      :is-visible="showPartialResultDialog"
      :partial-result="currentPartialResult"
      @save="handlePartialResultSave"
      @discard="handlePartialResultDiscard"
      @continue="handlePartialResultContinue"
      @close="handlePartialResultDialogClose"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted, watch, nextTick } from 'vue'
import { useSignalR, ConnectionState } from '@/composables/useSignalR'
import { useProgressThrottling } from '@/composables/useProgressThrottling'
import type { 
  ProcessingProgress, 
  SegmentStatus, 
  ProcessingStage,
  StageDefinition,
  PartialResult 
} from '@/types/progress'
import { DEFAULT_STAGE_DEFINITIONS } from '@/types/progress'

import ProcessingStageIndicator from './ProcessingStageIndicator.vue'
import ProgressBar from './ProgressBar.vue'
import SegmentStatusList from './SegmentStatusList.vue'
import TimeEstimationPanel from './TimeEstimationPanel.vue'
import CancelConfirmationDialog from './CancelConfirmationDialog.vue'
import PartialResultPreviewDialog from './PartialResultPreviewDialog.vue'

// Props 定義
interface Props {
  batchId: string                         // 批次處理 ID
  initialProgress?: ProcessingProgress    // 初始進度資料
  autoRefresh?: boolean                   // 自動刷新進度
  refreshInterval?: number                // 刷新間隔（毫秒）
  showTimeEstimation?: boolean            // 顯示時間預估
  showSegmentList?: boolean               // 顯示分段列表
  showHistoricalComparison?: boolean      // 顯示歷史比較
  canPauseProgress?: boolean              // 允許暫停處理
  canCancelProgress?: boolean             // 允許取消處理
  maxVisibleSegments?: number             // 最大顯示分段數
  defaultViewMode?: 'compact' | 'normal' | 'detailed'  // 預設檢視模式
  enableFullscreen?: boolean              // 啟用全螢幕模式
  useVirtualScrolling?: boolean           // 使用虛擬滾動
}

const props = withDefaults(defineProps<Props>(), {
  initialProgress: undefined,
  autoRefresh: true,
  refreshInterval: 2000,
  showTimeEstimation: true,
  showSegmentList: true,
  showHistoricalComparison: false,
  canPauseProgress: true,
  canCancelProgress: true,
  maxVisibleSegments: 50,
  defaultViewMode: 'normal',
  enableFullscreen: true,
  useVirtualScrolling: true
})

// Emits 定義
const emit = defineEmits<{
  progressUpdate: [progress: ProcessingProgress]
  segmentRetry: [segmentIndex: number]
  pauseProgress: []
  cancelProgress: [options: { savePartialResults: boolean }]
  continueFromPartialResult: [options: { partialResultId: string; comment: string }]
  close: []
  error: [error: Error]
}>()

// 內部狀態
const isLoading = ref(true)
const hasError = ref(false)
const errorMessage = ref('')
const loadingText = ref('載入進度資訊...')

const progress = ref<ProcessingProgress>(
  props.initialProgress || {} as ProcessingProgress
)
const segmentStatuses = ref<SegmentStatus[]>([])

// 取消確認對話框狀態
const showCancelDialog = ref(false)

// 部分結果預覽對話框狀態
const showPartialResultDialog = ref(false)
const currentPartialResult = ref<PartialResult | null>(null)
const isProcessingPartialResult = ref(false)

const viewMode = ref<'compact' | 'normal' | 'detailed'>(props.defaultViewMode)
const autoRefreshEnabled = ref(props.autoRefresh)
const isFullscreen = ref(false)
const isPaused = ref(false)
const lastUpdateTime = ref<Date | null>(null)

const selectedSegmentError = ref<SegmentStatus | null>(null)
const segmentListRef = ref<InstanceType<typeof SegmentStatusList> | null>(null)

// SignalR 連線管理
const signalR = useSignalR({
  automaticReconnect: true,
  enableHeartbeat: true
})

// 進度更新節流處理
const { smartProgressUpdate } = useProgressThrottling()

// 計算屬性
const viewClasses = computed(() => [
  `view-mode-${viewMode.value}`,
  {
    'fullscreen': isFullscreen.value,
    'has-error': hasError.value,
    'loading': isLoading.value
  }
])

const progressTitle = computed(() => {
  if (progress.value.currentStage === 'completed') return '處理完成'
  if (progress.value.currentStage === 'failed') return '處理失敗'
  return `處理中 - ${Math.round(progress.value.overallProgress || 0)}%`
})

const progressSubtitle = computed(() => {
  const total = progress.value.totalSegments || 0
  const completed = progress.value.completedSegments || 0
  const failed = progress.value.failedSegments || 0
  
  return `${completed}/${total} 分段已完成` + (failed > 0 ? `，${failed} 個失敗` : '')
})

const progressBarTitle = computed(() => 
  viewMode.value === 'compact' ? '' : '整體處理進度'
)

const progressBarVariant = computed(() => {
  if (progress.value.currentStage === 'failed') return 'danger'
  if (progress.value.currentStage === 'completed') return 'success'
  return 'primary'
})

const progressBarSize = computed(() => {
  if (viewMode.value === 'compact') return 'sm'
  if (viewMode.value === 'detailed') return 'lg'
  return 'md'
})

const stageMarkers = computed(() => {
  return DEFAULT_STAGE_DEFINITIONS.map(stage => ({
    position: stage.estimatedDurationPercentage,
    name: stage.name
  }))
})

const availableViewModes = [
  { value: 'compact', label: '精簡', icon: '📱', description: '精簡檢視模式' },
  { value: 'normal', label: '標準', icon: '🖥️', description: '標準檢視模式' },
  { value: 'detailed', label: '詳細', icon: '🔍', description: '詳細檢視模式' }
]

const progressSectionClasses = computed(() => [
  'progress-sections',
  {
    'single-column': viewMode.value === 'compact',
    'two-columns': viewMode.value !== 'compact' && props.showSegmentList
  }
])

const showDetailedTiming = computed(() => 
  viewMode.value === 'detailed'
)

const showBottomPanel = computed(() => 
  viewMode.value !== 'compact'
)

const stageIndicatorOrientation = computed((): 'horizontal' | 'vertical' => 
  viewMode.value === 'compact' ? 'horizontal' : 'horizontal'
)

// 連線狀態
const connectionStatusClasses = computed(() => [
  'connection-status-base',
  `status-${signalR.connectionState.value}`
])

const connectionStatusText = computed(() => {
  const stateTexts = {
    [ConnectionState.Connected]: '已連線',
    [ConnectionState.Connecting]: '連線中...',
    [ConnectionState.Disconnected]: '已斷線',
    [ConnectionState.Reconnecting]: '重新連線中...',
    [ConnectionState.Disconnecting]: '斷線中...'
  }
  return stateTexts[signalR.connectionState.value] || '未知狀態'
})

// 歷史資料（示例）
const historicalData = computed(() => {
  if (!props.showHistoricalComparison) return undefined
  
  return {
    lastRun: 45000, // 45秒
    average: 60000, // 60秒
    bestTime: 30000 // 30秒
  }
})

// 方法定義
const initializeProgressView = async () => {
  try {
    isLoading.value = true
    loadingText.value = '建立連線...'

    // 啟動 SignalR 連線
    await signalR.startConnection()
    
    loadingText.value = '加入處理群組...'
    
    // 加入批次群組
    await signalR.joinBatchGroup(props.batchId)
    
    loadingText.value = '載入進度資訊...'
    
    // 請求最新進度
    await signalR.requestProgressUpdate(props.batchId)
    
    hasError.value = false
    
  } catch (error) {
    hasError.value = true
    errorMessage.value = error instanceof Error ? error.message : '未知錯誤'
    emit('error', error instanceof Error ? error : new Error(String(error)))
  } finally {
    isLoading.value = false
  }
}

const handleProgressUpdate = (newProgress: ProcessingProgress) => {
  smartProgressUpdate(newProgress, (throttledProgress) => {
    progress.value = throttledProgress
    lastUpdateTime.value = new Date()
    emit('progressUpdate', throttledProgress)
  })
}

const handleSegmentStatusUpdate = (segment: SegmentStatus) => {
  const index = segmentStatuses.value.findIndex(s => s.index === segment.index)
  if (index >= 0) {
    segmentStatuses.value[index] = segment
  } else {
    segmentStatuses.value.push(segment)
  }
  
  // 排序分段列表
  segmentStatuses.value.sort((a, b) => a.index - b.index)
}

const handleStageChange = (stage: ProcessingStage, info?: any) => {
  if (progress.value) {
    progress.value.currentStage = stage
    lastUpdateTime.value = new Date()
  }
}

const setViewMode = (mode: 'compact' | 'normal' | 'detailed') => {
  viewMode.value = mode
  
  // 在切換到詳細模式時，滾動分段列表到當前處理項目
  if (mode === 'detailed' && segmentListRef.value) {
    nextTick(() => {
      segmentListRef.value?.scrollToCurrentSegment()
    })
  }
}

const toggleAutoRefresh = () => {
  autoRefreshEnabled.value = !autoRefreshEnabled.value
}

const toggleFullscreen = () => {
  isFullscreen.value = !isFullscreen.value
  
  if (isFullscreen.value) {
    document.documentElement.requestFullscreen?.()
  } else {
    document.exitFullscreen?.()
  }
}

const handleStageClick = (stage: StageDefinition) => {
  // 處理階段點擊事件（可選功能）
  console.log('Stage clicked:', stage)
}

const handleSegmentRetry = (segmentIndex: number) => {
  emit('segmentRetry', segmentIndex)
  
  // 關閉錯誤彈窗（如果開啟的話）
  if (selectedSegmentError.value?.index === segmentIndex) {
    selectedSegmentError.value = null
  }
}

const handleShowSegmentError = (segment: SegmentStatus) => {
  selectedSegmentError.value = segment
}

const closeSegmentErrorModal = () => {
  selectedSegmentError.value = null
}

const handleLoadMoreSegments = () => {
  // 處理載入更多分段的邏輯
  console.log('Load more segments requested')
}

const pauseProgress = () => {
  isPaused.value = !isPaused.value
  emit('pauseProgress')
}

const confirmCancelProgress = () => {
  showCancelDialog.value = true
}

const handleCancelDialogConfirm = async (savePartialResults: boolean) => {
  showCancelDialog.value = false
  
  if (savePartialResults) {
    // 如果用戶選擇保存部分結果，先處理部分結果
    await processPartialResult()
  } else {
    // 直接取消，不保存部分結果
    emit('cancelProgress', { savePartialResults: false })
  }
}

const handleCancelDialogClose = () => {
  showCancelDialog.value = false
}

// 處理部分結果的主要邏輯
const processPartialResult = async () => {
  try {
    isProcessingPartialResult.value = true
    
    // 使用專案的 API 客戶端調用後端 API 處理部分結果
    const { default: apiClient } = await import('@/api')
    const partialResult = await apiClient.post(`/api/partialresult/process/${props.batchId}`) as PartialResult
    
    currentPartialResult.value = partialResult
    showPartialResultDialog.value = true
  } catch (error) {
    console.error('處理部分結果時發生錯誤:', error)
    // 降級處理：直接取消
    emit('cancelProgress', { savePartialResults: false })
  } finally {
    isProcessingPartialResult.value = false
  }
}

// 部分結果對話框事件處理
const handlePartialResultSave = async (comment: string) => {
  if (!currentPartialResult.value) return
  
  try {
    const { default: apiClient } = await import('@/api')
    await apiClient.post(`/api/partialresult/save/${currentPartialResult.value.partialResultId}`, {
      status: 'Accepted',
      userComment: comment
    })
    
    showPartialResultDialog.value = false
    currentPartialResult.value = null
    emit('cancelProgress', { savePartialResults: true })
  } catch (error) {
    console.error('保存部分結果時發生錯誤:', error)
  }
}

const handlePartialResultDiscard = async (comment: string) => {
  if (!currentPartialResult.value) return
  
  try {
    const { default: apiClient } = await import('@/api')
    await apiClient.post(`/api/partialresult/save/${currentPartialResult.value.partialResultId}`, {
      status: 'Rejected',
      userComment: comment
    })
    
    showPartialResultDialog.value = false
    currentPartialResult.value = null
    emit('cancelProgress', { savePartialResults: false })
  } catch (error) {
    console.error('丟棄部分結果時發生錯誤:', error)
  }
}

const handlePartialResultContinue = async (comment: string) => {
  if (!currentPartialResult.value) return
  
  try {
    const { default: apiClient } = await import('@/api')
    await apiClient.post(`/api/partialresult/save/${currentPartialResult.value.partialResultId}`, {
      status: 'PendingUserDecision',
      userComment: comment
    })
    
    showPartialResultDialog.value = false
    const partialResultId = currentPartialResult.value.partialResultId
    currentPartialResult.value = null
    emit('continueFromPartialResult', { 
      partialResultId: partialResultId,
      comment: comment 
    })
  } catch (error) {
    console.error('繼續處理部分結果時發生錯誤:', error)
  }
}

const handlePartialResultDialogClose = () => {
  showPartialResultDialog.value = false
  currentPartialResult.value = null
}

const downloadProgressReport = () => {
  // 生成並下載進度報告
  const reportData = {
    batchId: props.batchId,
    progress: progress.value,
    segments: segmentStatuses.value,
    timestamp: new Date().toISOString()
  }
  
  const blob = new Blob([JSON.stringify(reportData, null, 2)], { 
    type: 'application/json' 
  })
  
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `progress-report-${props.batchId}.json`
  a.click()
  
  URL.revokeObjectURL(url)
}

const closeProgressView = () => {
  emit('close')
}

const retry = async () => {
  hasError.value = false
  await initializeProgressView()
}

const formatRelativeTime = (date: Date): string => {
  const now = new Date()
  const diff = now.getTime() - date.getTime()
  
  if (diff < 60000) return '剛剛'
  if (diff < 3600000) return `${Math.floor(diff / 60000)} 分鐘前`
  return `${Math.floor(diff / 3600000)} 小時前`
}

// 生命週期
onMounted(async () => {
  // 設定 SignalR 事件處理器
  signalR.setEventHandlers({
    onProgressUpdate: handleProgressUpdate,
    onSegmentStatusUpdate: handleSegmentStatusUpdate,
    onStageChanged: handleStageChange,
    onConnectionStateChanged: (state) => {
      if (state === ConnectionState.Connected && hasError.value) {
        // 重新連線後重試
        retry()
      }
    },
    onError: (error) => {
      hasError.value = true
      errorMessage.value = error.message
      emit('error', error)
    }
  })
  
  // 初始化進度檢視
  await initializeProgressView()
})

onUnmounted(async () => {
  try {
    if (signalR.currentBatchId.value) {
      await signalR.leaveBatchGroup(signalR.currentBatchId.value)
    }
    await signalR.stopConnection()
  } catch (error) {
    // 忽略卸載時的錯誤
  }
})

// 監聽全螢幕狀態變化
document.addEventListener('fullscreenchange', () => {
  isFullscreen.value = !!document.fullscreenElement
})
</script>

<style scoped>
.processing-progress-view {
  @apply relative w-full min-h-96 bg-white border rounded-lg shadow-lg overflow-hidden;
}

/* 載入狀態 */
.loading-overlay {
  @apply absolute inset-0 bg-white bg-opacity-90 flex items-center justify-center z-50;
}

.loading-content {
  @apply text-center;
}

.loading-spinner {
  @apply w-8 h-8 border-4 border-blue-500 border-t-transparent rounded-full animate-spin mx-auto mb-3;
}

.loading-text {
  @apply text-gray-600 text-sm;
}

/* 錯誤狀態 */
.error-state {
  @apply p-8 text-center;
}

.error-icon {
  @apply text-4xl mb-3;
}

.error-title {
  @apply text-xl font-semibold text-gray-800 mb-2;
}

.error-message {
  @apply text-gray-600 mb-4;
}

.error-actions {
  @apply space-x-3;
}

.retry-btn, .close-btn {
  @apply px-4 py-2 rounded-lg text-sm font-medium transition-colors;
}

.retry-btn {
  @apply bg-blue-500 text-white hover:bg-blue-600;
}

.close-btn {
  @apply bg-gray-500 text-white hover:bg-gray-600;
}

/* 進度內容 */
.progress-content {
  @apply p-6 space-y-6;
}

/* 頭部 */
.progress-header {
  @apply flex justify-between items-start pb-4 border-b;
}

.progress-title {
  @apply text-2xl font-bold text-gray-800;
}

.progress-subtitle {
  @apply text-gray-600 mt-1;
}

.header-controls {
  @apply flex items-center space-x-3;
}

.view-mode-toggle {
  @apply flex bg-gray-100 rounded-lg p-1;
}

.mode-btn {
  @apply px-3 py-1 text-xs font-medium rounded transition-colors;
}

.mode-btn.active {
  @apply bg-white text-blue-600 shadow-sm;
}

.mode-btn:not(.active) {
  @apply text-gray-600 hover:text-gray-800;
}

.control-btn {
  @apply p-2 rounded-lg text-gray-600 hover:bg-gray-100 transition-colors;
}

.control-btn.active {
  @apply bg-blue-100 text-blue-600;
}

.control-btn.close-btn {
  @apply hover:bg-red-100 hover:text-red-600;
}

/* 階段區域 */
.stage-section {
  @apply py-4;
}

/* 主要進度區域 */
.main-progress-section {
  @apply space-y-6;
}

.main-progress-section.two-columns {
  @apply grid grid-cols-1 lg:grid-cols-2 gap-6 space-y-0;
}

.progress-left-panel {
  @apply space-y-6;
}

.progress-right-panel {
  @apply space-y-4;
}

/* 底部面板 */
.bottom-panel {
  @apply flex justify-between items-center pt-4 border-t;
}

.connection-status {
  @apply flex items-center space-x-2 text-sm;
}

.status-indicator {
  @apply w-2 h-2 rounded-full;
}

.status-connected .status-indicator {
  @apply bg-green-500;
}

.status-connecting .status-indicator, 
.status-reconnecting .status-indicator {
  @apply bg-yellow-500 animate-pulse;
}

.status-disconnected .status-indicator {
  @apply bg-red-500;
}

.last-update {
  @apply text-gray-500;
}

.quick-actions {
  @apply flex space-x-2;
}

.action-btn {
  @apply px-3 py-1 text-xs font-medium rounded-lg transition-colors;
}

.pause-btn {
  @apply bg-yellow-500 text-white hover:bg-yellow-600;
}

.cancel-btn {
  @apply bg-red-500 text-white hover:bg-red-600;
}

.download-btn {
  @apply bg-green-500 text-white hover:bg-green-600;
}

.action-btn:disabled {
  @apply opacity-50 cursor-not-allowed;
}

/* 彈窗樣式 */
.modal-overlay {
  @apply fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50;
}

.modal-content {
  @apply bg-white rounded-lg shadow-xl max-w-md w-full mx-4;
}

.modal-header {
  @apply flex justify-between items-center p-4 border-b;
}

.modal-title {
  @apply text-lg font-semibold;
}

.modal-close {
  @apply p-1 rounded hover:bg-gray-100 transition-colors;
}

.modal-body {
  @apply p-4;
}

.error-info {
  @apply space-y-3;
}

.info-row {
  @apply flex justify-between;
}

.info-label {
  @apply font-medium text-gray-700;
}

.info-value {
  @apply text-gray-900;
}

.error-text {
  @apply text-red-600 break-words;
}

.modal-footer {
  @apply flex justify-end space-x-3 p-4 border-t;
}

.retry-segment-btn {
  @apply px-4 py-2 bg-blue-500 text-white rounded-lg text-sm hover:bg-blue-600 transition-colors;
}

.close-modal-btn {
  @apply px-4 py-2 bg-gray-500 text-white rounded-lg text-sm hover:bg-gray-600 transition-colors;
}

/* 檢視模式樣式 */
.view-mode-compact .progress-content {
  @apply p-4 space-y-4;
}

.view-mode-compact .progress-title {
  @apply text-lg;
}

.view-mode-detailed .progress-content {
  @apply p-8 space-y-8;
}

/* 全螢幕模式 */
.fullscreen {
  @apply fixed inset-0 z-50 rounded-none;
}

/* 響應式設計 */
@media (max-width: 1024px) {
  .main-progress-section.two-columns {
    @apply grid-cols-1;
  }
}

@media (max-width: 768px) {
  .progress-header {
    @apply flex-col space-y-4;
  }
  
  .header-controls {
    @apply w-full justify-between;
  }
  
  .view-mode-toggle {
    @apply flex-1;
  }
  
  .mode-btn {
    @apply flex-1 text-center;
  }
  
  .bottom-panel {
    @apply flex-col space-y-3;
  }
  
  .quick-actions {
    @apply w-full justify-center;
  }
}

/* 深色模式 */
@media (prefers-color-scheme: dark) {
  .processing-progress-view {
    @apply bg-gray-800 border-gray-700;
  }
  
  .progress-title {
    @apply text-gray-200;
  }
  
  .progress-subtitle,
  .status-text,
  .last-update {
    @apply text-gray-400;
  }
  
  .modal-content {
    @apply bg-gray-800;
  }
  
  .info-label {
    @apply text-gray-300;
  }
  
  .info-value {
    @apply text-gray-200;
  }
}
</style>