<!-- 分段狀態視覺化列表元件 -->
<template>
  <div class="segment-status-list-container">
    <!-- 標題和摘要統計 -->
    <div class="list-header">
      <h3 class="text-sm font-medium text-gray-700 mb-2">
        分段處理狀態
      </h3>
      <div class="status-summary">
        <span class="status-count status-completed">
          已完成: {{ completedCount }}
        </span>
        <span class="status-count status-processing">
          處理中: {{ processingCount }}
        </span>
        <span class="status-count status-pending">
          等待中: {{ pendingCount }}
        </span>
        <span class="status-count status-failed" v-if="failedCount > 0">
          失敗: {{ failedCount }}
        </span>
      </div>
    </div>

    <!-- 分段列表 -->
    <div
      class="segment-list"
      :class="listClasses"
    >
      <!-- 虛擬滾動容器 -->
      <div
        v-if="useVirtualScrolling && segments.length > virtualScrollThreshold"
        ref="virtualContainer"
        class="virtual-scroll-container"
        @scroll="handleScroll"
      >
        <div
          class="virtual-content"
          :style="{ height: `${totalHeight}px` }"
        >
          <div
            class="virtual-visible-area"
            :style="{ transform: `translateY(${offsetY}px)` }"
          >
            <SegmentStatusItem
              v-for="segment in visibleSegments"
              :key="segment.index"
              :segment="segment"
              :is-current="segment.index === currentSegment"
              :show-details="showDetails"
              :compact="compact"
              @retry="handleRetry"
              @show-error="handleShowError"
            />
          </div>
        </div>
      </div>

      <!-- 標準渲染 -->
      <div v-else class="standard-list">
        <SegmentStatusItem
          v-for="segment in segments"
          :key="segment.index"
          :segment="segment"
          :is-current="segment.index === currentSegment"
          :show-details="showDetails"
          :compact="compact"
          @retry="handleRetry"
          @show-error="handleShowError"
        />
      </div>

      <!-- 空狀態 -->
      <div
        v-if="segments.length === 0"
        class="empty-state"
      >
        <div class="empty-icon">📄</div>
        <p class="empty-message">尚未開始分段處理</p>
      </div>
    </div>

    <!-- 載入更多 -->
    <div
      v-if="showLoadMore"
      class="load-more-container"
    >
      <button
        class="load-more-btn"
        @click="loadMore"
        :disabled="loading"
      >
        {{ loading ? '載入中...' : `載入更多 (還有 ${remainingCount} 個)` }}
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted, nextTick } from 'vue'
import type { SegmentStatus, SegmentProcessingStatus } from '@/types/progress'
import SegmentStatusItem from './SegmentStatusItem.vue'

// Props 定義
interface Props {
  segments: SegmentStatus[]           // 分段狀態列表
  currentSegment?: number             // 當前處理的分段索引
  showDetails?: boolean               // 是否顯示詳細資訊
  compact?: boolean                   // 緊湊模式
  maxVisibleItems?: number            // 最大顯示項目數
  useVirtualScrolling?: boolean       // 使用虛擬滾動
  virtualScrollThreshold?: number     // 虛擬滾動觸發閾值
  itemHeight?: number                 // 每個項目的高度（虛擬滾動用）
  showLoadMore?: boolean              // 顯示載入更多按鈕
  pageSize?: number                   // 分頁大小
}

const props = withDefaults(defineProps<Props>(), {
  currentSegment: -1,
  showDetails: false,
  compact: false,
  maxVisibleItems: 50,
  useVirtualScrolling: true,
  virtualScrollThreshold: 20,
  itemHeight: 80,
  showLoadMore: false,
  pageSize: 20
})

// Emits 定義
const emit = defineEmits<{
  retry: [segmentIndex: number]
  showError: [segment: SegmentStatus]
  loadMore: []
}>()

// 響應式狀態
const virtualContainer = ref<HTMLElement>()
const scrollTop = ref(0)
const containerHeight = ref(0)
const currentPage = ref(1)
const loading = ref(false)

// 狀態統計
const completedCount = computed(() => 
  props.segments.filter(s => s.status === 'completed').length
)

const processingCount = computed(() => 
  props.segments.filter(s => s.isProcessing).length
)

const pendingCount = computed(() => 
  props.segments.filter(s => s.status === 'pending').length
)

const failedCount = computed(() => 
  props.segments.filter(s => s.status === 'failed').length
)

// 分頁相關
const displayedSegments = computed(() => {
  if (!props.showLoadMore) return props.segments
  return props.segments.slice(0, currentPage.value * props.pageSize)
})

const remainingCount = computed(() => 
  Math.max(0, props.segments.length - displayedSegments.value.length)
)

// 虛擬滾動相關
const totalHeight = computed(() => 
  displayedSegments.value.length * props.itemHeight
)

const visibleStart = computed(() => 
  Math.floor(scrollTop.value / props.itemHeight)
)

const visibleEnd = computed(() => {
  const end = visibleStart.value + Math.ceil(containerHeight.value / props.itemHeight) + 1
  return Math.min(end, displayedSegments.value.length)
})

const visibleSegments = computed(() => 
  displayedSegments.value.slice(visibleStart.value, visibleEnd.value)
)

const offsetY = computed(() => 
  visibleStart.value * props.itemHeight
)

// 樣式類別
const listClasses = computed(() => [
  {
    'compact-mode': props.compact,
    'detailed-mode': props.showDetails,
    'virtual-scrolling': props.useVirtualScrolling && displayedSegments.value.length > props.virtualScrollThreshold
  }
])

// 事件處理
const handleRetry = (segmentIndex: number) => {
  emit('retry', segmentIndex)
}

const handleShowError = (segment: SegmentStatus) => {
  emit('showError', segment)
}

const handleScroll = (event: Event) => {
  const target = event.target as HTMLElement
  scrollTop.value = target.scrollTop
}

const loadMore = async () => {
  if (loading.value) return
  
  loading.value = true
  currentPage.value += 1
  
  // 模擬載入延遲
  await new Promise(resolve => setTimeout(resolve, 300))
  
  loading.value = false
  emit('loadMore')
}

// 組件掛載
onMounted(async () => {
  if (props.useVirtualScrolling) {
    await nextTick()
    if (virtualContainer.value) {
      containerHeight.value = virtualContainer.value.clientHeight
      
      // 監聽容器大小變化
      const resizeObserver = new ResizeObserver(entries => {
        if (entries[0]) {
          containerHeight.value = entries[0].contentRect.height
        }
      })
      
      resizeObserver.observe(virtualContainer.value)
      
      // 清理函數
      onUnmounted(() => {
        resizeObserver.disconnect()
      })
    }
  }
})

// 自動滾動到當前處理的分段
const scrollToCurrentSegment = () => {
  if (props.currentSegment >= 0 && virtualContainer.value) {
    const targetScrollTop = props.currentSegment * props.itemHeight
    virtualContainer.value.scrollTo({
      top: targetScrollTop,
      behavior: 'smooth'
    })
  }
}

// 公開方法
defineExpose({
  scrollToCurrentSegment,
  scrollToTop: () => {
    if (virtualContainer.value) {
      virtualContainer.value.scrollTo({ top: 0, behavior: 'smooth' })
    }
  },
  scrollToBottom: () => {
    if (virtualContainer.value) {
      virtualContainer.value.scrollTo({ 
        top: totalHeight.value, 
        behavior: 'smooth' 
      })
    }
  }
})
</script>

<style scoped>
.segment-status-list-container {
  @apply bg-white rounded-lg border shadow-sm;
}

.list-header {
  @apply p-4 border-b bg-gray-50;
}

.status-summary {
  @apply flex flex-wrap gap-4 text-xs;
}

.status-count {
  @apply px-2 py-1 rounded-full text-white font-medium;
}

.status-completed {
  @apply bg-green-500;
}

.status-processing {
  @apply bg-blue-500;
}

.status-pending {
  @apply bg-gray-400;
}

.status-failed {
  @apply bg-red-500;
}

.segment-list {
  @apply relative;
}

.virtual-scroll-container {
  @apply h-64 overflow-auto;
}

.virtual-content {
  @apply relative;
}

.virtual-visible-area {
  @apply absolute top-0 left-0 right-0;
}

.standard-list {
  @apply max-h-64 overflow-y-auto;
}

.compact-mode .virtual-scroll-container,
.compact-mode .standard-list {
  @apply max-h-48;
}

.detailed-mode .virtual-scroll-container,
.detailed-mode .standard-list {
  @apply max-h-96;
}

.empty-state {
  @apply flex flex-col items-center justify-center py-8 text-gray-500;
}

.empty-icon {
  @apply text-4xl mb-2;
}

.empty-message {
  @apply text-sm;
}

.load-more-container {
  @apply p-4 border-t bg-gray-50 text-center;
}

.load-more-btn {
  @apply px-4 py-2 bg-blue-500 text-white rounded-lg text-sm font-medium hover:bg-blue-600 transition-colors disabled:opacity-50 disabled:cursor-not-allowed;
}

/* 響應式設計 */
@media (max-width: 768px) {
  .status-summary {
    @apply gap-2;
  }
  
  .segment-list {
    @apply text-sm;
  }
  
  .virtual-scroll-container,
  .standard-list {
    @apply max-h-48;
  }
}

/* 自訂滾動條 */
.virtual-scroll-container::-webkit-scrollbar,
.standard-list::-webkit-scrollbar {
  @apply w-2;
}

.virtual-scroll-container::-webkit-scrollbar-track,
.standard-list::-webkit-scrollbar-track {
  @apply bg-gray-100 rounded;
}

.virtual-scroll-container::-webkit-scrollbar-thumb,
.standard-list::-webkit-scrollbar-thumb {
  @apply bg-gray-300 rounded hover:bg-gray-400;
}

/* 深色模式 */
@media (prefers-color-scheme: dark) {
  .segment-status-list-container {
    @apply bg-gray-800 border-gray-700;
  }
  
  .list-header {
    @apply bg-gray-700 border-gray-600 text-gray-200;
  }
  
  .empty-state {
    @apply text-gray-400;
  }
}
</style>