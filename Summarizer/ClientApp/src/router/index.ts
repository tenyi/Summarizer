import { createRouter, createWebHistory } from 'vue-router'
import Summarizer from '../components/Summarizer.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'summarizer',
      component: Summarizer
    }
  ]
})

export default router
