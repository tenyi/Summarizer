// SignalR 客戶端連線管理組合式函數

import { ref, onUnmounted, type Ref } from 'vue'
import * as signalR from '@microsoft/signalr'
import type { 
  ProcessingProgress, 
  SegmentStatus, 
  ProcessingStage,
  ProgressUpdateEvent 
} from '@/types/progress'

/**
 * SignalR 連線狀態
 */
export enum ConnectionState {
  Disconnected = 'disconnected',
  Connecting = 'connecting', 
  Connected = 'connected',
  Reconnecting = 'reconnecting',
  Disconnecting = 'disconnecting'
}

/**
 * SignalR 連線選項
 */
interface SignalROptions {
  hubUrl?: string                         // Hub URL，預設為 '/batchProgressHub'
  automaticReconnect?: boolean            // 是否自動重連
  reconnectIntervals?: number[]           // 重連間隔（毫秒）
  maxReconnectAttempts?: number           // 最大重連次數
  enableLogging?: boolean                 // 是否啟用日誌
  enableHeartbeat?: boolean               // 是否啟用心跳檢測
  heartbeatInterval?: number              // 心跳間隔（毫秒）
}

/**
 * 事件回調函數類型
 */
interface SignalREventHandlers {
  onProgressUpdate?: (progress: ProcessingProgress) => void
  onSegmentStatusUpdate?: (segment: SegmentStatus) => void
  onStageChanged?: (stage: ProcessingStage, info?: any) => void
  onBatchCompleted?: (batchId: string, result: any) => void
  onBatchFailed?: (batchId: string, error: string) => void
  onConnectionStateChanged?: (state: ConnectionState) => void
  onSystemStatusUpdate?: (message: string, timestamp: string) => void
  onError?: (error: Error) => void
}

/**
 * SignalR 客戶端管理組合式函數
 */
export function useSignalR(options: SignalROptions = {}) {
  // 預設選項
  const defaultOptions: Required<SignalROptions> = {
    hubUrl: '/batchProgressHub',
    automaticReconnect: true,
    reconnectIntervals: [0, 2000, 10000, 30000],
    maxReconnectAttempts: 5,
    enableLogging: import.meta.env.DEV,
    enableHeartbeat: true,
    heartbeatInterval: 30000 // 30秒
  }
  
  const config = { ...defaultOptions, ...options }
  
  // 響應式狀態
  const connection: Ref<signalR.HubConnection | null> = ref(null)
  const connectionState = ref<ConnectionState>(ConnectionState.Disconnected)
  const isConnected = ref(false)
  const currentBatchId = ref<string | null>(null)
  const reconnectAttempts = ref(0)
  const lastHeartbeat = ref<Date | null>(null)
  const connectionId = ref<string | null>(null)
  
  // 事件處理器儲存
  const eventHandlers = ref<SignalREventHandlers>({})
  
  // 心跳計時器
  let heartbeatTimer: ReturnType<typeof setInterval> | null = null
  
  /**
   * 建立 SignalR 連線
   */
  const createConnection = () => {
    const hubUrl = `${window.location.origin}${config.hubUrl}`
    
    const builder = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
    
    // 設定自動重連
    if (config.automaticReconnect) {
      builder.withAutomaticReconnect(config.reconnectIntervals)
    }
    
    // 設定日誌等級
    if (config.enableLogging) {
      builder.configureLogging(signalR.LogLevel.Information)
    } else {
      builder.configureLogging(signalR.LogLevel.Error)
    }
    
    return builder.build()
  }
  
  /**
   * 設定事件監聽器
   */
  const setupEventListeners = (conn: signalR.HubConnection) => {
    // 連線狀態事件
    conn.onclose((error?: Error) => {
      connectionState.value = ConnectionState.Disconnected
      isConnected.value = false
      connectionId.value = null
      
      if (config.enableLogging) {
        console.log('[SignalR] Connection closed', error)
      }
      
      eventHandlers.value.onConnectionStateChanged?.(ConnectionState.Disconnected)
      
      if (error) {
        eventHandlers.value.onError?.(error instanceof Error ? error : new Error(String(error)))
      }
    })
    
    conn.onreconnecting((error?: Error) => {
      connectionState.value = ConnectionState.Reconnecting
      isConnected.value = false
      reconnectAttempts.value += 1
      
      if (config.enableLogging) {
        console.log(`[SignalR] Reconnecting... Attempt ${reconnectAttempts.value}`, error)
      }
      
      eventHandlers.value.onConnectionStateChanged?.(ConnectionState.Reconnecting)
    })
    
    conn.onreconnected((connectionId?: string) => {
      connectionState.value = ConnectionState.Connected
      isConnected.value = true
      reconnectAttempts.value = 0
      
      if (config.enableLogging) {
        console.log('[SignalR] Reconnected', connectionId)
      }
      
      eventHandlers.value.onConnectionStateChanged?.(ConnectionState.Connected)
      
      // 重連後重新加入批次群組
      if (currentBatchId.value) {
        joinBatchGroup(currentBatchId.value)
      }
    })
    
    // 業務事件監聽
    conn.on('Connected', (connId: string, timestamp: string) => {
      connectionId.value = connId
      if (config.enableLogging) {
        console.log('[SignalR] Welcome message received', connId, timestamp)
      }
    })
    
    conn.on('ProgressUpdate', (progress: ProcessingProgress) => {
      eventHandlers.value.onProgressUpdate?.(progress)
    })
    
    conn.on('SegmentStatusUpdate', (segment: SegmentStatus) => {
      eventHandlers.value.onSegmentStatusUpdate?.(segment)
    })
    
    conn.on('StageChanged', (stage: ProcessingStage, info?: any) => {
      eventHandlers.value.onStageChanged?.(stage, info)
    })
    
    conn.on('BatchCompleted', (batchId: string, result: any) => {
      eventHandlers.value.onBatchCompleted?.(batchId, result)
    })
    
    conn.on('BatchFailed', (batchId: string, error: string) => {
      eventHandlers.value.onBatchFailed?.(batchId, error)
    })
    
    conn.on('SystemStatusUpdate', (message: string, timestamp: string) => {
      eventHandlers.value.onSystemStatusUpdate?.(message, timestamp)
    })
    
    // 群組管理事件
    conn.on('JoinedBatchGroup', (batchId: string) => {
      if (config.enableLogging) {
        console.log(`[SignalR] Joined batch group: ${batchId}`)
      }
    })
    
    conn.on('LeftBatchGroup', (batchId: string) => {
      if (config.enableLogging) {
        console.log(`[SignalR] Left batch group: ${batchId}`)
      }
    })
    
    // 心跳響應
    conn.on('Pong', (timestamp: string) => {
      lastHeartbeat.value = new Date(timestamp)
    })
  }
  
  /**
   * 啟動心跳檢測
   */
  const startHeartbeat = () => {
    if (!config.enableHeartbeat || heartbeatTimer) return
    
    heartbeatTimer = setInterval(async () => {
      try {
        if (connection.value?.state === signalR.HubConnectionState.Connected) {
          await connection.value.invoke('Ping')
        }
      } catch (error) {
        if (config.enableLogging) {
          console.warn('[SignalR] Heartbeat failed', error)
        }
      }
    }, config.heartbeatInterval)
  }
  
  /**
   * 停止心跳檢測
   */
  const stopHeartbeat = () => {
    if (heartbeatTimer) {
      clearInterval(heartbeatTimer)
      heartbeatTimer = null
    }
  }
  
  /**
   * 啟動連線
   */
  const startConnection = async (): Promise<void> => {
    if (connection.value?.state === signalR.HubConnectionState.Connected) {
      return
    }
    
    try {
      connectionState.value = ConnectionState.Connecting
      
      if (!connection.value) {
        connection.value = createConnection()
        setupEventListeners(connection.value)
      }
      
      await connection.value.start()
      
      connectionState.value = ConnectionState.Connected
      isConnected.value = true
      reconnectAttempts.value = 0
      
      if (config.enableLogging) {
        console.log('[SignalR] Connection started successfully')
      }
      
      eventHandlers.value.onConnectionStateChanged?.(ConnectionState.Connected)
      
      // 啟動心跳
      startHeartbeat()
      
    } catch (error) {
      connectionState.value = ConnectionState.Disconnected
      isConnected.value = false
      
      const errorObj = error instanceof Error ? error : new Error(String(error))
      
      if (config.enableLogging) {
        console.error('[SignalR] Failed to start connection', errorObj)
      }
      
      eventHandlers.value.onError?.(errorObj)
      throw errorObj
    }
  }
  
  /**
   * 停止連線
   */
  const stopConnection = async (): Promise<void> => {
    if (!connection.value || connection.value.state === signalR.HubConnectionState.Disconnected) {
      return
    }
    
    try {
      connectionState.value = ConnectionState.Disconnecting
      
      // 停止心跳
      stopHeartbeat()
      
      // 離開當前批次群組
      if (currentBatchId.value) {
        await leaveBatchGroup(currentBatchId.value)
      }
      
      await connection.value.stop()
      
      connectionState.value = ConnectionState.Disconnected
      isConnected.value = false
      connectionId.value = null
      
      if (config.enableLogging) {
        console.log('[SignalR] Connection stopped')
      }
      
      eventHandlers.value.onConnectionStateChanged?.(ConnectionState.Disconnected)
      
    } catch (error) {
      const errorObj = error instanceof Error ? error : new Error(String(error))
      
      if (config.enableLogging) {
        console.error('[SignalR] Failed to stop connection', errorObj)
      }
      
      eventHandlers.value.onError?.(errorObj)
      throw errorObj
    }
  }
  
  /**
   * 加入批次群組
   */
  const joinBatchGroup = async (batchId: string): Promise<void> => {
    if (!connection.value || connection.value.state !== signalR.HubConnectionState.Connected) {
      throw new Error('SignalR connection is not established')
    }
    
    try {
      await connection.value.invoke('JoinBatchGroup', batchId)
      currentBatchId.value = batchId
      
      if (config.enableLogging) {
        console.log(`[SignalR] Joining batch group: ${batchId}`)
      }
    } catch (error) {
      const errorObj = error instanceof Error ? error : new Error(String(error))
      eventHandlers.value.onError?.(errorObj)
      throw errorObj
    }
  }
  
  /**
   * 離開批次群組
   */
  const leaveBatchGroup = async (batchId: string): Promise<void> => {
    if (!connection.value || connection.value.state !== signalR.HubConnectionState.Connected) {
      return // 連線已斷開，無需處理
    }
    
    try {
      await connection.value.invoke('LeaveBatchGroup', batchId)
      
      if (currentBatchId.value === batchId) {
        currentBatchId.value = null
      }
      
      if (config.enableLogging) {
        console.log(`[SignalR] Left batch group: ${batchId}`)
      }
    } catch (error) {
      const errorObj = error instanceof Error ? error : new Error(String(error))
      eventHandlers.value.onError?.(errorObj)
      throw errorObj
    }
  }
  
  /**
   * 請求進度更新
   */
  const requestProgressUpdate = async (batchId: string): Promise<void> => {
    if (!connection.value || connection.value.state !== signalR.HubConnectionState.Connected) {
      throw new Error('SignalR connection is not established')
    }
    
    try {
      await connection.value.invoke('RequestProgressUpdate', batchId)
    } catch (error) {
      const errorObj = error instanceof Error ? error : new Error(String(error))
      eventHandlers.value.onError?.(errorObj)
      throw errorObj
    }
  }
  
  /**
   * 設定事件處理器
   */
  const setEventHandlers = (handlers: Partial<SignalREventHandlers>) => {
    eventHandlers.value = { ...eventHandlers.value, ...handlers }
  }
  
  /**
   * 移除事件處理器
   */
  const removeEventHandler = (eventName: keyof SignalREventHandlers) => {
    delete eventHandlers.value[eventName]
  }
  
  /**
   * 取得連線統計資訊
   */
  const getConnectionStats = () => ({
    state: connectionState.value,
    isConnected: isConnected.value,
    connectionId: connectionId.value,
    currentBatchId: currentBatchId.value,
    reconnectAttempts: reconnectAttempts.value,
    lastHeartbeat: lastHeartbeat.value
  })
  
  // 組件卸載時清理資源
  onUnmounted(async () => {
    try {
      await stopConnection()
    } catch (error) {
      // 忽略卸載時的錯誤
    }
  })
  
  return {
    // 狀態
    connection: connection as Readonly<Ref<signalR.HubConnection | null>>,
    connectionState: connectionState as Readonly<Ref<ConnectionState>>,
    isConnected: isConnected as Readonly<Ref<boolean>>,
    currentBatchId: currentBatchId as Readonly<Ref<string | null>>,
    connectionId: connectionId as Readonly<Ref<string | null>>,
    
    // 方法
    startConnection,
    stopConnection,
    joinBatchGroup,
    leaveBatchGroup,
    requestProgressUpdate,
    setEventHandlers,
    removeEventHandler,
    getConnectionStats,
    
    // 工具方法
    isConnectionReady: () => connection.value?.state === signalR.HubConnectionState.Connected,
    reconnect: async () => {
      await stopConnection()
      await startConnection()
    }
  }
}