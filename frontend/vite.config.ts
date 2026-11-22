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
        name: 'Apna Kaarobar',
        short_name: 'Kaarobar',
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
        // The app shell is cached so billing opens with no network; API data is never cached here (offline data
        // lives in IndexedDB, written by the app itself). Item photos are the one exception, below.
        globPatterns: ['**/*.{js,css,html,svg,png,woff2,wasm}'],
        // The backup barcode reader (WebAssembly, about 1 MB) is kept too, so the camera scans offline.
        maximumFileSizeToCacheInBytes: 4 * 1024 * 1024,
        navigateFallback: '/index.html',
        navigateFallbackDenylist: [/^\/api/],
        // The morning stock reminder: shown from a push, and opens "What to order" when tapped.
        importScripts: ['push-sw.js'],
        // The Mukta font, kept after the first visit so text looks the same offline.
        runtimeCaching: [
          {
            urlPattern: /^https:\/\/fonts\.(googleapis|gstatic)\.com\/.*/,
            handler: 'CacheFirst',
            options: { cacheName: 'fonts', expiration: { maxEntries: 20, maxAgeSeconds: 60 * 60 * 24 * 365 } },
          },
          {
            // Shop photos of items: a photo's URL changes with it, so a cached one never goes stale. Capped, and
            // the oldest-used go first, so a busy shop's photos can't fill the phone.
            urlPattern: /\/api\/v1\/images\//,
            handler: 'CacheFirst',
            options: {
              cacheName: 'item-photos',
              expiration: { maxEntries: 400, maxAgeSeconds: 60 * 60 * 24 * 30, purgeOnQuotaError: true },
              cacheableResponse: { statuses: [200] },
            },
          },
        ],
      },
    }),
  ],
  server: {
    // GK_API points a second dev server at another API (one running a branch's changes); the usual one is on 5274.
    proxy: { '/api': process.env.GK_API ?? 'http://localhost:5274' },
    // A Cloudflare quick tunnel (cloudflared tunnel --url http://localhost:5175) for demos from this PC.
    allowedHosts: ['.trycloudflare.com'],
  },
  test: {
    environment: 'node',
    setupFiles: ['fake-indexeddb/auto'],
  },
})
