<!-- 即時進度條元件 -->
<template>
  <div class="progress-bar-container">
    <!-- 進度條標題和百分比 -->
    <div class="flex justify-between items-center mb-2">
      <h3 class="text-sm font-medium text-gray-700">
        {{ title }}
      </h3>
      <span class="text-sm font-semibold text-gray-600">
        {{ Math.round(progress) }}%
      </span>
    </div>
    
    <!-- 主要進度條 -->
    <div 
      class="progress-bar-track"
      :class="trackClasses"
    >
      <!-- 進度填充 -->
      <div
        class="progress-bar-fill"
        :class="fillClasses"
        :style="{ width: `${Math.min(100, Math.max(0, progress))}%` }"
      >
        <!-- 動畫效果 -->
        <div
          v-if="showAnimation"
          class="progress-animation"
        ></div>
      </div>
      
      <!-- 階段分隔線（如果需要） -->
      <div 
        v-if="showStageMarkers && stageMarkers.length > 0"
        class="stage-markers"
      >
        <div
          v-for="marker in stageMarkers"
          :key="marker.position"
          class="stage-marker"
          :style="{ left: `${marker.position}%` }"
          :title="marker.name"
        ></div>
      </div>
    </div>
    
    <!-- 詳細資訊 -->
    <div
      v-if="showDetails"
      class="progress-details mt-2 text-xs text-gray-500"
    >
      <div class="flex justify-between">
        <span>{{ currentStageText }}</span>
        <span v-if="estimatedTime">
          預估剩餘: {{ formatTime(estimatedTime) }}
        </span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { ProcessingStage } from '@/types/progress'

// Props 定義
interface Props {
  progress: number                    // 進度百分比 (0-100)
  title?: string                      // 進度條標題
  variant?: 'primary' | 'success' | 'warning' | 'danger'  // 樣式變體
  size?: 'sm' | 'md' | 'lg'          // 尺寸
  showAnimation?: boolean             // 是否顯示動畫
  showDetails?: boolean               // 是否顯示詳細資訊
  showStageMarkers?: boolean          // 是否顯示階段標記
  currentStage?: ProcessingStage      // 當前階段
  estimatedTime?: number              // 預估剩餘時間（毫秒）
  stageMarkers?: Array<{             // 階段標記
    position: number
    name: string
  }>
}

const props = withDefaults(defineProps<Props>(), {
  progress: 0,
  title: '處理進度',
  variant: 'primary',
  size: 'md',
  showAnimation: true,
  showDetails: false,
  showStageMarkers: false,
  stageMarkers: () => []
})

// 樣式計算
const trackClasses = computed(() => [
  'progress-track',
  `progress-track-${props.size}`,
  {
    'progress-track-animated': props.showAnimation
  }
])

const fillClasses = computed(() => [
  'progress-fill',
  `progress-fill-${props.variant}`,
  {
    'progress-fill-animated': props.showAnimation,
    'progress-fill-pulsing': props.showAnimation && props.progress > 0 && props.progress < 100
  }
])

// 當前階段文字
const currentStageText = computed(() => {
  if (!props.currentStage) return ''
  
  const stageNames = {
    'initializing': '初始化中...',
    'segmenting': '分段處理中...',
    'batch-processing': '批次處理中...',
    'merging': '合併結果中...',
    'finalizing': '完成處理中...',
    'completed': '處理完成',
    'failed': '處理失敗'
  }
  
  return stageNames[props.currentStage] || '處理中...'
})

// 時間格式化函數
const formatTime = (milliseconds: number): string => {
  const seconds = Math.floor(milliseconds / 1000)
  const minutes = Math.floor(seconds / 60)
  const hours = Math.floor(minutes / 60)
  
  if (hours > 0) {
    return `${hours}:${(minutes % 60).toString().padStart(2, '0')}:${(seconds % 60).toString().padStart(2, '0')}`
  } else if (minutes > 0) {
    return `${minutes}:${(seconds % 60).toString().padStart(2, '0')}`
  } else {
    return `${seconds}秒`
  }
}

// 進度變化動畫
const animationKey = ref(0)
watch(() => props.progress, () => {
  if (props.showAnimation) {
    animationKey.value += 1
  }
})
</script>

<style scoped>
.progress-bar-container {
  @apply w-full;
}

/* 進度條軌道 */
.progress-track {
  @apply w-full bg-gray-200 rounded-full overflow-hidden relative;
}

.progress-track-sm {
  @apply h-2;
}

.progress-track-md {
  @apply h-3;
}

.progress-track-lg {
  @apply h-4;
}

.progress-track-animated {
  @apply transition-all duration-300 ease-out;
}

/* 進度填充 */
.progress-fill {
  @apply h-full rounded-full transition-all duration-500 ease-out relative overflow-hidden;
}

.progress-fill-primary {
  @apply bg-gradient-to-r from-blue-500 to-blue-600;
}

.progress-fill-success {
  @apply bg-gradient-to-r from-green-500 to-green-600;
}

.progress-fill-warning {
  @apply bg-gradient-to-r from-yellow-500 to-yellow-600;
}

.progress-fill-danger {
  @apply bg-gradient-to-r from-red-500 to-red-600;
}

/* 動畫效果 */
.progress-fill-animated {
  @apply transition-all duration-500 ease-out;
}

.progress-fill-pulsing {
  animation: progress-pulse 2s ease-in-out infinite;
}

.progress-animation {
  @apply absolute inset-0 bg-white opacity-20;
  animation: progress-shimmer 2s ease-in-out infinite;
}

/* 階段標記 */
.stage-markers {
  @apply absolute inset-0 pointer-events-none;
}

.stage-marker {
  @apply absolute top-0 bottom-0 w-0.5 bg-white opacity-50;
}

/* 動畫關鍵影格 */
@keyframes progress-pulse {
  0%, 100% { 
    opacity: 1; 
  }
  50% { 
    opacity: 0.8; 
  }
}

@keyframes progress-shimmer {
  0% {
    transform: translateX(-100%);
  }
  50% {
    transform: translateX(100%);
  }
  100% {
    transform: translateX(-100%);
  }
}

/* 進度詳情 */
.progress-details {
  @apply flex justify-between items-center;
}

/* 響應式設計 */
@media (max-width: 640px) {
  .progress-details {
    @apply flex-col space-y-1;
  }
  
  .progress-details > span {
    @apply text-center;
  }
}

/* 深色模式支援 */
@media (prefers-color-scheme: dark) {
  .progress-track {
    @apply bg-gray-700;
  }
  
  .progress-bar-container h3 {
    @apply text-gray-200;
  }
  
  .progress-bar-container .text-gray-600 {
    @apply text-gray-300;
  }
  
  .progress-details {
    @apply text-gray-400;
  }
}
</style>