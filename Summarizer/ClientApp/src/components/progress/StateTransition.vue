<!-- 狀態過渡動畫元件 - 提供統一的狀態轉換動畫效果 -->
<template>
  <Transition
    :name="transitionName"
    :appear="props.appear"
    :duration="props.duration"
    @before-enter="handleBeforeEnter"
    @after-enter="handleAfterEnter"
    @before-leave="handleBeforeLeave"
    @after-leave="handleAfterLeave"
  >
    <div 
      v-if="props.visible"
      ref="containerRef"
      :class="containerClasses"
      :style="containerStyles"
    >
      <!-- 背景效果層 -->
      <div 
        v-if="props.showBackgroundEffect"
        class="background-effect"
        :class="backgroundEffectClasses"
      ></div>
      
      <!-- 內容區域 -->
      <div class="content-wrapper" :class="contentClasses">
        <slot />
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue'

/**
 * 狀態類型定義
 */
export type TransitionState = 
  | 'idle'           // 閒置狀態
  | 'processing'     // 處理中
  | 'cancelling'     // 取消中
  | 'cancelled'      // 已取消
  | 'error'          // 錯誤狀態
  | 'recovering'     // 恢復中
  | 'success'        // 成功狀態
  | 'warning'        // 警告狀態

/**
 * 動畫效果類型
 */
export type AnimationEffect = 
  | 'fade'           // 淡入淡出
  | 'slide'          // 滑動
  | 'scale'          // 縮放
  | 'shake'          // 震動

/**
 * 動畫方向
 */
export type AnimationDirection = 'up' | 'down' | 'left' | 'right' | 'center'

interface Props {
  /** 是否可見 */
  visible?: boolean
  /** 當前狀態 */
  state?: TransitionState
  /** 動畫效果類型 */
  effect?: AnimationEffect
  /** 動畫方向 */
  direction?: AnimationDirection
  /** 動畫持續時間（毫秒） */
  duration?: number
  /** 是否在首次渲染時就顯示動畫 */
  appear?: boolean
  /** 是否啟用背景效果 */
  showBackgroundEffect?: boolean
  /** 是否顯示裝飾效果 */
  showDecorations?: boolean
  /** 是否啟用動畫 */
  enableAnimations?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  visible: true,
  state: 'idle',
  effect: 'fade',
  direction: 'center',
  duration: 300,
  appear: true,
  showBackgroundEffect: false,
  showDecorations: false,
  enableAnimations: true
})

const emit = defineEmits<{
  /** 過渡開始前 */
  'before-enter': [el: Element]
  /** 過渡完成 */
  'after-enter': [el: Element]
  /** 離開過渡開始前 */
  'before-leave': [el: Element]
  /** 離開過渡完成 */
  'after-leave': [el: Element]
}>()

// 內部狀態
const containerRef = ref<HTMLElement>()

// 檢測用戶動畫偏好
const prefersReducedMotion = ref(false)

// 計算屬性
const shouldAnimate = computed(() => {
  return props.enableAnimations && !prefersReducedMotion.value
})

const transitionName = computed(() => {
  if (!shouldAnimate.value) return 'no-animation'
  
  return `state-transition-${props.effect}-${props.direction}`
})

const containerClasses = computed(() => [
  'state-transition-container',
  `state-transition-container--${props.state}`,
  `state-transition-container--${props.effect}`,
  {
    'state-transition-container--reduced-motion': !shouldAnimate.value
  }
])

const containerStyles = computed(() => ({
  '--transition-duration': `${props.duration}ms`
}))

const backgroundEffectClasses = computed(() => [
  'background-effect-base',
  `background-effect--${props.state}`,
  {
    'background-effect--animated': shouldAnimate.value
  }
])

const contentClasses = computed(() => [
  'content-wrapper-base',
  `content--${props.state}`
])

// 方法
const handleBeforeEnter = (el: Element) => {
  emit('before-enter', el)
}

const handleAfterEnter = (el: Element) => {
  emit('after-enter', el)
}

const handleBeforeLeave = (el: Element) => {
  emit('before-leave', el)
}

const handleAfterLeave = (el: Element) => {
  emit('after-leave', el)
}

const detectMotionPreference = () => {
  if (typeof window !== 'undefined' && window.matchMedia) {
    const mediaQuery = window.matchMedia('(prefers-reduced-motion: reduce)')
    prefersReducedMotion.value = mediaQuery.matches
    
    mediaQuery.addEventListener('change', (e) => {
      prefersReducedMotion.value = e.matches
    })
  }
}

// 生命週期
onMounted(() => {
  detectMotionPreference()
})
</script>

<style scoped>
/* 基礎容器樣式 */
.state-transition-container {
  @apply relative overflow-hidden transition-all duration-300;
}

.state-transition-container--reduced-motion {
  transition: none !important;
  animation: none !important;
}

/* 狀態特定樣式 */
.state-transition-container--processing {
  @apply border-blue-300 bg-blue-50;
}

.state-transition-container--error {
  @apply border-red-300 bg-red-50;
}

.state-transition-container--success {
  @apply border-green-300 bg-green-50;
}

.state-transition-container--recovering {
  @apply border-green-300 bg-green-50;
}

/* 背景效果 */
.background-effect-base {
  @apply absolute inset-0 pointer-events-none;
}

.background-effect--animated {
  animation: backgroundPulse 2s ease-in-out infinite;
}

.background-effect--processing {
  @apply bg-gradient-to-r from-blue-100 to-blue-200;
}

.background-effect--error {
  @apply bg-gradient-to-r from-red-100 to-red-200;
}

.background-effect--success {
  @apply bg-gradient-to-r from-green-100 to-green-200;
}

/* 內容包裝器 */
.content-wrapper-base {
  @apply relative z-10 transition-all duration-300;
}

/* 淡入淡出動畫 */
.state-transition-fade-center-enter-active,
.state-transition-fade-center-leave-active {
  transition: opacity 300ms ease;
}

.state-transition-fade-center-enter-from,
.state-transition-fade-center-leave-to {
  opacity: 0;
}

/* 滑動動畫 */
.state-transition-slide-up-enter-active,
.state-transition-slide-up-leave-active {
  transition: all 300ms ease;
}

.state-transition-slide-up-enter-from {
  opacity: 0;
  transform: translateY(20px);
}

.state-transition-slide-up-leave-to {
  opacity: 0;
  transform: translateY(-20px);
}

/* 縮放動畫 */
.state-transition-scale-center-enter-active,
.state-transition-scale-center-leave-active {
  transition: all 300ms ease;
}

.state-transition-scale-center-enter-from {
  opacity: 0;
  transform: scale(0.95);
}

.state-transition-scale-center-leave-to {
  opacity: 0;
  transform: scale(1.05);
}

/* 震動動畫 */
.state-transition-shake-center-enter-active {
  animation: shake 0.5s ease-in-out;
}

/* 動畫關鍵幀 */
@keyframes backgroundPulse {
  0%, 100% {
    opacity: 0.3;
  }
  50% {
    opacity: 0.5;
  }
}

@keyframes shake {
  0%, 100% {
    transform: translateX(0);
  }
  10%, 30%, 50%, 70%, 90% {
    transform: translateX(-3px);
  }
  20%, 40%, 60%, 80% {
    transform: translateX(3px);
  }
}

/* 無動畫模式 */
.no-animation-enter-active,
.no-animation-leave-active {
  transition: none !important;
  animation: none !important;
}

/* 減少動畫偏好 */
@media (prefers-reduced-motion: reduce) {
  .state-transition-container,
  .background-effect--animated {
    transition: none !important;
    animation: none !important;
  }
}

/* 深色模式 */
@media (prefers-color-scheme: dark) {
  .state-transition-container--processing {
    @apply border-blue-600 bg-blue-900;
  }
  
  .state-transition-container--error {
    @apply border-red-600 bg-red-900;
  }
  
  .state-transition-container--success {
    @apply border-green-600 bg-green-900;
  }
  
  .background-effect--processing {
    @apply bg-gradient-to-r from-blue-900 to-blue-800;
  }
  
  .background-effect--error {
    @apply bg-gradient-to-r from-red-900 to-red-800;
  }
  
  .background-effect--success {
    @apply bg-gradient-to-r from-green-900 to-green-800;
  }
}
</style>