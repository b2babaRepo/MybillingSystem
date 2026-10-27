/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { VitePWA } from 'vite-plugin-pwa'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    VitePWA({
      // Updates wait for a tap (src/lib/pwa.ts): a silent reload mid-bill would lose the cart.
      registerType: 'prompt',
      includeAssets: ['favicon.svg', 'apple-touch-icon.png'],
      manifest: {
        name: 'Grocery Khata',
        short_name: 'Khata',
        description: 'Billing and khata for kirana shops',
        lang: 'hi',
        start_url: '/',
        display: 'standalone',
        background_color: '#ffffff',
        theme_color: '#0f766e',
        // PNGs for Android's install prompt and home screen; the maskable one fills launcher shapes.
        icons: [
          { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png', purpose: 'any' },
          { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
          { src: 'maskable-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
          { src: 'favicon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' },
        ],
      },
      workbox: {
        // The app shell is cached so billing opens with no network; API calls are never cached here
        // (offline data lives in IndexedDB, written by the app itself).
        globPatterns: ['**/*.{js,css,html,svg,png,woff2}'],
        navigateFallback: '/index.html',
        navigateFallbackDenylist: [/^\/api/],
        // The Mukta font, kept after the first visit so text looks the same offline.
        runtimeCaching: [
          {
            urlPattern: /^https:\/\/fonts\.(googleapis|gstatic)\.com\/.*/,
            handler: 'CacheFirst',
            options: { cacheName: 'fonts', expiration: { maxEntries: 20, maxAgeSeconds: 60 * 60 * 24 * 365 } },
          },
        ],
      },
    }),
  ],
  server: {
    proxy: { '/api': 'http://localhost:5274' },
  },
  test: {
    environment: 'node',
    setupFiles: ['fake-indexeddb/auto'],
  },
})
