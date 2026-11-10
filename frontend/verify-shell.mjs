/* Renders the authed shell against the dev server with a seeded session, in both
 * locales, and reports what the sidebar actually shows. The API does not need to
 * be up: auth/ProtectedRoute/AppLayout read the session from localStorage, and
 * useCurrentUser seeds initialData from it. */
import { chromium } from 'playwright'

const BASE = 'http://localhost:5173'

const USER = (roles, permissions) => ({
  id: 1, fullName: 'Test User', email: 't@x.io', jobTitle: 'QA',
  clearance: 'Public', roles, permissions,
})

async function shell(page, { roles, permissions, lang }) {
  await page.goto(BASE, { waitUntil: 'domcontentloaded' })
  await page.evaluate(([u, l]) => {
    localStorage.setItem('archiving.accessToken', 'seed')
    localStorage.setItem('archiving.refreshToken', 'seed')
    localStorage.setItem('archiving.user', JSON.stringify(u))
    localStorage.setItem('diwan-lang', l)
  }, [USER(roles, permissions), lang])

  await page.goto(BASE + '/app', { waitUntil: 'networkidle' }).catch(() => {})
  await page.waitForTimeout(700)

  return page.evaluate(() => ({
    dir: document.documentElement.getAttribute('dir'),
    lang: document.documentElement.getAttribute('lang'),
    navItems: [...document.querySelectorAll('.rail__item')].map((e) => e.textContent.trim()),
    groups: [...document.querySelectorAll('.rail__group')].map((e) => e.textContent.trim()),
  }))
}

const browser = await chromium.launch()
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } })
page.on('pageerror', (e) => console.log('  !! page error:', e.message.split('\n')[0]))

const cases = [
  { name: 'Employee / EN', roles: ['Employee'], permissions: ['Documents.View'], lang: 'en' },
  { name: 'Employee / AR', roles: ['Employee'], permissions: ['Documents.View'], lang: 'ar' },
  { name: 'SystemAdmin / EN (no perms at all)', roles: ['System Administrator'], permissions: [], lang: 'en' },
  { name: 'SystemAdmin / AR (no perms at all)', roles: ['System Administrator'], permissions: [], lang: 'ar' },
]

for (const c of cases) {
  const r = await shell(page, c)
  console.log(`\n── ${c.name}`)
  console.log(`   dir=${r.dir} lang=${r.lang}`)
  console.log(`   groups: ${r.groups.join(' | ') || '(none)'}`)
  console.log(`   nav (${r.navItems.length}): ${r.navItems.join(' · ') || '(none)'}`)
}

await browser.close()
