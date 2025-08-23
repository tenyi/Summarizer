//===============================================================
// 檔案：api/summarize.ts
// 說明：提供呼叫後端摘要 API 的功能。
//===============================================================

import apiClient from './index';

interface SummarizeRequest {
  text: string;
  options?: {
    length?: 'short' | 'medium' | 'long';
    language?: string;
  };
}

interface SummarizeResponse {
  success: boolean;
  summary?: string;
  originalLength?: number;
  summaryLength?: number;
  processingTime?: number;
  error?: string;
  errorCode?: string;
}

export const summarizeText = (data: SummarizeRequest): Promise<SummarizeResponse> => {
  return apiClient.post('/api/summarize', data);
};
