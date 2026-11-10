/**
 * Tablet responsiveness audit — run it after any layout change:
 *
 *   1. start the API      dotnet run --project backend/src/Archiving.Api --urls http://0.0.0.0:5105
 *   2. start the SPA      cd frontend && npx vite --port 5174
 *   3. node docs/tools/audit-tablet.mjs          (SHOT_DIR=<dir> to also save screenshots)
 *
 * It emulates a real touch device, so the `pointer: coarse` rules in styles/touch.css are the ones
 * being measured — a plain desktop context reports a fine pointer and would silently skip them.
 * visits every route at two tablet viewports, screenshots each, and
 * measures the failures a "does it overflow?" check cannot see.
 *
 * Overflow is only the crudest failure. On a tablet the real ones are: the sidebar eating half the
 * frame, a wide table squeezed into a column too narrow to read, touch targets sized for a mouse,
 * and dialogs whose buttons fall outside the viewport.
 */
import { createRequire } from 'node:module'
// playwright lives in frontend/node_modules, and this file does not.
const require_ = createRequire(new URL('../../frontend/package.json', import.meta.url))
const { chromium } = require_('playwright')

const BASE = 'http://localhost:5174'
const OUT = process.env.SHOT_DIR

const VIEWPORTS = [
  { name: 'p768', width: 768, height: 1024 },
  { name: 'l1024', width: 1024, height: 768 },
]

const ROUTES = [
  ['dashboard', '/app'],
  ['diwan-list', '/app/diwan'],
  ['diwan-compose', '/app/diwan/compose?dir=0'],
  ['diwan-detail', '/app/diwan/5'],
  ['internal-list', '/app/internal'],
  ['internal-compose', '/app/internal/compose'],
  ['internal-detail', '/app/internal/242'],
  ['documents-list', '/app/documents'],
  ['documents-new', '/app/documents/new'],
  ['documents-detail', '/app/documents/424'],
  ['archive', '/app/archive'],
  ['disposition', '/app/disposition'],
  ['external', '/app/external'],
  ['departments', '/app/departments'],
  ['users', '/app/users'],
  ['locations', '/app/locations'],
  ['monitoring', '/app/monitoring'],
  ['settings', '/app/settings'],
  ['scanner', '/app/scanner'],
  ['backup', '/app/backup'],
  ['account', '/app/account'],
  ['print-design', '/app/print-design'],
  ['reports', '/app/reports'],
]

const probe = () => {
  const vw = document.documentElement.clientWidth
  const rail = document.querySelector('.shell__rail')
  const railBox = rail?.getBoundingClientRect()
  const railStyle = rail ? getComputedStyle(rail) : null
  // Off-canvas rails are still in the DOM with a width; what matters is whether they sit inside
  // the frame and take space from the content.
  const railVisible = !!railBox && railBox.width > 0 &&
    railBox.left < vw && railBox.right > 0 && railStyle.visibility !== 'hidden'

  const main = document.querySelector('.shell__main')
  const mainW = main ? Math.round(main.getBoundingClientRect().width) : 0

  // Tables: how much wider than their scroll container, i.e. how much is hidden at rest.
  const tables = [...document.querySelectorAll('table')].map((t) => {
    const w = Math.round(t.getBoundingClientRect().width)
    let p = t.parentElement, host = null
    while (p && p !== document.body) {
      if (['auto', 'scroll'].includes(getComputedStyle(p).overflowX)) { host = p; break }
      p = p.parentElement
    }
    const hostW = host ? Math.round(host.clientWidth) : mainW
    return { w, hostW, hidden: Math.max(0, w - hostW), scrolls: !!host, cols: t.querySelectorAll('thead th').length }
  })

  // Touch targets below the floor. Checkboxes and radios are excluded: they are ~22px by nature
  // and their label is the real target, so counting them only cries wolf.
  const small = [...document.querySelectorAll('button, a[href], select, [role="tab"]')]
    .map((el) => ({ el, r: el.getBoundingClientRect() }))
    // 32px is the deliberate floor for dense in-row icon buttons (see touch.css), so flag
    // only what falls below it.
    .filter(({ r }) => r.width > 0 && r.height > 0 && r.height < 32)
    .map(({ el, r }) => ({
      tag: el.tagName.toLowerCase(),
      cls: (typeof el.className === 'string' ? el.className : '').split(' ').slice(0, 2).join('.'),
      h: Math.round(r.height), w: Math.round(r.width),
    }))

  const counts = {}
  for (const s of small) { const k = `${s.tag}.${s.cls}`; counts[k] = (counts[k] ?? 0) + 1 }

  return {
    coarse: matchMedia('(pointer: coarse)').matches,
    vw,
    overflow: Math.round(document.documentElement.scrollWidth - vw),
    railVisible,
    railW: railVisible ? Math.round(railBox.width) : 0,
    mainW,
    tables: tables.filter((t) => t.hidden > 20),
    tableCount: tables.length,
    smallTargets: Object.entries(counts).sort((a, b) => b[1] - a[1]).slice(0, 3),
  }
}

const browser = await chromium.launch()
const results = []

for (const vp of VIEWPORTS) {
  // isMobile, not just hasTouch: `pointer: coarse` — which every touch rule keys off — comes from
  // Chromium's mobile emulation flag, and hasTouch alone leaves the page reporting a fine pointer.
  const ctx = await browser.newContext({
    viewport: { width: vp.width, height: vp.height }, deviceScaleFactor: 2,
    hasTouch: true, isMobile: true,
  })
  const page = await ctx.newPage()
  await page.goto(`${BASE}/login`, { waitUntil: 'networkidle' })
  await page.fill('#email', 'admin@archiving.local')
  await page.fill('#pwd', 'Admin@12345')
  await page.click('.login__submit')
  await page.waitForSelector('.shell', { timeout: 20000 })
  await page.waitForTimeout(1500)

  for (const [name, path] of ROUTES) {
    try {
      await page.goto(BASE + path, { waitUntil: 'networkidle', timeout: 25000 })
      await page.waitForTimeout(1600)
      results.push({ vp: vp.name, name, ...(await page.evaluate(probe)) })
      if (OUT) await page.screenshot({ path: `${OUT}/${vp.name}-${name}.png` })
    } catch (e) {
      results.push({ vp: vp.name, name, error: String(e).split('\n')[0].slice(0, 80) })
    }
  }
  await ctx.close()
}
await browser.close()

for (const vp of VIEWPORTS) {
  const rows = results.filter((x) => x.vp === vp.name)
  console.log(`\n════════ ${vp.name} (${vp.width}px) ════════`)
  console.log(`pointer: ${rows[0]?.coarse ? 'coarse (touch)' : 'fine (mouse)'}`)
  console.log(`rail inside the frame: ${rows[0]?.railVisible ? `YES, ${rows[0].railW}px — content gets ${rows[0].mainW}px` : 'no (off-canvas)'}`)
  for (const r of rows) {
    if (r.error) { console.log(`  ??  ${r.name}  ${r.error}`); continue }
    const flags = []
    if (r.overflow > 2) flags.push(`page overflows ${r.overflow}px`)
    for (const t of r.tables) flags.push(`table ${t.cols}col w=${t.w} in ${t.hostW} → ${t.hidden}px hidden${t.scrolls ? '' : ' NO SCROLL'}`)
    if (r.smallTargets.length) {
      flags.push('small: ' + r.smallTargets.map(([k, n]) => `${k}×${n}`).join(', '))
    }
    console.log(`  ${flags.length ? 'XX' : 'ok'}  ${r.name.padEnd(18)} ${flags.join(' | ')}`)
  }
}
