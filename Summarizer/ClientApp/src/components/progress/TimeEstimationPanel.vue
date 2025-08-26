<!-- 時間預估與統計顯示面板元件 -->
<template>
  <div class="time-estimation-panel">
    <!-- 主要時間資訊 -->
    <div class="time-info-grid">
      <!-- 已耗時間 -->
      <div class="time-card elapsed-time">
        <div class="time-icon">⏱️</div>
        <div class="time-content">
          <h4 class="time-label">已耗時間</h4>
          <div class="time-value">{{ formatDuration(elapsedTimeMs) }}</div>
          <div class="time-sub">{{ formatElapsedSubText }}</div>
        </div>
      </div>

      <!-- 預估剩餘時間 -->
      <div class="time-card remaining-time">
        <div class="time-icon">⏳</div>
        <div class="time-content">
          <h4 class="time-label">預估剩餘</h4>
          <div class="time-value">
            {{ estimatedRemainingTimeMs ? formatDuration(estimatedRemainingTimeMs) : '計算中...' }}
          </div>
          <div v-if="estimatedCompletionTime" class="time-sub">
            完成: {{ formatCompletionTime }}
          </div>
        </div>
        <!-- 準確性指示器 -->
        <div v-if="estimationAccuracy !== null" class="accuracy-indicator">
          <div class="accuracy-bar">
            <div 
              class="accuracy-fill" 
              :style="{ width: `${estimationAccuracy}%` }"
              :class="accuracyClasses"
            ></div>
          </div>
          <span class="accuracy-text">預估準確度: {{ Math.round(estimationAccuracy) }}%</span>
        </div>
      </div>

      <!-- 總預估時間 -->
      <div class="time-card total-time">
        <div class="time-icon">🎯</div>
        <div class="time-content">
          <h4 class="time-label">總預估時間</h4>
          <div class="time-value">{{ formatDuration(totalEstimatedTime) }}</div>
          <div class="time-sub">基於當前處理速度</div>
        </div>
      </div>
    </div>

    <!-- 處理速度統計 -->
    <div class="speed-stats-section">
      <h3 class="section-title">處理速度統計</h3>
      <div class="speed-grid">
        <!-- 分段處理速度 -->
        <div class="speed-item">
          <div class="speed-label">分段/分鐘</div>
          <div class="speed-value">{{ processingSpeed.segmentsPerMinute.toFixed(1) }}</div>
        </div>

        <!-- 字符處理速度 -->
        <div class="speed-item">
          <div class="speed-label">字符/秒</div>
          <div class="speed-value">{{ Math.round(processingSpeed.charactersPerSecond) }}</div>
        </div>

        <!-- 平均延遲 -->
        <div class="speed-item">
          <div class="speed-label">平均延遲</div>
          <div class="speed-value">{{ formatDuration(processingSpeed.averageLatencyMs) }}</div>
        </div>

        <!-- 處理效率 -->
        <div class="speed-item">
          <div class="speed-label">效率</div>
          <div class="speed-value efficiency">
            {{ Math.round(processingSpeed.efficiencyPercentage) }}%
            <div class="efficiency-bar">
              <div 
                class="efficiency-fill" 
                :style="{ width: `${processingSpeed.efficiencyPercentage}%` }"
              ></div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 歷史比較（如果有歷史資料） -->
    <div v-if="showHistoricalComparison && historicalData" class="historical-comparison">
      <h3 class="section-title">歷史處理比較</h3>
      <div class="comparison-grid">
        <div class="comparison-item">
          <div class="comparison-label">與上次相比</div>
          <div class="comparison-value" :class="comparisonClasses.lastRun">
            {{ formatComparisonText(historicalData.lastRun) }}
          </div>
        </div>

        <div class="comparison-item">
          <div class="comparison-label">與平均相比</div>
          <div class="comparison-value" :class="comparisonClasses.average">
            {{ formatComparisonText(historicalData.average) }}
          </div>
        </div>

        <div class="comparison-item">
          <div class="comparison-label">最佳紀錄</div>
          <div class="comparison-value best-record">
            {{ formatDuration(historicalData.bestTime) }}
          </div>
        </div>
      </div>
    </div>

    <!-- 詳細統計（展開模式） -->
    <div v-if="showDetailedStats" class="detailed-stats">
      <div class="stats-toggle">
        <button 
          class="toggle-button"
          @click="toggleStatsExpanded"
        >
          {{ statsExpanded ? '收起' : '展開' }} 詳細統計
          <span class="toggle-icon" :class="{ 'rotated': statsExpanded }">▼</span>
        </button>
      </div>

      <transition name="stats-expand">
        <div v-if="statsExpanded" class="expanded-stats">
          <!-- 時間分佈圖表 -->
          <div class="time-distribution">
            <h4 class="stats-subtitle">各階段時間分佈</h4>
            <div class="stage-times">
              <div 
                v-for="stage in stageTimeDistribution" 
                :key="stage.name"
                class="stage-time-item"
              >
                <span class="stage-name">{{ stage.name }}</span>
                <div class="stage-time-bar">
                  <div 
                    class="stage-time-fill"
                    :style="{ 
                      width: `${stage.percentage}%`,
                      backgroundColor: stage.color 
                    }"
                  ></div>
                </div>
                <span class="stage-time-value">{{ formatDuration(stage.timeMs) }}</span>
              </div>
            </div>
          </div>

          <!-- 處理趨勢 -->
          <div class="processing-trend">
            <h4 class="stats-subtitle">處理速度趨勢</h4>
            <div class="trend-chart">
              <!-- 簡單的趨勢線圖表 -->
              <svg class="trend-svg" viewBox="0 0 200 60" xmlns="http://www.w3.org/2000/svg">
                <polyline
                  :points="trendLinePoints"
                  fill="none"
                  stroke="#3b82f6"
                  stroke-width="2"
                />
                <!-- 趨勢點 -->
                <circle
                  v-for="(point, index) in trendPoints"
                  :key="index"
                  :cx="point.x"
                  :cy="point.y"
                  r="2"
                  fill="#3b82f6"
                />
              </svg>
              <div class="trend-labels">
                <span class="trend-start">開始</span>
                <span class="trend-end">現在</span>
              </div>
            </div>
          </div>

          <!-- 性能指標 -->
          <div class="performance-metrics">
            <h4 class="stats-subtitle">性能指標</h4>
            <div class="metrics-grid">
              <div class="metric-item">
                <span class="metric-name">吞吐量穩定性</span>
                <span class="metric-value">{{ Math.round(performanceMetrics.throughputStability) }}%</span>
              </div>
              <div class="metric-item">
                <span class="metric-name">延遲變異性</span>
                <span class="metric-value">{{ performanceMetrics.latencyVariability.toFixed(2) }}ms</span>
              </div>
              <div class="metric-item">
                <span class="metric-name">資源利用率</span>
                <span class="metric-value">{{ Math.round(performanceMetrics.resourceUtilization) }}%</span>
              </div>
            </div>
          </div>
        </div>
      </transition>
    </div>

    <!-- 預估信心等級 -->
    <div class="confidence-indicator">
      <div class="confidence-content">
        <span class="confidence-label">預估信心等級:</span>
        <div class="confidence-level" :class="confidenceLevelClasses">
          <div class="confidence-dots">
            <div 
              v-for="i in 5" 
              :key="i"
              class="confidence-dot"
              :class="{ 'active': i <= confidenceLevel }"
            ></div>
          </div>
          <span class="confidence-text">{{ confidenceLevelText }}</span>
        </div>
      </div>
      <div v-if="confidenceFactors.length > 0" class="confidence-factors">
        <summary class="factors-toggle">影響因素</summary>
        <ul class="factors-list">
          <li v-for="factor in confidenceFactors" :key="factor" class="factor-item">
            {{ factor }}
          </li>
        </ul>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { ProcessingSpeed } from '@/types/progress'

// Props 定義
interface Props {
  elapsedTimeMs: number                        // 已花費時間（毫秒）
  estimatedRemainingTimeMs?: number            // 預估剩餘時間（毫秒）
  processingSpeed: ProcessingSpeed             // 處理速度統計
  estimatedCompletionTime?: string             // 預估完成時間 ISO 字串
  showHistoricalComparison?: boolean           // 是否顯示歷史比較
  showDetailedStats?: boolean                  // 是否顯示詳細統計
  historicalData?: {                          // 歷史資料
    lastRun: number
    average: number
    bestTime: number
  }
  completedSegments?: number                   // 已完成分段數
  totalSegments?: number                       // 總分段數
  currentStage?: string                        // 當前階段
}

const props = withDefaults(defineProps<Props>(), {
  estimatedRemainingTimeMs: undefined,
  estimatedCompletionTime: undefined,
  showHistoricalComparison: false,
  showDetailedStats: true,
  historicalData: undefined,
  completedSegments: 0,
  totalSegments: 0,
  currentStage: 'processing'
})

// 內部狀態
const statsExpanded = ref(false)
const estimationHistory = ref<number[]>([])

// 計算屬性
const totalEstimatedTime = computed(() => 
  props.elapsedTimeMs + (props.estimatedRemainingTimeMs || 0)
)

const formatElapsedSubText = computed(() => {
  const startTime = new Date(Date.now() - props.elapsedTimeMs)
  return `開始於 ${startTime.toLocaleTimeString('zh-TW')}`
})

const formatCompletionTime = computed(() => {
  if (!props.estimatedCompletionTime) return ''
  
  const completionTime = new Date(props.estimatedCompletionTime)
  const now = new Date()
  
  if (completionTime.toDateString() === now.toDateString()) {
    return completionTime.toLocaleTimeString('zh-TW', { 
      hour: '2-digit', 
      minute: '2-digit' 
    })
  } else {
    return completionTime.toLocaleString('zh-TW', { 
      month: 'short', 
      day: 'numeric',
      hour: '2-digit', 
      minute: '2-digit' 
    })
  }
})

// 預估準確性
const estimationAccuracy = computed(() => {
  if (!props.estimatedRemainingTimeMs || estimationHistory.value.length < 3) {
    return null
  }
  
  // 基於歷史預估的變異性計算準確度
  const recent = estimationHistory.value.slice(-5)
  const avg = recent.reduce((a, b) => a + b, 0) / recent.length
  const variance = recent.reduce((acc, val) => acc + Math.pow(val - avg, 2), 0) / recent.length
  const stdDev = Math.sqrt(variance)
  const coefficientOfVariation = stdDev / avg
  
  // 將變異系數轉換為準確度百分比（越低變異性 = 越高準確度）
  return Math.max(0, Math.min(100, 100 - (coefficientOfVariation * 100)))
})

const accuracyClasses = computed(() => ({
  'high-accuracy': (estimationAccuracy.value || 0) >= 80,
  'medium-accuracy': (estimationAccuracy.value || 0) >= 50 && (estimationAccuracy.value || 0) < 80,
  'low-accuracy': (estimationAccuracy.value || 0) < 50
}))

// 歷史比較
const comparisonClasses = computed(() => {
  if (!props.historicalData) return { lastRun: '', average: '' }
  
  const current = totalEstimatedTime.value
  return {
    lastRun: current < props.historicalData.lastRun ? 'faster' : 'slower',
    average: current < props.historicalData.average ? 'faster' : 'slower'
  }
})

// 信心等級
const confidenceLevel = computed(() => {
  let confidence = 3 // 基礎信心等級
  
  // 根據已完成分段數調整
  if (props.completedSegments > 5) confidence += 1
  if (props.completedSegments > 10) confidence += 1
  
  // 根據預估準確度調整
  if (estimationAccuracy.value && estimationAccuracy.value > 80) confidence += 1
  if (estimationAccuracy.value && estimationAccuracy.value < 50) confidence -= 1
  
  // 根據處理速度穩定性調整
  if (props.processingSpeed.efficiencyPercentage > 80) confidence += 1
  if (props.processingSpeed.efficiencyPercentage < 50) confidence -= 1
  
  return Math.max(1, Math.min(5, confidence))
})

const confidenceLevelText = computed(() => {
  const levels = ['非常低', '低', '中等', '高', '非常高']
  return levels[confidenceLevel.value - 1] || '未知'
})

const confidenceLevelClasses = computed(() => ({
  'confidence-very-low': confidenceLevel.value === 1,
  'confidence-low': confidenceLevel.value === 2,
  'confidence-medium': confidenceLevel.value === 3,
  'confidence-high': confidenceLevel.value === 4,
  'confidence-very-high': confidenceLevel.value === 5
}))

const confidenceFactors = computed(() => {
  const factors = []
  
  if (props.completedSegments < 3) factors.push('處理樣本較少')
  if (props.processingSpeed.efficiencyPercentage < 60) factors.push('處理效率不穩定')
  if (estimationAccuracy.value && estimationAccuracy.value < 60) factors.push('預估變異性較高')
  
  return factors
})

// 階段時間分佈
const stageTimeDistribution = computed(() => {
  // 模擬階段時間分佈，實際應用中應該從真實資料計算
  const totalTime = props.elapsedTimeMs
  const distributions = [
    { name: '初始化', percentage: 5, timeMs: totalTime * 0.05, color: '#6b7280' },
    { name: '分段', percentage: 10, timeMs: totalTime * 0.10, color: '#f59e0b' },
    { name: '處理', percentage: 70, timeMs: totalTime * 0.70, color: '#3b82f6' },
    { name: '合併', percentage: 10, timeMs: totalTime * 0.10, color: '#10b981' },
    { name: '完成', percentage: 5, timeMs: totalTime * 0.05, color: '#8b5cf6' }
  ]
  
  return distributions
})

// 趨勢資料
const trendPoints = computed(() => {
  // 生成模擬趨勢資料點
  const points = []
  for (let i = 0; i < 10; i++) {
    points.push({
      x: (i / 9) * 200,
      y: 30 + Math.sin(i * 0.5) * 20 + Math.random() * 10
    })
  }
  return points
})

const trendLinePoints = computed(() => 
  trendPoints.value.map(p => `${p.x},${p.y}`).join(' ')
)

// 性能指標
const performanceMetrics = computed(() => ({
  throughputStability: Math.max(0, 100 - (props.processingSpeed.maxLatencyMs - props.processingSpeed.minLatencyMs) / props.processingSpeed.averageLatencyMs * 100),
  latencyVariability: props.processingSpeed.maxLatencyMs - props.processingSpeed.minLatencyMs,
  resourceUtilization: props.processingSpeed.efficiencyPercentage
}))

// 工具函數
const formatDuration = (milliseconds: number): string => {
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

const formatComparisonText = (historicalTimeMs: number): string => {
  const current = totalEstimatedTime.value
  const diff = Math.abs(current - historicalTimeMs)
  const percentage = (diff / historicalTimeMs) * 100
  
  if (current < historicalTimeMs) {
    return `快 ${formatDuration(diff)} (${percentage.toFixed(1)}%)`
  } else {
    return `慢 ${formatDuration(diff)} (${percentage.toFixed(1)}%)`
  }
}

const toggleStatsExpanded = () => {
  statsExpanded.value = !statsExpanded.value
}

// 監聽預估時間變化，記錄歷史
watch(() => props.estimatedRemainingTimeMs, (newValue) => {
  if (newValue !== undefined) {
    estimationHistory.value.push(newValue)
    // 限制歷史記錄長度
    if (estimationHistory.value.length > 20) {
      estimationHistory.value.shift()
    }
  }
})
</script>

<style scoped>
.time-estimation-panel {
  @apply bg-white rounded-lg border shadow-sm p-6 space-y-6;
}

/* 時間資訊網格 */
.time-info-grid {
  @apply grid grid-cols-1 md:grid-cols-3 gap-4;
}

.time-card {
  @apply bg-gray-50 rounded-lg p-4 relative;
}

.time-card.elapsed-time {
  @apply bg-blue-50 border border-blue-200;
}

.time-card.remaining-time {
  @apply bg-orange-50 border border-orange-200;
}

.time-card.total-time {
  @apply bg-green-50 border border-green-200;
}

.time-icon {
  @apply text-2xl mb-2;
}

.time-label {
  @apply text-sm font-medium text-gray-600 mb-1;
}

.time-value {
  @apply text-2xl font-bold text-gray-900;
}

.time-sub {
  @apply text-xs text-gray-500 mt-1;
}

/* 準確性指示器 */
.accuracy-indicator {
  @apply mt-3;
}

.accuracy-bar {
  @apply w-full h-1 bg-gray-200 rounded-full mb-1;
}

.accuracy-fill {
  @apply h-full rounded-full transition-all duration-300;
}

.accuracy-fill.high-accuracy {
  @apply bg-green-500;
}

.accuracy-fill.medium-accuracy {
  @apply bg-yellow-500;
}

.accuracy-fill.low-accuracy {
  @apply bg-red-500;
}

.accuracy-text {
  @apply text-xs text-gray-600;
}

/* 速度統計 */
.speed-stats-section {
  @apply space-y-3;
}

.section-title {
  @apply text-lg font-semibold text-gray-800;
}

.speed-grid {
  @apply grid grid-cols-2 lg:grid-cols-4 gap-4;
}

.speed-item {
  @apply text-center p-3 bg-gray-50 rounded-lg;
}

.speed-label {
  @apply text-xs text-gray-600 mb-1;
}

.speed-value {
  @apply text-lg font-bold text-gray-900;
}

.speed-value.efficiency {
  @apply relative;
}

.efficiency-bar {
  @apply w-full h-1 bg-gray-200 rounded-full mt-1;
}

.efficiency-fill {
  @apply h-full bg-blue-500 rounded-full transition-all duration-300;
}

/* 歷史比較 */
.historical-comparison {
  @apply space-y-3;
}

.comparison-grid {
  @apply grid grid-cols-1 md:grid-cols-3 gap-4;
}

.comparison-item {
  @apply text-center p-3 bg-gray-50 rounded-lg;
}

.comparison-label {
  @apply text-xs text-gray-600 mb-1;
}

.comparison-value {
  @apply text-sm font-medium;
}

.comparison-value.faster {
  @apply text-green-600;
}

.comparison-value.slower {
  @apply text-red-600;
}

.comparison-value.best-record {
  @apply text-blue-600;
}

/* 詳細統計 */
.detailed-stats {
  @apply space-y-4;
}

.stats-toggle {
  @apply text-center;
}

.toggle-button {
  @apply px-4 py-2 bg-blue-500 text-white rounded-lg text-sm font-medium hover:bg-blue-600 transition-colors inline-flex items-center space-x-2;
}

.toggle-icon {
  @apply transition-transform duration-200;
}

.toggle-icon.rotated {
  @apply transform rotate-180;
}

.expanded-stats {
  @apply space-y-6;
}

.stats-subtitle {
  @apply text-base font-medium text-gray-700 mb-3;
}

/* 階段時間分佈 */
.stage-times {
  @apply space-y-2;
}

.stage-time-item {
  @apply flex items-center space-x-3;
}

.stage-name {
  @apply text-sm text-gray-600 w-16 flex-shrink-0;
}

.stage-time-bar {
  @apply flex-1 h-4 bg-gray-200 rounded-full overflow-hidden;
}

.stage-time-fill {
  @apply h-full transition-all duration-300;
}

.stage-time-value {
  @apply text-sm font-medium text-gray-700 w-20 text-right;
}

/* 趨勢圖表 */
.trend-chart {
  @apply relative;
}

.trend-svg {
  @apply w-full h-16 bg-gray-50 rounded;
}

.trend-labels {
  @apply flex justify-between text-xs text-gray-500 mt-1;
}

/* 性能指標 */
.metrics-grid {
  @apply grid grid-cols-1 md:grid-cols-3 gap-4;
}

.metric-item {
  @apply flex justify-between items-center p-3 bg-gray-50 rounded-lg;
}

.metric-name {
  @apply text-sm text-gray-600;
}

.metric-value {
  @apply text-sm font-medium text-gray-900;
}

/* 信心等級指示器 */
.confidence-indicator {
  @apply bg-gray-50 rounded-lg p-4;
}

.confidence-content {
  @apply flex justify-between items-center mb-2;
}

.confidence-label {
  @apply text-sm font-medium text-gray-700;
}

.confidence-level {
  @apply flex items-center space-x-2;
}

.confidence-dots {
  @apply flex space-x-1;
}

.confidence-dot {
  @apply w-2 h-2 rounded-full bg-gray-300 transition-colors duration-200;
}

.confidence-dot.active {
  @apply bg-blue-500;
}

.confidence-text {
  @apply text-sm font-medium;
}

.confidence-very-low .confidence-text {
  @apply text-red-600;
}

.confidence-low .confidence-text {
  @apply text-orange-600;
}

.confidence-medium .confidence-text {
  @apply text-yellow-600;
}

.confidence-high .confidence-text {
  @apply text-green-600;
}

.confidence-very-high .confidence-text {
  @apply text-blue-600;
}

.confidence-factors {
  @apply text-sm text-gray-600;
}

.factors-list {
  @apply list-disc list-inside space-y-1 mt-2;
}

/* 動畫 */
.stats-expand-enter-active,
.stats-expand-leave-active {
  @apply transition-all duration-300 ease-in-out;
}

.stats-expand-enter-from,
.stats-expand-leave-to {
  @apply opacity-0 transform -translate-y-4;
}

/* 響應式設計 */
@media (max-width: 768px) {
  .time-info-grid {
    @apply grid-cols-1;
  }
  
  .speed-grid {
    @apply grid-cols-2;
  }
  
  .comparison-grid {
    @apply grid-cols-1;
  }
  
  .metrics-grid {
    @apply grid-cols-1;
  }
}

/* 深色模式 */
@media (prefers-color-scheme: dark) {
  .time-estimation-panel {
    @apply bg-gray-800 border-gray-700;
  }
  
  .time-card {
    @apply bg-gray-700;
  }
  
  .section-title,
  .stats-subtitle {
    @apply text-gray-200;
  }
  
  .time-value,
  .speed-value,
  .metric-value {
    @apply text-gray-100;
  }
  
  .time-label,
  .speed-label,
  .metric-name,
  .confidence-label {
    @apply text-gray-300;
  }
}
</style>