//===============================================================
// 檔案：api/index.ts
// 說明：設定 API 呼叫的基礎配置，使用 axios 套件。
//===============================================================

import axios from 'axios';

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '/',
  headers: {
    'Content-Type': 'application/json'
  }
});

// 添加回應攔截器來處理錯誤和資料格式
apiClient.interceptors.response.use(
  (response) => {
    console.log('API Response:', response.data);
    return response.data; // 直接返回 data，不是整個 response
  },
  (error) => {
    console.error('API Error:', error);
    if (error.response) {
      // 伺服器回應了錯誤狀態碼
      console.error('Error data:', error.response.data);
      console.error('Error status:', error.response.status);
      return Promise.reject(error.response.data);
    } else if (error.request) {
      // 請求已發出但沒有收到回應
      console.error('No response received:', error.request);
      return Promise.reject({ error: '無法連接到伺服器' });
    } else {
      // 其他錯誤
      console.error('Request setup error:', error.message);
      return Promise.reject({ error: '請求設定錯誤' });
    }
  }
);

export default apiClient;