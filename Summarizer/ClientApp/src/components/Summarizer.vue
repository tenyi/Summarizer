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
        
        <!-- 輸入方式選擇 -->
        <div class="flex justify-center mb-6">
          <div class="bg-gray-100 rounded-xl p-1 flex">
            <button 
              @click="setInputMode('text')"
              :class="[
                'px-6 py-2 rounded-lg font-medium transition-all duration-200',
                inputMode === 'text' 
                  ? 'bg-blue-600 text-white shadow-md' 
                  : 'text-gray-600 hover:text-gray-800'
              ]"
            >
              文字輸入
            </button>
            <button 
              @click="setInputMode('file')"
              :class="[
                'px-6 py-2 rounded-lg font-medium transition-all duration-200',
                inputMode === 'file' 
                  ? 'bg-blue-600 text-white shadow-md' 
                  : 'text-gray-600 hover:text-gray-800'
              ]"
            >
              檔案上傳
            </button>
          </div>
        </div>

        <!-- 文字輸入區域 -->
        <div v-if="inputMode === 'text'" class="space-y-4">
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

        <!-- 檔案上傳區域 -->
        <div v-if="inputMode === 'file'" class="space-y-4">
          <div class="border-2 border-dashed border-gray-300 rounded-xl p-8 text-center hover:border-blue-400 transition-colors duration-200">
            <div class="space-y-4">
              <svg class="mx-auto h-12 w-12 text-gray-400" stroke="currentColor" fill="none" viewBox="0 0 48 48">
                <path d="M28 8H12a4 4 0 00-4 4v20m32-12v8m0 0v8a4 4 0 01-4 4H12a4 4 0 01-4-4v-4m32-4l-3.172-3.172a4 4 0 00-5.656 0L28 28M8 32l9.172-9.172a4 4 0 015.656 0L28 28m0 0l4 4m4-24h8m-4-4v8m-12 4h.02" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
              </svg>
              <div>
                <label for="file-upload" class="cursor-pointer">
                  <span class="text-blue-600 font-medium hover:text-blue-500">點擊選擇檔案</span>
                  <span class="text-gray-500"> 或拖曳檔案至此處</span>
                </label>
                <input 
                  id="file-upload" 
                  ref="fileInput"
                  type="file" 
                  class="hidden" 
                  accept=".txt,.md,.rtf"
                  @change="onFileSelect"
                />
              </div>
              <p class="text-sm text-gray-500">
                支援格式：TXT, MD, RTF (最大 10MB)
              </p>
              <div v-if="uploadedFileName" class="mt-4 p-3 bg-green-50 border border-green-200 rounded-lg">
                <div class="flex items-center space-x-2 text-green-700">
                  <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"></path>
                  </svg>
                  <span class="font-medium">已選擇檔案：{{ uploadedFileName }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>
        
        <!-- 按鈕區域 -->
        <div class="flex justify-center">
          <button 
            @click="handleSummarize" 
            :disabled="loading || !canStartSummarize"
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
              <div class="flex items-center justify-between">
                <div class="flex items-center space-x-2 text-green-700">
                  <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"></path>
                  </svg>
                  <h3 class="font-semibold">摘要完成</h3>
                </div>
                <button 
                  @click="downloadSummary"
                  class="flex items-center space-x-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors duration-200 text-sm font-medium"
                >
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"></path>
                  </svg>
                  <span>下載摘要</span>
                </button>
              </div>
              <div class="bg-white rounded-lg p-4 border border-green-300 shadow-sm">
                <p class="text-gray-800 leading-relaxed whitespace-pre-wrap">{{ summary }}</p>
              </div>
              <div class="flex justify-between items-center text-sm text-gray-600 pt-2 border-t border-green-200">
                <span>原文 {{ getOriginalTextLength() }} 字元</span>
                <span>摘要 {{ summary.length }} 字元</span>
                <span class="text-green-600 font-medium">
                  壓縮率 {{ getCompressionRatio() }}%
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
import { ref, computed } from 'vue';
import { summarizeText as apiSummarizeText, uploadFileForSummary } from '../api/summarize';

const text = ref('');
const summary = ref('');
const loading = ref(false);
const error = ref<string | null>(null);
const uploadedFileName = ref<string | null>(null);
const inputMode = ref<'text' | 'file'>('text');
const selectedFile = ref<File | null>(null);
const fileInput = ref<HTMLInputElement | null>(null);
const originalTextLength = ref(0);

// 設定輸入模式
const setInputMode = (mode: 'text' | 'file') => {
  inputMode.value = mode;
  // 清除狀態
  text.value = '';
  summary.value = '';
  error.value = null;
  uploadedFileName.value = null;
  selectedFile.value = null;
  if (fileInput.value) {
    fileInput.value.value = '';
  }
};

// 檔案選擇處理
const onFileSelect = (event: Event) => {
  const target = event.target as HTMLInputElement;
  if (target.files && target.files.length > 0) {
    selectedFile.value = target.files[0];
    uploadedFileName.value = target.files[0].name;
    error.value = null;
  }
};

// 檢查是否可以開始摘要
const canStartSummarize = computed(() => {
  if (inputMode.value === 'text') {
    return text.value.length > 0;
  } else {
    return selectedFile.value !== null;
  }
});

// 統一的摘要處理函數
const handleSummarize = async () => {
  if (inputMode.value === 'text') {
    await summarizeText();
  } else {
    await summarizeFile();
  }
};

// 原有的文字摘要功能
const summarizeText = async () => {
  if (text.value.length === 0) {
    error.value = '請輸入要摘要的文字。';
    return;
  }

  loading.value = true;
  error.value = null;
  summary.value = '';
  originalTextLength.value = text.value.length;

  try {
    console.log('發送摘要請求，文字長度:', text.value.length);
    const response = await apiSummarizeText({ text: text.value });
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

// 檔案摘要功能
const summarizeFile = async () => {
  if (!selectedFile.value) {
    error.value = '請選擇一個檔案。';
    return;
  }

  loading.value = true;
  error.value = null;
  summary.value = '';

  try {
    console.log('發送檔案摘要請求，檔案名稱:', selectedFile.value.name);
    const response = await uploadFileForSummary(selectedFile.value);
    console.log('收到檔案摘要回應:', response);
    
    if (response.success) {
      summary.value = response.summary || '';
      originalTextLength.value = response.originalLength || 0;
      console.log('檔案摘要成功:', summary.value);
    } else {
      console.error('檔案摘要失敗:', response);
      error.value = response.error || '發生了未知錯誤。';
    }
  } catch (err) {
    console.error('檔案上傳 API 呼叫錯誤:', err);
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

// 下載摘要檔案
const downloadSummary = () => {
  if (!summary.value) {
    error.value = '沒有可下載的摘要內容。';
    return;
  }

  const blob = new Blob([summary.value], { type: 'text/plain;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  
  // 生成檔案名稱
  const now = new Date();
  const timestamp = now.toLocaleString('zh-TW').replace(/[/:]/g, '-').replace(/ /g, '_');
  const fileName = inputMode.value === 'file' && uploadedFileName.value 
    ? `摘要_${uploadedFileName.value.replace(/\.[^/.]+$/, '')}_${timestamp}.txt`
    : `摘要_${timestamp}.txt`;
  
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
};

// 取得原文長度（用於顯示）
const getOriginalTextLength = () => {
  if (inputMode.value === 'text') {
    return text.value.length;
  } else {
    return originalTextLength.value;
  }
};

// 計算壓縮率
const getCompressionRatio = () => {
  const originalLength = getOriginalTextLength();
  if (originalLength === 0 || !summary.value) return 0;
  return Math.round((1 - summary.value.length / originalLength) * 100);
};
</script>

<style scoped>
/* 使用 Tailwind CSS，無需自定義樣式 */
</style>
