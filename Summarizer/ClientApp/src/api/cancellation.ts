//===============================================================
// 檔案：api/cancellation.ts
// 說明：提供呼叫後端取消和恢復 API 的功能。
//===============================================================

import apiClient from './index';
import { CancellationReason } from '../types/progress';
import type { 
  CancellationRequest, 
  CancellationResult,
  SystemRecoveryResult,
  SystemHealthCheckResult,
  SelfRepairResult,
  RecoveryStatus,
  RecoveryReason,
  ProcessingError
} from '../types/progress';

/**
 * 取消批次處理（增強版本，支援部分結果保存）
 */
export const cancelBatchProcessing = async (
  batchId: string, 
  request: CancellationRequest
): Promise<CancellationResult> => {
  return apiClient.post(`/api/summarize/cancel/${batchId}`, request);
};

/**
 * 取消批次處理（簡化版本，保持向後相容性）
 */
export const cancelBatchProcessingLegacy = async (batchId: string): Promise<{ success: boolean; data: { message: string } }> => {
  return apiClient.post(`/api/summarize/batch/${batchId}/cancel`);
};

/**
 * 執行系統恢復
 */
export const recoverSystem = async (
  batchId: string, 
  reason: RecoveryReason = 'UserRequested'
): Promise<SystemRecoveryResult> => {
  return apiClient.post(`/api/summarize/recovery/${batchId}?reason=${reason}`);
};

/**
 * 執行系統健康檢查
 */
export const performSystemHealthCheck = async (): Promise<SystemHealthCheckResult> => {
  return apiClient.get('/api/summarize/health/system');
};

/**
 * 執行系統自我修復
 */
export const performSelfRepair = async (): Promise<SelfRepairResult> => {
  return apiClient.post('/api/summarize/health/self-repair');
};

/**
 * 重置系統狀態
 */
export const resetSystemState = async (
  resetType: 'ui' | 'batch' | 'resources' = 'ui',
  batchId?: string
): Promise<{ success: boolean; data: { message: string } }> => {
  const params = new URLSearchParams({ resetType });
  if (batchId) {
    params.append('batchId', batchId);
  }
  
  return apiClient.post(`/api/summarize/reset?${params}`);
};

/**
 * 取得恢復狀態
 */
export const getRecoveryStatus = async (batchId: string): Promise<RecoveryStatus> => {
  return apiClient.get(`/api/summarize/recovery/${batchId}/status`);
};

/**
 * 建立取消請求物件的輔助函數
 */
export const createCancellationRequest = (
  batchId: string,
  savePartialResults: boolean = false,
  requestedBy: string = 'User'
): CancellationRequest => {
  return {
    batchId,
    reason: CancellationReason.UserRequested,
    savePartialResults,
    requestedBy,
    requestedAt: new Date().toISOString()
  };
};

/**
 * 處理取消操作的完整流程
 */
export const handleCancellationFlow = async (
  batchId: string,
  savePartialResults: boolean = false
): Promise<CancellationResult> => {
  const request = createCancellationRequest(batchId, savePartialResults);
  
  try {
    const result = await cancelBatchProcessing(batchId, request);
    
    // 如果取消成功，可能需要執行系統清理
    if (result.isSuccessful) {
      try {
        await resetSystemState('ui', batchId);
      } catch (cleanupError) {
        console.warn('UI 狀態清理失敗，但取消操作已成功', cleanupError);
      }
    }
    
    return result;
  } catch (error) {
    console.error('取消操作失敗', error);
    throw error;
  }
};

/**
 * 處理系統恢復的完整流程
 */
export const handleRecoveryFlow = async (
  batchId: string,
  reason: RecoveryReason = 'UserRequested'
): Promise<SystemRecoveryResult> => {
  try {
    // 先檢查系統健康狀態
    const healthCheck = await performSystemHealthCheck();
    
    if (!healthCheck.isHealthy) {
      console.warn('系統健康檢查發現問題，繼續進行恢復', healthCheck.issues);
    }
    
    // 執行系統恢復
    const recoveryResult = await recoverSystem(batchId, reason);
    
    // 如果恢復成功，執行後續清理
    if (recoveryResult.isSuccessful) {
      try {
        await resetSystemState('resources', batchId);
      } catch (cleanupError) {
        console.warn('資源清理失敗，但恢復操作已成功', cleanupError);
      }
    }
    
    return recoveryResult;
  } catch (error) {
    console.error('系統恢復流程失敗', error);
    throw error;
  }
};

/**
 * 處理錯誤恢復的完整流程
 */
export const handleErrorRecovery = async (
  batchId: string,
  error: ProcessingError
): Promise<boolean> => {
  try {
    if (!error.isRecoverable) {
      console.warn('錯誤不可恢復，跳過自動恢復', error);
      return false;
    }
    
    // 根據錯誤嚴重程度決定恢復策略
    switch (error.severity) {
      case 'Low':
      case 'Medium':
        // 嘗試輕量級恢復
        await resetSystemState('ui', batchId);
        break;
        
      case 'High':
        // 嘗試完整恢復
        await handleRecoveryFlow(batchId, 'ErrorRecovery');
        break;
        
      case 'Critical':
        // 執行自我修復
        await performSelfRepair();
        await handleRecoveryFlow(batchId, 'SystemFailure');
        break;
    }
    
    return true;
  } catch (recoveryError) {
    console.error('錯誤恢復處理失敗', recoveryError);
    return false;
  }
};