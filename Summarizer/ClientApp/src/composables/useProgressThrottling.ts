// 進度更新防抖和節流處理

import { ref, type Ref } from 'vue'
import type { ProcessingProgress } from '@/types/progress'

/**
 * 防抖函數
 */
function debounce<T extends (...args: any[]) => any>(
  func: T, 
  delay: number
): T {
  let timeoutId: ReturnType<typeof setTimeout>
  
  return ((...args: Parameters<T>) => {
    clearTimeout(timeoutId)
    timeoutId = setTimeout(() => func(...args), delay)
  }) as T
}

/**
 * 節流函數
 */
function throttle<T extends (...args: any[]) => any>(
  func: T, 
  delay: number
): T {
  let lastCall = 0
  
  return ((...args: Parameters<T>) => {
    const now = Date.now()
    if (now - lastCall >= delay) {
      lastCall = now
      return func(...args)
    }
  }) as T
}

/**
 * 進度更新的防抖和節流處理組合式函數
 */
export function useProgressThrottling(options: {
  progressThrottleMs?: number      // 進度更新節流間隔
  timeEstimationDebounceMs?: number // 時間預估更新防抖延遲
  uiUpdateThrottleMs?: number      // UI 更新節流間隔
} = {}) {
  
  const {
    progressThrottleMs = 500,
    timeEstimationDebounceMs = 1000,
    uiUpdateThrottleMs = 300
  } = options

  // 最後更新的進度資料
  const lastProgress: Ref<ProcessingProgress | null> = ref(null)
  const lastUpdateTime = ref(0)
  
  // 節流的進度更新函數
  const throttledProgressUpdate = throttle((progress: ProcessingProgress, callback: (progress: ProcessingProgress) => void) => {
    // 檢查是否需要更新（避免重複更新相同的進度值）
    if (lastProgress.value && 
        Math.abs(lastProgress.value.overallProgress - progress.overallProgress) < 0.1 &&
        lastProgress.value.currentStage === progress.currentStage) {
      return
    }
    
    lastProgress.value = progress
    lastUpdateTime.value = Date.now()
    callback(progress)
  }, progressThrottleMs)
  
  // 防抖的時間預估更新
  const debouncedTimeUpdate = debounce((progress: ProcessingProgress, callback: (progress: ProcessingProgress) => void) => {
    callback(progress)
  }, timeEstimationDebounceMs)
  
  // 節流的 UI 更新
  const throttledUIUpdate = throttle((updateFn: () => void) => {
    updateFn()
  }, uiUpdateThrottleMs)
  
  // 批量進度更新處理
  const batchProgressUpdates = (() => {
    const pendingUpdates: ProcessingProgress[] = []
    let batchTimeout: ReturnType<typeof setTimeout> | null = null
    
    return (progress: ProcessingProgress, callback: (progress: ProcessingProgress) => void) => {
      pendingUpdates.push(progress)
      
      if (batchTimeout) {
        clearTimeout(batchTimeout)
      }
      
      batchTimeout = setTimeout(() => {
        // 取得最新的進度資料
        const latestProgress = pendingUpdates[pendingUpdates.length - 1]
        pendingUpdates.length = 0 // 清空待處理列表
        
        callback(latestProgress)
        batchTimeout = null
      }, 100) // 100ms 批處理間隔
    }
  })()
  
  // 智能更新決策 - 根據進度變化幅度決定更新頻率
  const smartProgressUpdate = (progress: ProcessingProgress, callback: (progress: ProcessingProgress) => void) => {
    const now = Date.now()
    const timeSinceLastUpdate = now - lastUpdateTime.value
    
    if (!lastProgress.value) {
      // 首次更新，立即執行
      lastProgress.value = progress
      lastUpdateTime.value = now
      callback(progress)
      return
    }
    
    const progressDelta = Math.abs(progress.overallProgress - lastProgress.value.overallProgress)
    const stageChanged = progress.currentStage !== lastProgress.value.currentStage
    
    // 決定更新策略
    if (stageChanged || progressDelta >= 5 || timeSinceLastUpdate >= 2000) {
      // 階段變化、進度變化超過 5% 或超過 2 秒，立即更新
      lastProgress.value = progress
      lastUpdateTime.value = now
      callback(progress)
    } else if (progressDelta >= 1) {
      // 進度變化超過 1%，使用節流更新
      throttledProgressUpdate(progress, callback)
    } else {
      // 小幅變化，使用批量更新
      batchProgressUpdates(progress, callback)
    }
  }
  
  // 記憶體清理函數
  const cleanup = () => {
    lastProgress.value = null
    lastUpdateTime.value = 0
  }
  
  return {
    throttledProgressUpdate,
    debouncedTimeUpdate,
    throttledUIUpdate,
    smartProgressUpdate,
    batchProgressUpdates,
    cleanup,
    
    // 統計資訊
    getLastProgress: () => lastProgress.value,
    getLastUpdateTime: () => lastUpdateTime.value
  }
}

/**
 * 進度變化檢測組合式函數
 */
export function useProgressChangeDetection() {
  const previousProgress: Ref<ProcessingProgress | null> = ref(null)
  
  const detectSignificantChange = (currentProgress: ProcessingProgress): {
    hasSignificantChange: boolean
    changeType: 'stage' | 'major_progress' | 'completion' | 'minor' | 'none'
    changeDetails: {
      stageDelta?: string
      progressDelta: number
      timeDelta: number
    }
  } => {
    if (!previousProgress.value) {
      previousProgress.value = currentProgress
      return {
        hasSignificantChange: true,
        changeType: 'stage',
        changeDetails: {
          progressDelta: currentProgress.overallProgress,
          timeDelta: 0
        }
      }
    }
    
    const prev = previousProgress.value
    const progressDelta = Math.abs(currentProgress.overallProgress - prev.overallProgress)
    const timeDelta = new Date(currentProgress.lastUpdated).getTime() - new Date(prev.lastUpdated).getTime()
    const stageChanged = currentProgress.currentStage !== prev.currentStage
    
    let changeType: 'stage' | 'major_progress' | 'completion' | 'minor' | 'none' = 'none'
    let hasSignificantChange = false
    
    if (stageChanged) {
      changeType = 'stage'
      hasSignificantChange = true
    } else if (currentProgress.overallProgress >= 100) {
      changeType = 'completion'
      hasSignificantChange = true
    } else if (progressDelta >= 5) {
      changeType = 'major_progress'
      hasSignificantChange = true
    } else if (progressDelta >= 1) {
      changeType = 'minor'
      hasSignificantChange = timeDelta >= 1000 // 1 秒內的小變化可能不需要更新
    }
    
    if (hasSignificantChange) {
      previousProgress.value = currentProgress
    }
    
    return {
      hasSignificantChange,
      changeType,
      changeDetails: {
        stageDelta: stageChanged ? `${prev.currentStage} → ${currentProgress.currentStage}` : undefined,
        progressDelta,
        timeDelta
      }
    }
  }
  
  const reset = () => {
    previousProgress.value = null
  }
  
  return {
    detectSignificantChange,
    reset,
    getPreviousProgress: () => previousProgress.value
  }
}