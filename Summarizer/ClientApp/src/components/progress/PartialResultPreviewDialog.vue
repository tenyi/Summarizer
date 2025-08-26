<template>
  <div 
    v-if="isVisible" 
    class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4"
    @keydown.esc="handleClose"
    role="dialog"
    aria-labelledby="partial-result-title"
    aria-describedby="partial-result-description"
  >
    <div 
      class="bg-white rounded-lg shadow-xl max-w-4xl w-full max-h-[90vh] overflow-hidden flex flex-col"
      @click.stop
    >
      <!-- 對話框標題 -->
      <div class="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
        <div class="flex items-center space-x-3">
          <InformationCircleIcon class="h-8 w-8 text-blue-500" />
          <div>
            <h2 id="partial-result-title" class="text-xl font-semibold text-gray-900">
              部分摘要結果預覽
            </h2>
            <p id="partial-result-description" class="text-sm text-gray-600">
              處理已取消，您可以選擇保存或丟棄已完成的部分結果
            </p>
          </div>
        </div>
        <button
          @click="handleClose"
          class="text-gray-400 hover:text-gray-600 transition-colors"
          aria-label="關閉對話框"
        >
          <XMarkIcon class="h-6 w-6" />
        </button>
      </div>

      <!-- 對話框內容 -->
      <div class="flex-1 overflow-hidden">
        <div class="flex h-full">
          <!-- 左側：摘要內容 -->
          <div class="flex-1 flex flex-col p-6">
            <h3 class="text-lg font-medium text-gray-900 mb-4">摘要內容</h3>
            <div 
              class="flex-1 bg-gray-50 rounded-lg p-4 overflow-y-auto border"
              :class="{ 'text-gray-500': !partialResult?.partialSummary }"
            >
              <div v-if="partialResult?.partialSummary" class="whitespace-pre-wrap">
                {{ partialResult.partialSummary }}
              </div>
              <div v-else class="flex items-center justify-center h-32">
                <div class="text-center">
                  <DocumentTextIcon class="h-12 w-12 text-gray-300 mx-auto mb-2" />
                  <p>暫無摘要內容</p>
                </div>
              </div>
            </div>
          </div>

          <!-- 右側：品質評估 -->
          <div class="w-80 bg-gray-50 p-6 border-l border-gray-200">
            <h3 class="text-lg font-medium text-gray-900 mb-4">品質評估</h3>
            
            <!-- 基本統計 -->
            <div class="space-y-4">
              <div class="bg-white rounded-lg p-4 shadow-sm">
                <h4 class="font-medium text-gray-800 mb-2">基本資訊</h4>
                <div class="space-y-2 text-sm">
                  <div class="flex justify-between">
                    <span class="text-gray-600">完成進度</span>
                    <span class="font-medium">
                      {{ partialResult?.completionPercentage?.toFixed(1) ?? 0 }}%
                    </span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-gray-600">已完成分段</span>
                    <span class="font-medium">
                      {{ partialResult?.completedSegments?.length ?? 0 }} / {{ partialResult?.totalSegments ?? 0 }}
                    </span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-gray-600">處理時間</span>
                    <span class="font-medium">
                      {{ formatProcessingTime(partialResult?.processingTime) }}
                    </span>
                  </div>
                </div>
              </div>

              <!-- 品質分數 -->
              <div class="bg-white rounded-lg p-4 shadow-sm">
                <h4 class="font-medium text-gray-800 mb-2">品質分數</h4>
                <div class="space-y-3">
                  <!-- 完整性分數 -->
                  <div>
                    <div class="flex justify-between text-sm mb-1">
                      <span class="text-gray-600">完整性</span>
                      <span class="font-medium">
                        {{ ((partialResult?.quality?.completenessScore ?? 0) * 100).toFixed(0) }}%
                      </span>
                    </div>
                    <div class="w-full bg-gray-200 rounded-full h-2">
                      <div 
                        class="bg-blue-500 h-2 rounded-full transition-all duration-300"
                        :style="{ width: `${(partialResult?.quality?.completenessScore ?? 0) * 100}%` }"
                      ></div>
                    </div>
                  </div>

                  <!-- 連貫性分數 -->
                  <div>
                    <div class="flex justify-between text-sm mb-1">
                      <span class="text-gray-600">連貫性</span>
                      <span class="font-medium">
                        {{ ((partialResult?.quality?.coherenceScore ?? 0) * 100).toFixed(0) }}%
                      </span>
                    </div>
                    <div class="w-full bg-gray-200 rounded-full h-2">
                      <div 
                        class="bg-green-500 h-2 rounded-full transition-all duration-300"
                        :style="{ width: `${(partialResult?.quality?.coherenceScore ?? 0) * 100}%` }"
                      ></div>
                    </div>
                  </div>
                </div>
              </div>

              <!-- 總體品質等級 -->
              <div class="bg-white rounded-lg p-4 shadow-sm">
                <h4 class="font-medium text-gray-800 mb-2">總體品質</h4>
                <div class="flex items-center space-x-2">
                  <QualityBadge :quality="partialResult?.quality?.overallQuality" />
                  <span class="text-sm text-gray-600">
                    {{ getQualityText(partialResult?.quality?.overallQuality) }}
                  </span>
                </div>
                
                <!-- 推薦動作 -->
                <div class="mt-3 p-3 rounded-md" :class="getRecommendationClass(partialResult?.quality?.recommendedAction)">
                  <p class="text-sm font-medium">
                    {{ getRecommendationText(partialResult?.quality?.recommendedAction) }}
                  </p>
                </div>
              </div>

              <!-- 品質警告 -->
              <div v-if="partialResult?.quality?.qualityWarnings?.length" class="bg-white rounded-lg p-4 shadow-sm">
                <h4 class="font-medium text-gray-800 mb-2 flex items-center">
                  <ExclamationTriangleIcon class="h-4 w-4 text-amber-500 mr-1" />
                  品質提醒
                </h4>
                <ul class="space-y-1 text-sm text-gray-600">
                  <li v-for="warning in partialResult.quality.qualityWarnings" :key="warning" class="flex items-start">
                    <span class="text-amber-500 mr-1">•</span>
                    {{ warning }}
                  </li>
                </ul>
              </div>

              <!-- 遺漏主題 -->
              <div v-if="partialResult?.quality?.missingTopics?.length" class="bg-white rounded-lg p-4 shadow-sm">
                <h4 class="font-medium text-gray-800 mb-2">可能遺漏的主題</h4>
                <ul class="space-y-1 text-sm text-gray-600">
                  <li v-for="topic in partialResult.quality.missingTopics" :key="topic" class="flex items-start">
                    <span class="text-gray-400 mr-1">•</span>
                    {{ topic }}
                  </li>
                </ul>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- 對話框操作按鈕 -->
      <div class="px-6 py-4 border-t border-gray-200 bg-gray-50">
        <div class="flex items-center justify-between">
          <!-- 左側：使用者評論輸入 -->
          <div class="flex-1 mr-4">
            <label class="block text-sm text-gray-600 mb-1">備註（可選）</label>
            <input
              v-model="userComment"
              type="text"
              placeholder="您可以為這個結果添加備註..."
              class="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            >
          </div>

          <!-- 右側：操作按鈕 -->
          <div class="flex space-x-3">
            <button
              @click="handleDiscard"
              :disabled="isProcessing"
              class="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-gray-500 focus:border-transparent disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {{ isProcessing ? '處理中...' : '丟棄結果' }}
            </button>
            
            <!-- 只有在品質不是太差的情況下才顯示繼續處理選項 -->
            <button
              v-if="canContinueProcessing"
              @click="handleContinue"
              :disabled="isProcessing"
              class="px-4 py-2 text-sm font-medium text-blue-700 bg-blue-50 border border-blue-200 rounded-md hover:bg-blue-100 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {{ isProcessing ? '處理中...' : '繼續處理' }}
            </button>

            <button
              @click="handleSave"
              :disabled="isProcessing"
              class="px-4 py-2 text-sm font-medium text-white bg-green-600 border border-transparent rounded-md hover:bg-green-700 focus:outline-none focus:ring-2 focus:ring-green-500 focus:border-transparent disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {{ isProcessing ? '保存中...' : '保存結果' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { 
  InformationCircleIcon, 
  XMarkIcon, 
  DocumentTextIcon,
  ExclamationTriangleIcon
} from '@heroicons/vue/24/outline'
import QualityBadge from './QualityBadge.vue'
import type { PartialResult, QualityLevel, RecommendedAction } from '@/types/progress'

// Props 定義
interface Props {
  isVisible: boolean
  partialResult?: PartialResult | null
}

const props = defineProps<Props>()

// Emits 定義
const emit = defineEmits<{
  save: [comment: string]
  discard: [comment: string]
  continue: [comment: string]
  close: []
}>()

// 響應式狀態
const userComment = ref('')
const isProcessing = ref(false)

// 計算屬性
const canContinueProcessing = computed(() => {
  if (!props.partialResult?.quality) return false
  
  // 如果品質等級是可接受以上，且完整性超過30%，則可以繼續處理
  const qualityLevel = props.partialResult.quality.overallQuality
  const completeness = props.partialResult.quality.completenessScore
  
  return (qualityLevel === 'Acceptable' || qualityLevel === 'Good' || qualityLevel === 'Excellent') && 
         completeness >= 0.3
})

// 方法
const handleSave = async () => {
  isProcessing.value = true
  try {
    emit('save', userComment.value)
  } finally {
    isProcessing.value = false
  }
}

const handleDiscard = async () => {
  isProcessing.value = true
  try {
    emit('discard', userComment.value)
  } finally {
    isProcessing.value = false
  }
}

const handleContinue = async () => {
  isProcessing.value = true
  try {
    emit('continue', userComment.value)
  } finally {
    isProcessing.value = false
  }
}

const handleClose = () => {
  if (!isProcessing.value) {
    emit('close')
  }
}

const formatProcessingTime = (timeSpan: string | undefined): string => {
  if (!timeSpan) return '未知'
  
  try {
    // 假設 timeSpan 是 "HH:MM:SS" 格式
    const parts = timeSpan.split(':')
    if (parts.length === 3) {
      const hours = parseInt(parts[0])
      const minutes = parseInt(parts[1])
      const seconds = parseInt(parts[2])
      
      if (hours > 0) {
        return `${hours} 小時 ${minutes} 分鐘`
      } else if (minutes > 0) {
        return `${minutes} 分鐘 ${seconds} 秒`
      } else {
        return `${seconds} 秒`
      }
    }
  } catch {
    // 忽略解析錯誤
  }
  
  return timeSpan
}

const getQualityText = (quality: QualityLevel | undefined): string => {
  switch (quality) {
    case 'Excellent': return '優秀'
    case 'Good': return '良好'
    case 'Acceptable': return '可接受'
    case 'Poor': return '較差'
    case 'Unusable': return '不可用'
    default: return '未知'
  }
}

const getRecommendationText = (action: RecommendedAction | undefined): string => {
  switch (action) {
    case 'Recommend': return '💚 建議保存此結果'
    case 'ReviewRequired': return '⚠️ 建議審查後決定'
    case 'ConsiderContinue': return '🔄 考慮繼續處理以改善品質'
    case 'Discard': return '❌ 建議丟棄此結果'
    default: return '❓ 需要手動判斷'
  }
}

const getRecommendationClass = (action: RecommendedAction | undefined): string => {
  switch (action) {
    case 'Recommend': return 'bg-green-50 text-green-800 border border-green-200'
    case 'ReviewRequired': return 'bg-yellow-50 text-yellow-800 border border-yellow-200'
    case 'ConsiderContinue': return 'bg-blue-50 text-blue-800 border border-blue-200'
    case 'Discard': return 'bg-red-50 text-red-800 border border-red-200'
    default: return 'bg-gray-50 text-gray-800 border border-gray-200'
  }
}
</script>

<style scoped>
/* 滾動條樣式 */
.overflow-y-auto::-webkit-scrollbar {
  width: 6px;
}

.overflow-y-auto::-webkit-scrollbar-track {
  background: #f1f1f1;
  border-radius: 3px;
}

.overflow-y-auto::-webkit-scrollbar-thumb {
  background: #c1c1c1;
  border-radius: 3px;
}

.overflow-y-auto::-webkit-scrollbar-thumb:hover {
  background: #a8a8a8;
}
</style>