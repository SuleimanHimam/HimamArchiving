import { chromium } from 'playwright'
import { mkdirSync } from 'node:fs'

const WEB = 'http://localhost:5173'
const API = 'http://localhost:5105/api'
const OUT = 'rcheck'
mkdirSync(OUT, { recursive: true })

// 1) Get a real session token.
const login = await fetch(`${API}/auth/login`, {
  method: 'POST', headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email: 'admin@archiving.local', password: 'Admin@12345' }),
}).then((r) => r.json())

const widths = [
  { w: 375, h: 740, name: 'mobile' },
  { w: 768, h: 1024, name: 'tablet' },
  { w: 1280, h: 800, name: 'desktop' },
]
const pages = [
  { path: '/login', name: 'login', auth: false },
  { path: '/app', name: 'dashboard', auth: true },
  { path: '/app/documents', name: 'documents', auth: true },
  { path: '/app/settings', name: 'settings', auth: true },
]

const browser = await chromium.launch()
for (const vp of widths) {
  const ctx = await browser.newContext({ viewport: { width: vp.w, height: vp.h } })
  // Seed auth so protected routes render.
  await ctx.addInitScript(([t, r, u]) => {
    localStorage.setItem('archiving.accessToken', t)
    localStorage.setItem('archiving.refreshToken', r)
    localStorage.setItem('archiving.user', u)
  }, [login.accessToken, login.refreshToken, JSON.stringify(login.user)])

  for (const pg of pages) {
    const page = await ctx.newPage()
    await page.goto(`${WEB}${pg.path}`, { waitUntil: 'networkidle' }).catch(() => {})
    await page.waitForTimeout(600)
    const m = await page.evaluate(() => ({
      sw: document.documentElement.scrollWidth,
      iw: window.innerWidth,
    }))
    const overflow = m.sw - m.iw
    const flag = overflow > 2 ? `❌ OVERFLOW +${overflow}px` : '✓'
    console.log(`${vp.name.padEnd(8)} ${pg.name.padEnd(10)} innerW=${m.iw} scrollW=${m.sw}  ${flag}`)
    await page.screenshot({ path: `${OUT}/${vp.name}-${pg.name}.png` })
    await page.close()
  }
  await ctx.close()
}
await browser.close()
