import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  define: {
    // Inject version information at build time
    __APP_VERSION__: JSON.stringify(process.env.VITE_APP_VERSION || '0.1.0-dev'),
    __BUILD_DATE__: JSON.stringify(process.env.VITE_BUILD_DATE || new Date().toISOString()),
    __GIT_COMMIT__: JSON.stringify(process.env.VITE_GIT_COMMIT || 'unknown'),
  },
})
