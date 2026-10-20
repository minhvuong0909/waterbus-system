import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

// https://vitejs.dev/config/
export default defineConfig({
  // The React Fast Refresh plugin needs a browser page, so it is left out when Vitest runs.
  plugins: process.env.VITEST ? [] : [react()],
  esbuild: { jsx: 'automatic' },
  server: {
    proxy: {
      '/api': { target: 'http://127.0.0.1:5000', changeOrigin: true },
      '/swagger': { target: 'http://127.0.0.1:5000', changeOrigin: true },
      '/hubs': { target: 'http://127.0.0.1:5000', changeOrigin: true, ws: true },
    },
  },
  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.{ts,tsx}'],
  },
  build: {
    rollupOptions: {
      output: {
        // Stable vendor chunks cache across deploys; heavy libraries stay out of the entry chunk.
        manualChunks: {
          react: ['react', 'react-dom', 'react-router-dom'],
          state: ['@reduxjs/toolkit', 'react-redux'],
          // Named so the bundle report is readable; each is only loaded by the pages that use it.
          leaflet: ['leaflet', 'react-leaflet'],
          gsap: ['gsap', '@gsap/react'],
          signalr: ['@microsoft/signalr'],
        },
      },
    },
  },
})
