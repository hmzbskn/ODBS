import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// Geliştirmede /api isteklerini backend'e yönlendirir (aynı origin: CORS gerekmez).
// Backend adresi VITE_API_TARGET ile değiştirilebilir.
const apiTarget = process.env.VITE_API_TARGET ?? 'http://localhost:5154'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: apiTarget, changeOrigin: true },
    },
  },
})
