/**
 * Regenerates the Windows .ico files from the brand SVGs.
 *
 *   node brand/make-icons.mjs
 *
 * Renders each SVG at every icon size in headless Chromium (the browser is already a
 * dependency of the frontend, so this adds no image toolchain to the build) and writes a
 * PNG-compressed .ico by hand — Windows Vista and later read PNG entries directly.
 *
 * Outputs: brand/app-icon.ico (+ a copy in deploy/) and brand/setup-icon.ico.
 */
import { readFileSync, writeFileSync, copyFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'

// playwright lives in frontend/node_modules, and this file does not.
const require_ = createRequire(new URL('../frontend/package.json', import.meta.url))
const { chromium } = require_('playwright')

const here = dirname(fileURLToPath(import.meta.url))
const root = join(here, '..')
const SIZES = [16, 24, 32, 48, 64, 128, 256]

async function render(page, svgPath, size) {
  const svg = readFileSync(svgPath, 'utf8')
  await page.setViewportSize({ width: size, height: size })
  await page.setContent(
    `<style>html,body{margin:0;background:transparent}svg{display:block;width:${size}px;height:${size}px}</style>${svg}`,
  )
  return page.screenshot({ omitBackground: true })
}

/** ICONDIR + ICONDIRENTRY[] + the PNG payloads, in that order. */
function buildIco(pngs) {
  const head = Buffer.alloc(6)
  head.writeUInt16LE(0, 0); head.writeUInt16LE(1, 2); head.writeUInt16LE(pngs.length, 4)
  const dir = Buffer.alloc(16 * pngs.length)
  let offset = 6 + dir.length
  pngs.forEach(({ size, data }, i) => {
    const e = 16 * i
    // 256 is stored as 0 — the field is one byte.
    dir[e] = size === 256 ? 0 : size
    dir[e + 1] = size === 256 ? 0 : size
    dir.writeUInt16LE(1, e + 4)       // colour planes
    dir.writeUInt16LE(32, e + 6)      // bits per pixel
    dir.writeUInt32LE(data.length, e + 8)
    dir.writeUInt32LE(offset, e + 12)
    offset += data.length
  })
  return Buffer.concat([head, dir, ...pngs.map((p) => p.data)])
}

const browser = await chromium.launch()
const page = await browser.newPage({ deviceScaleFactor: 1 })

for (const [svg, ico] of [['logo-mark.svg', 'app-icon.ico'], ['logo-setup.svg', 'setup-icon.ico']]) {
  const pngs = []
  for (const size of SIZES) pngs.push({ size, data: await render(page, join(here, svg), size) })
  const out = join(here, ico)
  writeFileSync(out, buildIco(pngs))
  console.log(`${ico}  ${SIZES.length} sizes  ${readFileSync(out).length} bytes`)
}

copyFileSync(join(here, 'app-icon.ico'), join(root, 'deploy', 'app-icon.ico'))
copyFileSync(join(here, 'setup-icon.ico'), join(root, 'deploy', 'setup-icon.ico'))
console.log('copied both to deploy/')

await browser.close()
