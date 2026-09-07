import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    tailwindcss()
  ],
  server: {
    port: 5173,
    proxy: {
      '/query': 'http://localhost:5000',
      '/query_audio': 'http://localhost:5000',
      '/transcribe': 'http://localhost:5000',
      '/health': 'http://localhost:5000',
      '/session': 'http://localhost:5000'
    }
  },
  build: {
    outDir: '../src/AdhocSystem.Api/wwwroot',
    emptyOutDir: true
  }
})
