<!-- 處理階段指示器元件 -->
<template>
  <div class="stage-indicator-container">
    <!-- 水平步驟導航 -->
    <div class="stage-navigation" :class="navigationClasses">
      <div
        v-for="(stageItem, index) in orderedStages"
        :key="stageItem.stage"
        class="stage-item"
        :class="getStageItemClasses(stageItem, index)"
        @click="handleStageClick(stageItem)"
      >
        <!-- 階段圖示 -->
        <div class="stage-icon-wrapper">
          <div
            class="stage-icon"
            :class="getStageIconClasses(stageItem)"
          >
            <!-- 圖示內容 -->
            <component
              v-if="getStageIcon(stageItem)"
              :is="getStageIcon(stageItem)"
              class="icon-component"
            />
            <span v-else class="icon-text">
              {{ getStageIconText(stageItem) }}
            </span>
            
            <!-- 載入動畫 -->
            <div
              v-if="isStageActive(stageItem) && showLoadingAnimation"
              class="loading-overlay"
            >
              <div class="loading-spinner"></div>
            </div>
          </div>
          
          <!-- 階段進度環 -->
          <div
            v-if="showStageProgress && isStageActive(stageItem)"
            class="stage-progress-ring"
          >
            <svg class="progress-svg" width="60" height="60">
              <circle
                class="progress-background"
                cx="30"
                cy="30"
                r="26"
                fill="none"
                stroke="#e5e7eb"
                stroke-width="2"
              />
              <circle
                class="progress-foreground"
                cx="30"
                cy="30"
                r="26"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                :stroke-dasharray="circumference"
                :stroke-dashoffset="progressOffset"
                :style="{ color: getStageColor(stageItem) }"
              />
            </svg>
          </div>
        </div>
        
        <!-- 階段資訊 -->
        <div class="stage-info">
          <h4 class="stage-name">{{ stageItem.name }}</h4>
          <p v-if="showDescription" class="stage-description">
            {{ stageItem.description }}
          </p>
          
          <!-- 階段狀態 -->
          <div class="stage-status">
            <span class="status-indicator" :class="getStatusClasses(stageItem)">
              {{ getStageStatusText(stageItem) }}
            </span>
            
            <!-- 時間資訊 -->
            <span
              v-if="showTiming && getStageTime(stageItem)"
              class="stage-time"
            >
              {{ getStageTime(stageItem) }}
            </span>
          </div>
        </div>
        
        <!-- 連接線 -->
        <div
          v-if="index < orderedStages.length - 1"
          class="stage-connector"
          :class="getConnectorClasses(stageItem, orderedStages[index + 1])"
        ></div>
      </div>
    </div>
    
    <!-- 當前階段詳細說明 -->
    <div
      v-if="showCurrentStageDetails && currentStageDetail"
      class="current-stage-detail"
    >
      <div class="detail-header">
        <h3 class="detail-title">{{ currentStageDetail.name }}</h3>
        <span class="detail-progress">{{ currentStageProgressText }}</span>
      </div>
      <p class="detail-description">{{ currentStageDetail.description }}</p>
      
      <!-- 階段子任務 -->
      <div v-if="currentStageSubtasks.length > 0" class="subtasks-list">
        <h4 class="subtasks-title">處理步驟:</h4>
        <ul class="subtasks">
          <li
            v-for="(subtask, index) in currentStageSubtasks"
            :key="index"
            class="subtask-item"
            :class="{ 'completed': index < currentSubtaskIndex }"
          >
            <span class="subtask-indicator">
              {{ index < currentSubtaskIndex ? '✓' : index === currentSubtaskIndex ? '⏳' : '○' }}
            </span>
            <span class="subtask-text">{{ subtask }}</span>
          </li>
        </ul>
      </div>
    </div>
    
    <!-- 緊湊模式的線性指示器 -->
    <div v-if="compact" class="compact-indicator">
      <div class="compact-progress">
        <div
          class="compact-fill"
          :style="{ width: `${overallProgress}%` }"
        ></div>
      </div>
      <div class="compact-info">
        <span class="compact-stage">{{ currentStageDetail?.name }}</span>
        <span class="compact-progress-text">{{ Math.round(overallProgress) }}%</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { ProcessingStage, StageDefinition } from '@/types/progress'
import { DEFAULT_STAGE_DEFINITIONS } from '@/types/progress'

// Props 定義
interface Props {
  currentStage: ProcessingStage         // 當前處理階段
  stageProgress?: number                // 當前階段進度 (0-100)
  overallProgress?: number              // 整體進度 (0-100)
  stages?: StageDefinition[]            // 自訂階段定義
  showDescription?: boolean             // 是否顯示階段描述
  showTiming?: boolean                  // 是否顯示時間資訊
  showStageProgress?: boolean           // 是否顯示階段進度環
  showLoadingAnimation?: boolean        // 是否顯示載入動畫
  showCurrentStageDetails?: boolean     // 是否顯示當前階段詳細資訊
  compact?: boolean                     // 緊湊模式
  clickableStages?: boolean             // 階段是否可點擊
  orientation?: 'horizontal' | 'vertical' // 佈局方向
}

const props = withDefaults(defineProps<Props>(), {
  stageProgress: 0,
  overallProgress: 0,
  stages: () => DEFAULT_STAGE_DEFINITIONS,
  showDescription: true,
  showTiming: false,
  showStageProgress: true,
  showLoadingAnimation: true,
  showCurrentStageDetails: true,
  compact: false,
  clickableStages: false,
  orientation: 'horizontal'
})

// Emits 定義
const emit = defineEmits<{
  stageClick: [stage: StageDefinition]
}>()

// 計算屬性
const orderedStages = computed(() => 
  [...props.stages].sort((a, b) => a.order - b.order)
)

const currentStageDetail = computed(() =>
  orderedStages.value.find(s => s.stage === props.currentStage)
)

const currentStageProgressText = computed(() => {
  if (props.currentStage === 'completed') return '100%'
  if (props.currentStage === 'failed') return '失敗'
  return `${Math.round(props.stageProgress)}%`
})

// 進度環相關計算
const circumference = 2 * Math.PI * 26 // r = 26
const progressOffset = computed(() => {
  const progress = props.stageProgress / 100
  return circumference * (1 - progress)
})

// 階段樣式類別
const navigationClasses = computed(() => [
  'stage-nav',
  {
    'vertical': props.orientation === 'vertical',
    'horizontal': props.orientation === 'horizontal',
    'compact': props.compact,
    'clickable': props.clickableStages
  }
])

// 方法定義
const getStageItemClasses = (stageItem: StageDefinition, index: number) => [
  'stage-base',
  {
    'stage-completed': isStageCompleted(stageItem),
    'stage-active': isStageActive(stageItem),
    'stage-pending': isStagePending(stageItem),
    'stage-failed': props.currentStage === 'failed' && isStageActive(stageItem),
    'clickable': props.clickableStages
  }
]

const getStageIconClasses = (stageItem: StageDefinition) => [
  'icon-base',
  {
    'icon-completed': isStageCompleted(stageItem),
    'icon-active': isStageActive(stageItem),
    'icon-pending': isStagePending(stageItem),
    'icon-failed': props.currentStage === 'failed' && isStageActive(stageItem)
  }
]

const getConnectorClasses = (current: StageDefinition, next: StageDefinition) => [
  'connector-base',
  {
    'connector-completed': isStageCompleted(current),
    'connector-active': isStageCompleted(current) && isStageActive(next),
    'connector-pending': !isStageCompleted(current)
  }
]

const getStatusClasses = (stageItem: StageDefinition) => [
  'status-base',
  {
    'status-completed': isStageCompleted(stageItem),
    'status-active': isStageActive(stageItem),
    'status-pending': isStagePending(stageItem),
    'status-failed': props.currentStage === 'failed' && isStageActive(stageItem)
  }
]

// 階段狀態判斷
const isStageCompleted = (stageItem: StageDefinition): boolean => {
  const currentIndex = orderedStages.value.findIndex(s => s.stage === props.currentStage)
  const stageIndex = orderedStages.value.findIndex(s => s.stage === stageItem.stage)
  return currentIndex > stageIndex
}

const isStageActive = (stageItem: StageDefinition): boolean => {
  return stageItem.stage === props.currentStage
}

const isStagePending = (stageItem: StageDefinition): boolean => {
  const currentIndex = orderedStages.value.findIndex(s => s.stage === props.currentStage)
  const stageIndex = orderedStages.value.findIndex(s => s.stage === stageItem.stage)
  return currentIndex < stageIndex
}

// 階段圖示和文字
const getStageIcon = (stageItem: StageDefinition) => {
  // 這裡可以返回 Vue 元件，目前簡化為文字
  return null
}

const getStageIconText = (stageItem: StageDefinition): string => {
  const iconMap: Record<string, string> = {
    'settings': '⚙️',
    'cut': '✂️',
    'cpu': '🔄',
    'merge': '🔀',
    'check': '✅'
  }
  return iconMap[stageItem.icon] || '●'
}

const getStageColor = (stageItem: StageDefinition): string => {
  if (isStageCompleted(stageItem)) return '#10b981' // green-500
  if (isStageActive(stageItem)) return '#3b82f6'    // blue-500
  if (props.currentStage === 'failed' && isStageActive(stageItem)) return '#ef4444' // red-500
  return '#6b7280' // gray-500
}

const getStageStatusText = (stageItem: StageDefinition): string => {
  if (isStageCompleted(stageItem)) return '已完成'
  if (isStageActive(stageItem)) {
    if (props.currentStage === 'failed') return '失敗'
    return '進行中'
  }
  return '等待中'
}

const getStageTime = (stageItem: StageDefinition): string | null => {
  // 這裡可以計算並返回階段的時間資訊
  // 目前簡化實作
  return null
}

// 當前階段子任務
const currentStageSubtasks = computed(() => {
  const subtaskMap: Record<ProcessingStage, string[]> = {
    'initializing': ['檢查系統資源', '初始化處理環境', '載入配置設定'],
    'segmenting': ['分析文本結構', '計算分段大小', '建立處理佇列'],
    'batch-processing': ['分段摘要處理', '品質檢查', '進度更新'],
    'merging': ['整合分段結果', '處理重複內容', '最佳化輸出'],
    'finalizing': ['結果驗證', '格式化輸出', '清理暫存檔案'],
    'completed': [],
    'failed': []
  }
  
  return subtaskMap[props.currentStage] || []
})

const currentSubtaskIndex = computed(() => {
  // 根據階段進度計算當前子任務索引
  const subtaskCount = currentStageSubtasks.value.length
  if (subtaskCount === 0) return 0
  
  const progressRatio = props.stageProgress / 100
  return Math.floor(progressRatio * subtaskCount)
})

// 事件處理
const handleStageClick = (stageItem: StageDefinition) => {
  if (props.clickableStages) {
    emit('stageClick', stageItem)
  }
}
</script>

<style scoped>
.stage-indicator-container {
  @apply w-full;
}

/* 水平導航 */
.stage-navigation.horizontal {
  @apply flex items-start justify-between relative;
}

.stage-navigation.vertical {
  @apply flex flex-col space-y-4;
}

.stage-navigation.compact {
  @apply hidden;
}

/* 階段項目 */
.stage-item {
  @apply relative flex flex-col items-center text-center transition-all duration-300;
}

.horizontal .stage-item {
  @apply flex-1 max-w-xs;
}

.vertical .stage-item {
  @apply flex-row text-left w-full;
}

.stage-item.clickable {
  @apply cursor-pointer hover:transform hover:scale-105;
}

/* 階段圖示 */
.stage-icon-wrapper {
  @apply relative;
}

.stage-icon {
  @apply w-12 h-12 rounded-full border-2 flex items-center justify-center text-lg font-bold transition-all duration-300 relative overflow-hidden;
}

.vertical .stage-icon {
  @apply w-10 h-10 text-base mr-4;
}

.icon-pending {
  @apply bg-gray-100 border-gray-300 text-gray-500;
}

.icon-active {
  @apply bg-blue-100 border-blue-500 text-blue-600 animate-pulse;
}

.icon-completed {
  @apply bg-green-100 border-green-500 text-green-600;
}

.icon-failed {
  @apply bg-red-100 border-red-500 text-red-600;
}

/* 載入動畫 */
.loading-overlay {
  @apply absolute inset-0 bg-blue-500 bg-opacity-20 rounded-full flex items-center justify-center;
}

.loading-spinner {
  @apply w-6 h-6 border-2 border-blue-500 border-t-transparent rounded-full animate-spin;
}

/* 進度環 */
.stage-progress-ring {
  @apply absolute inset-0;
}

.progress-svg {
  @apply w-full h-full transform -rotate-90;
}

.progress-foreground {
  @apply transition-all duration-500 ease-out;
}

/* 階段資訊 */
.stage-info {
  @apply mt-3;
}

.vertical .stage-info {
  @apply mt-0 flex-1;
}

.stage-name {
  @apply text-sm font-medium text-gray-700 mb-1;
}

.stage-description {
  @apply text-xs text-gray-500 mb-2;
}

.stage-status {
  @apply flex flex-col items-center space-y-1;
}

.vertical .stage-status {
  @apply flex-row items-center space-y-0 space-x-2;
}

.status-indicator {
  @apply text-xs font-medium px-2 py-1 rounded-full;
}

.status-pending {
  @apply bg-gray-100 text-gray-600;
}

.status-active {
  @apply bg-blue-100 text-blue-600;
}

.status-completed {
  @apply bg-green-100 text-green-600;
}

.status-failed {
  @apply bg-red-100 text-red-600;
}

.stage-time {
  @apply text-xs text-gray-500;
}

/* 連接線 */
.stage-connector {
  @apply absolute top-6 left-full w-full h-0.5 transition-all duration-300;
}

.vertical .stage-connector {
  @apply top-full left-5 w-0.5 h-4;
}

.connector-pending {
  @apply bg-gray-300;
}

.connector-active {
  @apply bg-blue-500;
}

.connector-completed {
  @apply bg-green-500;
}

/* 當前階段詳細說明 */
.current-stage-detail {
  @apply mt-6 p-4 bg-blue-50 rounded-lg border border-blue-200;
}

.detail-header {
  @apply flex justify-between items-center mb-2;
}

.detail-title {
  @apply text-lg font-semibold text-blue-800;
}

.detail-progress {
  @apply text-sm font-medium text-blue-600 bg-blue-100 px-3 py-1 rounded-full;
}

.detail-description {
  @apply text-sm text-blue-700 mb-3;
}

/* 子任務列表 */
.subtasks-list {
  @apply mt-3;
}

.subtasks-title {
  @apply text-sm font-medium text-blue-800 mb-2;
}

.subtasks {
  @apply space-y-1;
}

.subtask-item {
  @apply flex items-center text-sm text-blue-700;
}

.subtask-item.completed {
  @apply text-green-600;
}

.subtask-indicator {
  @apply mr-2 font-medium;
}

.subtask-text {
  @apply flex-1;
}

/* 緊湊模式 */
.compact-indicator {
  @apply space-y-2;
}

.compact-progress {
  @apply w-full h-2 bg-gray-200 rounded-full overflow-hidden;
}

.compact-fill {
  @apply h-full bg-blue-500 transition-all duration-500 ease-out;
}

.compact-info {
  @apply flex justify-between items-center text-sm;
}

.compact-stage {
  @apply font-medium text-gray-700;
}

.compact-progress-text {
  @apply font-semibold text-blue-600;
}

/* 響應式設計 */
@media (max-width: 768px) {
  .stage-navigation.horizontal {
    @apply flex-col space-y-4;
  }
  
  .stage-item {
    @apply flex-row text-left w-full;
  }
  
  .stage-info {
    @apply mt-0 ml-4 flex-1;
  }
  
  .stage-status {
    @apply flex-row items-center space-y-0 space-x-2;
  }
  
  .stage-connector {
    @apply top-full left-6 w-0.5 h-4;
  }
}

/* 深色模式 */
@media (prefers-color-scheme: dark) {
  .current-stage-detail {
    @apply bg-blue-900 border-blue-700;
  }
  
  .detail-title {
    @apply text-blue-200;
  }
  
  .detail-description {
    @apply text-blue-300;
  }
  
  .stage-name {
    @apply text-gray-200;
  }
  
  .stage-description {
    @apply text-gray-400;
  }
}
</style>