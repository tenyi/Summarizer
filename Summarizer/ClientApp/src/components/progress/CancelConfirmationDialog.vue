<template>
  <Teleport to="body">
    <div 
      v-if="isVisible" 
      class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50"
      role="dialog"
      aria-modal="true"
      aria-labelledby="cancel-dialog-title"
      aria-describedby="cancel-dialog-description"
      @click="handleBackdropClick"
      @keydown="handleKeydown"
    >
      <div 
        ref="dialogRef"
        class="bg-white rounded-lg p-6 max-w-md w-full mx-4 shadow-xl"
        @click.stop
        tabindex="-1"
      >
        <!-- Dialog Header -->
        <div class="flex items-center mb-4">
          <ExclamationTriangleIcon class="h-8 w-8 text-amber-500 mr-3" aria-hidden="true" />
          <h3 id="cancel-dialog-title" class="text-lg font-semibold text-gray-900">
            確認取消處理
          </h3>
        </div>
        
        <!-- Dialog Content -->
        <p id="cancel-dialog-description" class="text-gray-600 mb-4">
          您確定要取消目前的摘要處理嗎？
          <span v-if="completedSegments > 0">
            已處理的 {{ completedSegments }} / {{ totalSegments }} 個分段
            {{ savePartialResults ? '將會保存' : '將會遺失' }}。
          </span>
          <span v-else>
            處理尚未開始完成任何分段。
          </span>
        </p>
        
        <!-- Partial Results Option -->
        <div v-if="completedSegments > 0 && allowPartialResultSaving" class="mb-4">
          <label class="flex items-center cursor-pointer">
            <input 
              v-model="savePartialResults" 
              type="checkbox" 
              class="mr-2 h-4 w-4 text-blue-600 focus:ring-blue-500 border-gray-300 rounded"
              aria-describedby="partial-results-help"
            >
            <span class="text-sm text-gray-700">保存已完成的部分結果</span>
          </label>
          <p id="partial-results-help" class="text-xs text-gray-500 mt-1 ml-6">
            勾選此選項將保存已完成分段的摘要結果
          </p>
        </div>
        
        <!-- Progress Information -->
        <div v-if="completedSegments > 0" class="bg-gray-50 rounded-lg p-3 mb-4">
          <div class="text-sm text-gray-600">
            <div class="flex justify-between items-center">
              <span>處理進度:</span>
              <span class="font-medium">{{ Math.round((completedSegments / totalSegments) * 100) }}%</span>
            </div>
            <div class="flex justify-between items-center mt-1">
              <span>已完成:</span>
              <span class="font-medium">{{ completedSegments }} / {{ totalSegments }} 分段</span>
            </div>
          </div>
        </div>
        
        <!-- Action Buttons -->
        <div class="flex space-x-3">
          <button
            ref="cancelButtonRef"
            @click="handleConfirmCancel"
            class="flex-1 bg-red-600 text-white py-2 px-4 rounded-md hover:bg-red-700 focus:outline-none focus:ring-2 focus:ring-red-500 focus:ring-offset-2 transition-colors"
            :aria-describedby="completedSegments > 0 && savePartialResults ? 'cancel-with-save-help' : 'cancel-no-save-help'"
          >
            確認取消
          </button>
          <button
            ref="continueButtonRef"
            @click="handleClose"
            class="flex-1 bg-gray-300 text-gray-700 py-2 px-4 rounded-md hover:bg-gray-400 focus:outline-none focus:ring-2 focus:ring-gray-500 focus:ring-offset-2 transition-colors"
          >
            繼續處理
          </button>
        </div>
        
        <!-- Hidden assistive text -->
        <div class="sr-only">
          <span id="cancel-with-save-help">取消處理並保存已完成的結果</span>
          <span id="cancel-no-save-help">取消處理，不保存任何結果</span>
        </div>
        
        <!-- Keyboard shortcut hint -->
        <div class="mt-3 text-xs text-gray-500 text-center">
          按 ESC 鍵繼續處理，或使用 Tab 鍵導航
        </div>
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { ref, nextTick, onMounted, onUnmounted, computed } from 'vue'
import { ExclamationTriangleIcon } from '@heroicons/vue/24/outline'

/// <summary>
/// 取消確認對話框元件屬性
/// </summary>
interface Props {
  /// <summary>
  /// 對話框是否可見
  /// </summary>
  isVisible: boolean
  /// <summary>
  /// 已完成的分段數量
  /// </summary>
  completedSegments: number
  /// <summary>
  /// 總分段數量
  /// </summary>
  totalSegments: number
  /// <summary>
  /// 是否允許保存部分結果
  /// </summary>
  allowPartialResultSaving?: boolean
  /// <summary>
  /// 預設是否保存部分結果
  /// </summary>
  defaultSavePartialResults?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  allowPartialResultSaving: true,
  defaultSavePartialResults: false
})

/// <summary>
/// 對話框發出的事件
/// </summary>
const emit = defineEmits<{
  /// <summary>
  /// 確認取消事件
  /// </summary>
  cancel: [savePartialResults: boolean]
  /// <summary>
  /// 關閉對話框事件
  /// </summary>
  close: []
}>()

// Refs
const dialogRef = ref<HTMLElement>()
const cancelButtonRef = ref<HTMLButtonElement>()
const continueButtonRef = ref<HTMLButtonElement>()

// State
const savePartialResults = ref(props.defaultSavePartialResults)

// Computed
const hasCompletedSegments = computed(() => props.completedSegments > 0)

/// <summary>
/// 處理確認取消操作
/// </summary>
const handleConfirmCancel = () => {
  emit('cancel', savePartialResults.value)
}

/// <summary>
/// 處理關閉對話框操作
/// </summary>
const handleClose = () => {
  emit('close')
}

/// <summary>
/// 處理背景點擊事件
/// </summary>
const handleBackdropClick = (event: MouseEvent) => {
  // 防止誤操作，點擊背景不關閉對話框
  // 使用者必須明確選擇操作
  event.preventDefault()
}

/// <summary>
/// 處理鍵盤事件
/// </summary>
const handleKeydown = (event: KeyboardEvent) => {
  switch (event.key) {
    case 'Escape':
      event.preventDefault()
      handleClose()
      break
    case 'Tab':
      handleTabNavigation(event)
      break
    case 'Enter':
      // 如果焦點在按鈕上，觸發點擊
      const activeElement = document.activeElement as HTMLElement
      if (activeElement?.tagName === 'BUTTON') {
        event.preventDefault()
        activeElement.click()
      }
      break
  }
}

/// <summary>
/// 處理 Tab 鍵導航，確保焦點在對話框內循環
/// </summary>
const handleTabNavigation = (event: KeyboardEvent) => {
  if (!dialogRef.value) return

  const focusableElements = dialogRef.value.querySelectorAll(
    'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
  )
  const firstElement = focusableElements[0] as HTMLElement
  const lastElement = focusableElements[focusableElements.length - 1] as HTMLElement

  if (event.shiftKey) {
    // Shift + Tab：向前導航
    if (document.activeElement === firstElement) {
      event.preventDefault()
      lastElement.focus()
    }
  } else {
    // Tab：向後導航
    if (document.activeElement === lastElement) {
      event.preventDefault()
      firstElement.focus()
    }
  }
}

/// <summary>
/// 設置初始焦點
/// </summary>
const setInitialFocus = async () => {
  await nextTick()
  if (props.isVisible && continueButtonRef.value) {
    // 預設焦點設定在「繼續處理」按鈕上，符合較安全的預設選項
    continueButtonRef.value.focus()
  }
}

/// <summary>
/// 監聽對話框可見性變化
/// </summary>
let previousActiveElement: Element | null = null

const handleVisibilityChange = async () => {
  if (props.isVisible) {
    // 對話框開啟時
    previousActiveElement = document.activeElement
    document.body.style.overflow = 'hidden' // 防止背景滾動
    await setInitialFocus()
  } else {
    // 對話框關閉時
    document.body.style.overflow = '' // 恢復滾動
    if (previousActiveElement) {
      (previousActiveElement as HTMLElement).focus?.()
    }
  }
}

// Watch for visibility changes
onMounted(() => {
  if (props.isVisible) {
    handleVisibilityChange()
  }
})

onUnmounted(() => {
  document.body.style.overflow = '' // 清理樣式
})

// Watch prop changes
import { watch } from 'vue'
watch(() => props.isVisible, handleVisibilityChange)
</script>

<style scoped>
/* 確保螢幕閱讀器可以讀取但視覺上隱藏的內容 */
.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}

/* 高對比度支援 */
@media (prefers-contrast: high) {
  .bg-gray-50 {
    @apply bg-gray-100;
  }
  
  .text-gray-600 {
    @apply text-gray-800;
  }
  
  .text-gray-500 {
    @apply text-gray-700;
  }
}

/* 減少動畫偏好支援 */
@media (prefers-reduced-motion: reduce) {
  .transition-colors {
    transition: none;
  }
}

/* 自定義焦點樣式 */
button:focus-visible {
  outline: 2px solid currentColor;
  outline-offset: 2px;
}

input:focus-visible {
  outline: 2px solid #3b82f6;
  outline-offset: 2px;
}
</style>