<template>
  <div class="min-h-screen bg-gradient-to-br from-blue-50 via-slate-50 to-slate-100 p-4 md:p-8">
    <div class="max-w-4xl mx-auto">
      <div class="bg-white rounded-2xl shadow-xl border border-gray-100 p-6 md:p-8 space-y-8">
        <!-- 標題區域 -->
        <div class="text-center">
          <h1 class="text-3xl md:text-4xl font-bold bg-gradient-to-r from-blue-600 to-blue-800 bg-clip-text text-transparent mb-2">
            摘要小幫手
          </h1>
          <p class="text-gray-600 text-lg">輸入文字，AI 幫您快速生成精準摘要</p>
        </div>
        
        <!-- 輸入區域 -->
        <div class="space-y-4">
          <div class="relative">
            <textarea 
              v-model="text" 
              placeholder="請輸入要摘要的文字..."
              class="w-full h-48 p-4 border-2 border-gray-200 rounded-xl focus:border-blue-500 focus:ring-4 focus:ring-blue-100 transition-all duration-200 resize-none text-gray-700 placeholder-gray-400 text-base leading-relaxed"
            ></textarea>
            <div class="absolute bottom-3 right-3 text-sm text-gray-500 font-medium bg-white px-2 py-1 rounded-md shadow-sm">
              {{ text.length }} 字元
            </div>
          </div>
        </div>
        
        <!-- 按鈕區域 -->
        <div class="flex justify-center">
          <button 
            @click="summarize" 
            :disabled="loading || text.length === 0"
            class="relative bg-gradient-to-r from-blue-600 to-blue-700 hover:from-blue-700 hover:to-blue-800 text-white font-semibold py-3 px-8 rounded-xl shadow-lg hover:shadow-xl transform hover:-translate-y-0.5 transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed disabled:transform-none disabled:shadow-none min-w-32"
          >
            <span v-if="!loading">開始摘要</span>
            <span v-else class="flex items-center justify-center">
              <svg class="animate-spin -ml-1 mr-3 h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
              </svg>
              處理中...
            </span>
          </button>
        </div>
        
        <!-- 結果區域 -->
        <div class="space-y-4">
          <div 
            v-if="loading || error || summary" 
            :class="[
              'rounded-xl p-6 min-h-32 border transition-all duration-300',
              {
                'bg-gray-50 border-gray-200': !error && !summary,
                'bg-red-50 border-red-200': error,
                'bg-green-50 border-green-200': summary && !error
              }
            ]"
          >
            <!-- 載入狀態 -->
            <div v-if="loading" class="flex flex-col items-center justify-center space-y-4 text-gray-600">
              <svg class="animate-spin h-8 w-8 text-blue-600" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
              </svg>
              <p class="text-lg font-medium">AI 正在分析文字內容...</p>
              <p class="text-sm opacity-75">請稍候，這通常需要幾秒鐘</p>
            </div>
            
            <!-- 錯誤狀態 -->
            <div v-else-if="error" class="flex items-start space-x-3">
              <svg class="w-6 h-6 text-red-500 mt-0.5 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
              </svg>
              <div>
                <h3 class="font-semibold text-red-800 mb-1">處理失敗</h3>
                <p class="text-red-700">{{ error }}</p>
              </div>
            </div>
            
            <!-- 成功結果 -->
            <div v-else-if="summary" class="space-y-4">
              <div class="flex items-center space-x-2 text-green-700">
                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"></path>
                </svg>
                <h3 class="font-semibold">摘要完成</h3>
              </div>
              <div class="bg-white rounded-lg p-4 border border-green-300 shadow-sm">
                <p class="text-gray-800 leading-relaxed whitespace-pre-wrap">{{ summary }}</p>
              </div>
              <div class="flex justify-between items-center text-sm text-gray-600 pt-2 border-t border-green-200">
                <span>原文 {{ text.length }} 字元</span>
                <span>摘要 {{ summary.length }} 字元</span>
                <span class="text-green-600 font-medium">
                  壓縮率 {{ Math.round((1 - summary.length / text.length) * 100) }}%
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { summarizeText } from '../api/summarize';

const text = ref('');
const summary = ref('');
const loading = ref(false);
const error = ref<string | null>(null);

const summarize = async () => {
  if (text.value.length === 0) {
    error.value = '請輸入要摘要的文字。';
    return;
  }

  loading.value = true;
  error.value = null;
  summary.value = '';

  try {
    console.log('發送摘要請求，文字長度:', text.value.length);
    const response = await summarizeText({ text: text.value });
    console.log('收到回應:', response);
    
    if (response.success) {
      summary.value = response.summary || '';
      console.log('摘要成功:', summary.value);
    } else {
      console.error('摘要失敗:', response);
      error.value = response.error || '發生了未知錯誤。';
    }
  } catch (err) {
    console.error('API 呼叫錯誤:', err);
    if (err && typeof err === 'object' && 'error' in err) {
      error.value = String(err.error);
    } else if (err && typeof err === 'object' && 'message' in err) {
      error.value = String(err.message);
    } else {
      error.value = '無法連接到 API。';
    }
  } finally {
    loading.value = false;
  }
};
</script>

<style scoped>
/* 使用 Tailwind CSS，無需自定義樣式 */
</style>
