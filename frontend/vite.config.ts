import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { resolve } from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src'),
    },
  },
  // Expose the dev server on the LAN so other machines on the same network can
  // reach it at http://192.168.1.49:5173 (host: true binds 0.0.0.0 / all interfaces).
  server: {
    host: true,
    port: 5173,
  },
})
