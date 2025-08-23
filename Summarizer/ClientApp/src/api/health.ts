//===============================================================
// 檔案：api/health.ts
// 說明：提供呼叫後端健康檢查 API 的功能。
//===============================================================

import apiClient from './index';

export const checkHealth = () => {
  return apiClient.get('/api/health');
};
