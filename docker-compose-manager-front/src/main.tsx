import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import './i18n/config'

// Silence non-critical console outputs if VITE_DISABLE_LOGS is set
if (import.meta.env.VITE_DISABLE_LOGS === 'true') {
  const noop = () => {}
  console.log = noop
  console.info = noop
  console.debug = noop
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
