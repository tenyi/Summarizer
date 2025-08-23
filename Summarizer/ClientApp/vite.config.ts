import serverOption from './serverOption'
import { fileURLToPath, URL } from 'node:url';
import tailwindcss from '@tailwindcss/vite';
import plugin from '@vitejs/plugin-vue';
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import fs from 'fs';
import path from 'path';
import child_process from 'child_process';
import { env } from 'process';

// https://vitejs.dev/config/
export default defineConfig({
  server : serverOption,
  plugins: [plugin(), tailwindcss()],
  base: './', // 將路徑設定為相對路徑
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url))
        }
    },
})
