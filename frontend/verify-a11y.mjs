/* axe-core accessibility check against the running dev server, in BOTH locales
 * (§14.5 WCAG 2.1 AA, §17.4 DoD item 4: "axe accessibility checks pass in both").
 *
 * Seeds a System Admin session in localStorage so the authed shell renders without
 * the API being up. Data-dependent screens will be empty — this checks the shell,
 * not the tables. */
import { chromium } from 'playwright'
import { readFileSync } from 'node:fs'

const BASE = 'http://localhost:5173'
const THEME = process.env.THEME === 'dark' ? 'dark' : 'light'
const AXE = readFileSync('node_modules/axe-core/axe.min.js', 'utf8')

const USER = {
  id: 1, fullName: 'Test User', email: 't@x.io', jobTitle: 'QA',
  clearance: 'Public', roles: ['System Administrator'], permissions: [],
}

const ROUTES = [
  ['/login', 'Login'],
  ['/app', 'Dashboard'],
  ['/app/monitoring', 'Audit log viewer'],
  ['/app/settings?tab=prefs', 'Settings · Preferences'],
]

const browser = await chromium.launch()
let totalViolations = 0

for (const lang of ['en', 'ar']) {
  console.log(`\n════ locale: ${lang} ════`)
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } })

  await page.goto(BASE, { waitUntil: 'domcontentloaded' })
  // process.env is not available inside evaluate() — that runs in the browser.
  await page.evaluate(([u, l, theme]) => {
    localStorage.setItem('archiving.accessToken', 'seed')
    localStorage.setItem('archiving.refreshToken', 'seed')
    localStorage.setItem('archiving.user', JSON.stringify(u))
    localStorage.setItem('diwan-lang', l)
    localStorage.setItem('marasil.theme', theme)
  }, [USER, lang, THEME])

  for (const [route, name] of ROUTES) {
    await page.goto(BASE + route, { waitUntil: 'networkidle' }).catch(() => {})
    await page.waitForTimeout(600)
    await page.addScriptTag({ content: AXE })

    const res = await page.evaluate(async () =>
      await window.axe.run(document, {
        runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] },
      }),
    )

    const v = res.violations
    totalViolations += v.length
    const dir = await page.evaluate(() => document.documentElement.getAttribute('dir'))
    console.log(`\n  ${name}  [dir=${dir}]  → ${v.length} violation type(s)`)
    for (const x of v) {
      console.log(`    · [${x.impact}] ${x.id}: ${x.help}  (${x.nodes.length} node${x.nodes.length > 1 ? 's' : ''})`)
      const t = x.nodes[0]?.target?.join(' ')
      if (t) console.log(`        first: ${t.slice(0, 90)}`)
    }
  }
  await page.close()
}

await browser.close()
console.log(`\n──── total violation types across both locales: ${totalViolations}`)
