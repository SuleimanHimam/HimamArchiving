// Reports hardcoded Arabic literals in TS/TSX — §14.2 requires every UI string
// to be a key. Comments are excluded; JSX/attribute/string content is not.
import fs from 'node:fs'
import path from 'node:path'

const AR = /[؀-ۿ]/

function walk(dir, out = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name)
    if (e.isDirectory()) walk(p, out)
    else if (/\.(tsx|ts)$/.test(e.name)) out.push(p)
  }
  return out
}

const rows = []
for (const f of walk('src')) {
  const lines = fs.readFileSync(f, 'utf8').split('\n')
  let n = 0
  for (const l of lines) {
    const trimmed = l.trim()
    if (trimmed.startsWith('//') || trimmed.startsWith('*') || trimmed.startsWith('/*')) continue
    if (AR.test(l)) n++
  }
  if (n) rows.push([n, f.split(path.sep).join('/')])
}
rows.sort((a, b) => b[0] - a[0])

const total = rows.reduce((s, r) => s + r[0], 0)
console.log(`files with hardcoded Arabic: ${rows.length}  |  total lines: ${total}`)
for (const [n, f] of rows.slice(0, 12)) console.log('  ' + String(n).padStart(4) + '  ' + f)
