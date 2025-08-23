<template>
  <div class="home">
    <h1>Welcome to Summarizer</h1>
    <p v-if="error" class="error">{{ error }}</p>
    <p>API Status: {{ apiStatus }}</p>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { checkHealth } from '../api/health';

const apiStatus = ref('checking...');
const error = ref<string | null>(null);

onMounted(async () => {
  try {
    const response = await checkHealth();
    if (response.data.status === 'healthy') {
      apiStatus.value = 'OK';
    } else {
      apiStatus.value = 'Error';
      error.value = 'API returned an unhealthy status.';
    }
  } catch (err) {
    console.error(err);
    apiStatus.value = 'Error';
    error.value = 'Failed to connect to the API.';
  }
});
</script>

<style scoped>
.home {
  text-align: center;
  margin-top: 50px;
}
.error {
  color: red;
}
</style>
