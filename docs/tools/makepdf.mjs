/**
 * Renders docs/mail-archive-manual.html to the Arabic PDF beside it.
 *
 *   node docs/tools/makepdf.mjs
 *
 * The manual references screenshots as {{IMG:name}}; each is inlined as a data URI here so the
 * PDF carries its own images and the HTML stays readable in the repo. Headless Chromium does the
 * printing — no LaTeX, no PDF library.
 */
import { readFileSync, writeFileSync, unlinkSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createRequire } from 'node:module'
// playwright lives in frontend/node_modules, and these scripts do not.
const require_ = createRequire(new URL('../../frontend/package.json', import.meta.url))
const { chromium } = require_('playwright')

const docs = join(dirname(fileURLToPath(import.meta.url)), '..')
const src = join(docs, 'mail-archive-manual.html')
const out = join(docs, 'دليل-البريد-والأرشيف.pdf')
const tmp = join(docs, '_build.html')

const html = readFileSync(src, 'utf8').replace(/\{\{IMG:([^}]+)\}\}/g, (_, name) => {
  const b64 = readFileSync(join(docs, 'screenshots', `${name}.png`)).toString('base64')
  return `data:image/png;base64,${b64}`
})
writeFileSync(tmp, html)

const browser = await chromium.launch()
const page = await browser.newPage()
await page.goto('file:///' + tmp.split('\\').join('/'), { waitUntil: 'networkidle' })
await page.emulateMedia({ media: 'print' })
await page.waitForTimeout(1500)
await page.pdf({
  path: out,
  format: 'A4',
  printBackground: true,
  displayHeaderFooter: true,
  headerTemplate: '<div></div>',
  footerTemplate: `<div style="width:100%;font-size:8pt;color:#8A96AC;font-family:'Segoe UI',Tahoma;
     padding:0 15mm;display:flex;justify-content:space-between;direction:rtl">
     <span>الديوان الإلكتروني — دليل البريد والأرشيف</span>
     <span class="pageNumber"></span></div>`,
  margin: { top: '14mm', bottom: '16mm', left: '13mm', right: '13mm' },
})
await browser.close()
unlinkSync(tmp)
console.log('pdf:', out)
