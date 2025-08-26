<!-- 取消按鈕元件 - 支援不同樣式和狀態的取消操作 -->
<template>
  <button
    ref="buttonRef"
    :class="buttonClasses"
    :disabled="isDisabled || isLoading"
    @click="handleClick"
    @mouseenter="handleMouseEnter"
    @mouseleave="handleMouseLeave"
    :title="tooltip"
    :aria-label="ariaLabel"
    role="button"
    :tabindex="isDisabled ? -1 : 0"
  >
    <!-- 載入中指示器 -->
    <div v-if="isLoading" class="loading-spinner" :class="spinnerClasses">
      <div class="spinner-inner"></div>
    </div>
    
    <!-- 圖示 -->
    <div v-else-if="showIcon" class="button-icon" :class="iconClasses">
      <slot name="icon">
        <component :is="iconComponent" class="w-4 h-4" />
      </slot>
    </div>
    
    <!-- 按鈕文字 -->
    <span v-if="showText" class="button-text" :class="textClasses">
      {{ computedText }}
    </span>
    
    <!-- 進度指示 -->
    <div v-if="showProgress && progress > 0" class="progress-indicator">
      <div class="progress-fill" :style="{ width: `${progress}%` }"></div>
    </div>
  </button>
</template>

<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue'
import { XMarkIcon, ExclamationTriangleIcon, StopIcon } from '@heroicons/vue/24/outline'

/**
 * 取消按鈕變體類型
 */
export type CancelButtonVariant = 'default' | 'danger' | 'warning' | 'ghost' | 'outline'

/**
 * 按鈕尺寸類型
 */
export type CancelButtonSize = 'xs' | 'sm' | 'md' | 'lg' | 'xl'

/**
 * 取消按鈕狀態類型
 */
export type CancelButtonState = 'idle' | 'loading' | 'confirming' | 'success' | 'error'

interface Props {
  /** 按鈕變體樣式 */
  variant?: CancelButtonVariant
  /** 按鈕尺寸 */
  size?: CancelButtonSize
  /** 按鈕狀態 */
  state?: CancelButtonState
  /** 是否禁用 */
  disabled?: boolean
  /** 是否顯示圖示 */
  showIcon?: boolean
  /** 是否顯示文字 */
  showText?: boolean
  /** 自訂按鈕文字 */
  text?: string
  /** 工具提示文字 */
  tooltip?: string
  /** 無障礙標籤 */
  ariaLabel?: string
  /** 是否顯示進度指示 */
  showProgress?: boolean
  /** 進度百分比 (0-100) */
  progress?: number
  /** 確認延遲時間（毫秒） */
  confirmDelay?: number
  /** 是否需要雙重確認 */
  requireDoubleConfirm?: boolean
  /** 是否支援鍵盤操作 */
  keyboardSupport?: boolean
  /** 是否啟用動畫 */
  enableAnimations?: boolean
  /** 危險模式（紅色警告樣式） */
  dangerMode?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  variant: 'default',
  size: 'md',
  state: 'idle',
  disabled: false,
  showIcon: true,
  showText: true,
  text: '',
  tooltip: '取消處理',
  ariaLabel: '取消當前處理',
  showProgress: false,
  progress: 0,
  confirmDelay: 1000,
  requireDoubleConfirm: false,
  keyboardSupport: true,
  enableAnimations: true,
  dangerMode: false
})

const emit = defineEmits<{
  /** 點擊事件 */
  click: []
  /** 確認取消事件 */
  confirm: []
  /** 取消確認事件 */
  cancel: []
  /** 狀態變化事件 */
  stateChange: [state: CancelButtonState]
}>()

// 內部狀態
const buttonRef = ref<HTMLElement>()
const isHovered = ref(false)
const isPressed = ref(false)
const confirmTimeout = ref<ReturnType<typeof setTimeout> | null>(null)
const doubleClickCount = ref(0)

// 計算屬性
const isDisabled = computed(() => 
  props.disabled || props.state === 'success'
)

const isLoading = computed(() => 
  props.state === 'loading'
)

const computedText = computed(() => {
  if (props.text) return props.text
  
  switch (props.state) {
    case 'loading':
      return '取消中...'
    case 'confirming':
      return props.requireDoubleConfirm ? '再次點擊確認' : '確認取消'
    case 'success':
      return '已取消'
    case 'error':
      return '取消失敗'
    default:
      return '取消'
  }
})

const iconComponent = computed(() => {
  switch (props.state) {
    case 'confirming':
      return ExclamationTriangleIcon
    case 'error':
      return ExclamationTriangleIcon
    default:
      return props.variant === 'danger' || props.dangerMode ? StopIcon : XMarkIcon
  }
})

// 樣式計算
const buttonClasses = computed(() => [
  'cancel-button',
  `cancel-button--${props.variant}`,
  `cancel-button--${props.size}`,
  `cancel-button--${props.state}`,
  {
    'cancel-button--disabled': isDisabled.value,
    'cancel-button--loading': isLoading.value,
    'cancel-button--hovered': isHovered.value,
    'cancel-button--pressed': isPressed.value,
    'cancel-button--danger': props.dangerMode,
    'cancel-button--animated': props.enableAnimations,
    'cancel-button--progress': props.showProgress && props.progress > 0
  }
])

const iconClasses = computed(() => [
  'transition-all duration-200',
  {
    'text-red-500': props.dangerMode && isHovered.value,
    'scale-110': props.enableAnimations && isHovered.value
  }
])

const textClasses = computed(() => [
  'transition-all duration-200',
  {
    'text-red-600 font-medium': props.dangerMode && isHovered.value
  }
])

const spinnerClasses = computed(() => [
  {
    'text-white': props.variant === 'danger',
    'text-gray-600': props.variant !== 'danger'
  }
])

// 方法
const handleClick = async () => {
  if (isDisabled.value) return

  emit('click')

  // 處理雙重確認邏輯
  if (props.requireDoubleConfirm) {
    doubleClickCount.value++
    
    if (doubleClickCount.value === 1) {
      // 第一次點擊，等待第二次點擊
      emit('stateChange', 'confirming')
      
      setTimeout(() => {
        if (doubleClickCount.value === 1) {
          // 超時重置
          doubleClickCount.value = 0
          emit('stateChange', 'idle')
          emit('cancel')
        }
      }, props.confirmDelay)
      
      return
    } else if (doubleClickCount.value >= 2) {
      // 第二次點擊，確認操作
      doubleClickCount.value = 0
      emit('stateChange', 'loading')
      emit('confirm')
      return
    }
  }

  // 簡單確認模式
  if (props.state === 'idle') {
    emit('stateChange', 'confirming')
    
    // 延遲確認
    confirmTimeout.value = setTimeout(() => {
      emit('stateChange', 'loading')
      emit('confirm')
    }, props.confirmDelay)
  } else if (props.state === 'confirming') {
    // 立即確認
    if (confirmTimeout.value) {
      clearTimeout(confirmTimeout.value)
      confirmTimeout.value = null
    }
    
    emit('stateChange', 'loading')
    emit('confirm')
  }
}

const handleMouseEnter = () => {
  if (!isDisabled.value) {
    isHovered.value = true
  }
}

const handleMouseLeave = () => {
  isHovered.value = false
  isPressed.value = false
}

const handleKeyDown = (event: KeyboardEvent) => {
  if (!props.keyboardSupport) return
  
  if (event.code === 'Space' || event.code === 'Enter') {
    event.preventDefault()
    isPressed.value = true
    handleClick()
  } else if (event.code === 'Escape' && props.state === 'confirming') {
    // ESC 鍵取消確認
    if (confirmTimeout.value) {
      clearTimeout(confirmTimeout.value)
      confirmTimeout.value = null
    }
    
    doubleClickCount.value = 0
    emit('stateChange', 'idle')
    emit('cancel')
  }
}

const handleKeyUp = (event: KeyboardEvent) => {
  if (!props.keyboardSupport) return
  
  if (event.code === 'Space' || event.code === 'Enter') {
    isPressed.value = false
  }
}

// 清理函數
const cleanup = () => {
  if (confirmTimeout.value) {
    clearTimeout(confirmTimeout.value)
    confirmTimeout.value = null
  }
  
  doubleClickCount.value = 0
}

// 生命週期
onMounted(() => {
  if (props.keyboardSupport && buttonRef.value) {
    buttonRef.value.addEventListener('keydown', handleKeyDown)
    buttonRef.value.addEventListener('keyup', handleKeyUp)
  }
})

onUnmounted(() => {
  cleanup()
  
  if (props.keyboardSupport && buttonRef.value) {
    buttonRef.value.removeEventListener('keydown', handleKeyDown)
    buttonRef.value.removeEventListener('keyup', handleKeyUp)
  }
})

// 暴露方法給父元件
defineExpose({
  reset: () => {
    cleanup()
    emit('stateChange', 'idle')
  },
  setLoading: (loading: boolean) => {
    emit('stateChange', loading ? 'loading' : 'idle')
  },
  setSuccess: () => {
    cleanup()
    emit('stateChange', 'success')
  },
  setError: () => {
    cleanup()
    emit('stateChange', 'error')
  }
})
</script>

<style scoped>
/* 基礎按鈕樣式 */
.cancel-button {
  @apply relative inline-flex items-center justify-center gap-2 
         rounded-lg font-medium transition-all duration-200 
         focus:outline-none focus:ring-2 focus:ring-offset-2 
         active:scale-95 select-none;
}

/* 尺寸變體 */
.cancel-button--xs {
  @apply text-xs px-2 py-1;
}

.cancel-button--sm {
  @apply text-sm px-3 py-1.5;
}

.cancel-button--md {
  @apply text-sm px-4 py-2;
}

.cancel-button--lg {
  @apply text-base px-6 py-2.5;
}

.cancel-button--xl {
  @apply text-lg px-8 py-3;
}

/* 樣式變體 */
.cancel-button--default {
  @apply bg-gray-100 text-gray-700 border border-gray-300 
         hover:bg-gray-200 focus:ring-gray-500;
}

.cancel-button--danger {
  @apply bg-red-500 text-white border border-red-500 
         hover:bg-red-600 focus:ring-red-500 
         shadow-sm hover:shadow-md;
}

.cancel-button--warning {
  @apply bg-yellow-500 text-white border border-yellow-500 
         hover:bg-yellow-600 focus:ring-yellow-500;
}

.cancel-button--ghost {
  @apply bg-transparent text-gray-600 border-none 
         hover:bg-gray-100 focus:ring-gray-500;
}

.cancel-button--outline {
  @apply bg-transparent text-red-600 border border-red-300 
         hover:bg-red-50 focus:ring-red-500;
}

/* 狀態樣式 */
.cancel-button--loading {
  @apply cursor-wait opacity-75;
}

.cancel-button--confirming {
  @apply bg-amber-500 text-white border border-amber-500 
         hover:bg-amber-600 animate-pulse;
}

.cancel-button--success {
  @apply bg-green-500 text-white border border-green-500 
         cursor-default;
}

.cancel-button--error {
  @apply bg-red-600 text-white border border-red-600 
         hover:bg-red-700;
}

.cancel-button--disabled {
  @apply opacity-50 cursor-not-allowed pointer-events-none;
}

/* 動畫效果 */
.cancel-button--animated {
  @apply transition-all duration-300 ease-in-out;
}

.cancel-button--animated:hover {
  @apply transform -translate-y-0.5 shadow-lg;
}

.cancel-button--animated:active {
  @apply transform translate-y-0 shadow-sm;
}

/* 載入中動畫 */
.loading-spinner {
  @apply w-4 h-4;
}

.spinner-inner {
  @apply w-full h-full border-2 border-current border-t-transparent 
         rounded-full animate-spin;
}

/* 進度指示器 */
.cancel-button--progress {
  @apply overflow-hidden;
}

.progress-indicator {
  @apply absolute bottom-0 left-0 right-0 h-1 bg-black bg-opacity-10;
}

.progress-fill {
  @apply h-full bg-current opacity-50 transition-all duration-300;
}

/* 危險模式樣式 */
.cancel-button--danger.cancel-button--hovered {
  @apply shadow-red-200 shadow-lg;
}

/* 響應式設計 */
@media (max-width: 768px) {
  .cancel-button {
    @apply touch-manipulation;
  }
  
  .cancel-button--animated:hover {
    @apply transform-none shadow-none;
  }
}

/* 高對比度模式 */
@media (prefers-contrast: high) {
  .cancel-button {
    @apply border-2;
  }
  
  .cancel-button--danger {
    @apply border-red-700;
  }
}

/* 減少動畫模式 */
@media (prefers-reduced-motion: reduce) {
  .cancel-button--animated,
  .spinner-inner,
  .progress-fill {
    @apply transition-none animate-none;
  }
}

/* 深色模式 */
@media (prefers-color-scheme: dark) {
  .cancel-button--default {
    @apply bg-gray-700 text-gray-200 border-gray-600 
           hover:bg-gray-600;
  }
  
  .cancel-button--ghost {
    @apply text-gray-300 hover:bg-gray-700;
  }
  
  .cancel-button--outline {
    @apply text-red-400 border-red-400 hover:bg-red-900;
  }
}
</style>