/**
 * Service worker for the installed app.
 *
 * Deliberately narrow. An archiving system is a system of record: serving a stale document list
 * or a cached API response would show someone a register that no longer matches reality, which is
 * worse than showing nothing. So:
 *
 *   • /api/** is never touched — every request goes to the network, always.
 *   • Built assets (hashed filenames under /assets/) are cache-first: their name changes when the
 *     content does, so a cache hit can never be stale.
 *   • Navigations are network-first with a cached shell as the offline fallback, which is what
 *     makes the installed window open at all when the network is down.
 */

const VERSION = 'diwan-v1'
const SHELL = `${VERSION}-shell`
const ASSETS = `${VERSION}-assets`
const SHELL_URL = '/index.html'

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(SHELL).then((c) => c.add(SHELL_URL)).then(() => self.skipWaiting()),
  )
})

self.addEventListener('activate', (event) => {
  // Drop caches from older versions so a deploy cannot be half-served from the previous one.
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(
        keys.filter((k) => !k.startsWith(VERSION)).map((k) => caches.delete(k)),
      ))
      .then(() => self.clients.claim()),
  )
})

self.addEventListener('fetch', (event) => {
  const { request } = event
  if (request.method !== 'GET') return

  const url = new URL(request.url)
  if (url.origin !== self.location.origin) return          // API host, fonts, anything external
  if (url.pathname.startsWith('/api/')) return             // never cache records

  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request).catch(() => caches.match(SHELL_URL).then((r) => r ?? Response.error())),
    )
    return
  }

  if (url.pathname.startsWith('/assets/')) {
    event.respondWith(
      caches.match(request).then((hit) => hit ?? fetch(request).then((res) => {
        // Only complete, same-origin responses are worth keeping.
        if (res.ok && res.type === 'basic') {
          const copy = res.clone()
          caches.open(ASSETS).then((c) => c.put(request, copy))
        }
        return res
      })),
    )
  }
})
