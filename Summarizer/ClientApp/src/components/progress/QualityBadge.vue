<template>
  <div 
    class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium"
    :class="badgeClass"
  >
    <component :is="iconComponent" class="h-3 w-3 mr-1" />
    {{ qualityText }}
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import {
  CheckCircleIcon,
  ExclamationCircleIcon,
  ExclamationTriangleIcon,
  XCircleIcon,
  QuestionMarkCircleIcon
} from '@heroicons/vue/24/solid'
import type { QualityLevel } from '@/types/progress'

// Props 定義
interface Props {
  quality?: QualityLevel | null
}

const props = defineProps<Props>()

// 計算屬性
const qualityText = computed(() => {
  switch (props.quality) {
    case 'Excellent': return '優秀'
    case 'Good': return '良好'
    case 'Acceptable': return '可接受'
    case 'Poor': return '較差'
    case 'Unusable': return '不可用'
    default: return '未知'
  }
})

const badgeClass = computed(() => {
  switch (props.quality) {
    case 'Excellent':
      return 'bg-green-100 text-green-800 border border-green-200'
    case 'Good':
      return 'bg-blue-100 text-blue-800 border border-blue-200'
    case 'Acceptable':
      return 'bg-yellow-100 text-yellow-800 border border-yellow-200'
    case 'Poor':
      return 'bg-orange-100 text-orange-800 border border-orange-200'
    case 'Unusable':
      return 'bg-red-100 text-red-800 border border-red-200'
    default:
      return 'bg-gray-100 text-gray-800 border border-gray-200'
  }
})

const iconComponent = computed(() => {
  switch (props.quality) {
    case 'Excellent':
      return CheckCircleIcon
    case 'Good':
      return CheckCircleIcon
    case 'Acceptable':
      return ExclamationCircleIcon
    case 'Poor':
      return ExclamationTriangleIcon
    case 'Unusable':
      return XCircleIcon
    default:
      return QuestionMarkCircleIcon
  }
})
</script>

<style scoped>
/* 無額外樣式需要，全部使用 Tailwind CSS */
</style>