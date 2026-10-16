import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

// https://vitejs.dev/config/
export default defineConfig({
  // The React Fast Refresh plugin needs a browser page, so it is left out when Vitest runs.
  plugins: process.env.VITEST ? [] : [react()],
  esbuild: { jsx: 'automatic' },
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
        },
      },
    },
  },
})
