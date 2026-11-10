/**
 * Re-captures every screenshot used by docs/mail-archive-manual.html.
 *
 *   1. start the API      dotnet run --project backend/src/Archiving.Api --urls http://0.0.0.0:5105
 *   2. start the SPA      cd frontend && npx vite --port 5174
 *   3. node docs/tools/capture.mjs
 *
 * The API must be reachable at whatever VITE_API_URL the SPA was built with — .env.local points at
 * the LAN address, so binding the API to localhost only will fail every request on the login page.
 *
 * Screenshots land in docs/screenshots/ under the names the manual references.
 */
import { createRequire } from 'node:module'
// playwright lives in frontend/node_modules, and these scripts do not.
const require_ = createRequire(new URL('../../frontend/package.json', import.meta.url))
const { chromium } = require_('playwright')
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const OUT = join(dirname(fileURLToPath(import.meta.url)), '..', 'screenshots')
const BASE = process.env.DOC_BASE ?? 'http://localhost:5174'
const USER = process.env.DOC_USER ?? 'admin@archiving.local'
const PASS = process.env.DOC_PASS ?? 'Admin@12345'

const browser = await chromium.launch()
// 2x so the shots stay legible when the PDF scales them down to page width.
const page = await browser.newPage({ viewport: { width: 1500, height: 940 }, deviceScaleFactor: 2 })

const shot = async (name) => { await page.screenshot({ path: join(OUT, `${name}.png`) }); console.log('✓', name) }
const go = async (path, wait = 2400) => {
  await page.goto(BASE + path, { waitUntil: 'networkidle' })
  await page.waitForTimeout(wait)
}

// ── sign in ──
await go('/login', 1200)
await page.fill('#email', USER)
await page.fill('#pwd', PASS)
await page.click('.login__submit')
await page.waitForSelector('.shell', { timeout: 20000 })
await page.waitForTimeout(2200)
await shot('00-dashboard')

// ── الديوان ──
await go('/app/diwan', 2600); await shot('01-diwan-list')
await page.click('.mbx__filterbtn'); await page.waitForTimeout(700); await shot('02-diwan-filters')
await page.click('.mbx__filterbtn'); await page.waitForTimeout(400)
await page.locator('.mbx__row').first().click({ button: 'right' })
await page.waitForTimeout(800); await shot('03-diwan-context')
await page.keyboard.press('Escape'); await page.waitForTimeout(400)

await go('/app/diwan/compose?dir=0', 2400); await shot('04-diwan-compose-in')
await go('/app/diwan/compose?dir=1', 2400); await shot('05-diwan-compose-out')
await page.getByRole('tab', { name: /المرفقات/ }).click(); await page.waitForTimeout(900)
await shot('06-diwan-compose-attach')

// A "تم الرد" entry, so the conversation section is present rather than hidden.
await go('/app/diwan/5', 3000); await shot('07-diwan-detail')

// Row dialogs
await go('/app/diwan', 2600)
await page.locator('.mbx__row .mbx__act[title*="مراسلة داخلية"]').first().click()
await page.waitForTimeout(1400); await shot('16-diwan-convert')
await page.keyboard.press('Escape'); await page.waitForTimeout(800)
await page.locator('.mbx__row .mbx__act[title*="حالة البريد"]').first().click()
await page.waitForTimeout(1400); await shot('17-diwan-status')
await page.keyboard.press('Escape'); await page.waitForTimeout(800)
await page.locator('.mbx__row .mbx__act[title*="يطّلع"]').first().click()
await page.waitForTimeout(1800); await shot('18-diwan-access')
await page.keyboard.press('Escape'); await page.waitForTimeout(600)

// ── المراسلات الداخلية ──
await go('/app/internal', 2600); await shot('08-internal-list')
await go('/app/internal/compose', 2400); await shot('09-internal-compose')
await go('/app/internal', 2400)
await page.locator('.mbx__row').first().click(); await page.waitForTimeout(2600)
await shot('10-internal-detail')

// ── الأرشيف ──
await go('/app/archive', 3200)
// Expanded one level: a collapsed tree shows a single root and says nothing about the axis.
await page.getByText('الإدارة العامة', { exact: false }).first().click()
await page.waitForTimeout(1800); await shot('11-archive-tree')

await page.getByRole('tab', { name: 'وارد الأرشفة' }).click(); await page.waitForTimeout(2400)
await shot('12-archive-intake')
await page.getByRole('button', { name: /حفظ في صندوق/ }).first().click()
await page.waitForTimeout(1800); await shot('19-archive-filing')
await page.getByRole('button', { name: /إغلاق/ }).first().click(); await page.waitForTimeout(800)
await page.getByRole('button', { name: /إرجاع/ }).first().click()
await page.waitForTimeout(1500); await shot('20-archive-return')
await page.keyboard.press('Escape'); await page.waitForTimeout(600)

await page.getByRole('tab', { name: 'البنود المؤرشفة' }).click(); await page.waitForTimeout(2400)
await shot('13-archive-items')
// The filter toggle carries an icon and no label, so it is matched by its glyph.
await page.locator('button:has(svg.lucide-funnel), button:has(svg.lucide-filter)').first().click()
await page.waitForTimeout(1500); await shot('14-archive-items-filters')

await page.getByRole('tab', { name: 'المواقع الفعلية' }).click(); await page.waitForTimeout(2400)
await shot('15-archive-locations')

await browser.close()
