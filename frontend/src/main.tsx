import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { AuthProvider } from './auth'
import { piattaformaNativa } from './mobile/piattaforma'

document.documentElement.classList.toggle('app-nativa', piattaformaNativa)

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AuthProvider>
      <App />
    </AuthProvider>
  </StrictMode>,
)

// Registra il service worker: rende l'app installabile su telefono e computer
// e apribile anche offline. Vale solo in produzione (build), non durante lo sviluppo.
if ('serviceWorker' in navigator && import.meta.env.PROD && !piattaformaNativa) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch(() => {
      /* installazione PWA non disponibile: l'app funziona comunque dal browser */
    })
  })
}

if (piattaformaNativa && 'serviceWorker' in navigator) {
  void navigator.serviceWorker.getRegistrations().then((registrazioni) =>
    Promise.all(registrazioni.map((registrazione) => registrazione.unregister())),
  )
}
