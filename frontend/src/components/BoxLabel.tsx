import { useEffect, useState } from 'react'
import { locations, type Box, type Breadcrumb } from '../lib/locations'

/** A printable card for a box: code, barcode, full breadcrumb path + location code. */
export default function BoxLabel({ box, onClose }: { box: Box; onClose: () => void }) {
  const [bc, setBc] = useState<Breadcrumb | null>(null)
  useEffect(() => { locations.breadcrumb(box.id).then(setBc).catch(() => {}) }, [box.id])

  // Decorative bar pattern derived from the barcode/code (visual; pair with a real barcode font in prod).
  const code = box.barcode || box.boxCode
  const bars = Array.from(code).map((ch) => (ch.charCodeAt(0) % 4) + 1)

  function print() {
    const w = window.open('', '_blank', 'width=420,height=320')
    if (!w) return
    const barsHtml = bars.map((b) => `<span style="display:inline-block;width:${b}px;height:46px;background:#000;margin-inline-end:1px"></span>`).join('')
    w.document.write(`<html dir="rtl"><head><meta charset="utf-8"><title>${box.boxCode}</title></head>
      <body style="font-family:Arial;padding:18px;text-align:center">
        <div style="border:2px solid #14213D;border-radius:10px;padding:14px">
          <div style="font-size:22px;font-weight:bold">${box.boxCode}</div>
          <div style="font-family:monospace;color:#555;margin:6px 0">${bc?.locationCode ?? ''}</div>
          <div style="white-space:nowrap;overflow:hidden;direction:ltr;margin:8px 0">${barsHtml}</div>
          <div style="font-family:monospace;font-size:12px">${code}</div>
          <div style="font-size:12px;margin-top:8px;color:#333">${bc?.path ?? ''}</div>
        </div>
      </body></html>`)
    w.document.close(); w.focus(); setTimeout(() => w.print(), 200)
  }

  return (
    <div className="shell__backdrop shell__backdrop--drawer" style={{ display: 'grid', placeItems: 'center', zIndex: 300 }} onClick={onClose}>
      <div className="doc-card" style={{ maxWidth: 420, width: '90%', textAlign: 'center' }} onClick={(e) => e.stopPropagation()}>
        <div style={{ border: '2px solid var(--ink)', borderRadius: 10, padding: '1rem' }}>
          <div style={{ fontSize: '1.4rem', fontWeight: 700 }}>{box.boxCode}</div>
          <div className="mono muted" style={{ margin: '.4rem 0' }}>{bc?.locationCode ?? '…'}</div>
          <div style={{ display: 'flex', justifyContent: 'center', gap: 1, margin: '.6rem 0', direction: 'ltr' }}>
            {bars.map((b, i) => <span key={i} style={{ width: b, height: 46, background: 'var(--ink)' }} />)}
          </div>
          <div className="mono" style={{ fontSize: '.8rem' }}>{code}</div>
          <div className="muted" style={{ fontSize: '.82rem', marginTop: '.6rem' }}>{bc?.path ?? ''}</div>
        </div>
        <div className="form-actions" style={{ justifyContent: 'center', marginTop: '1rem' }}>
          <button className="btn btn-primary" onClick={print}>🖨 طباعة البطاقة</button>
          <button className="btn btn-ghost" onClick={onClose}>إغلاق</button>
        </div>
      </div>
    </div>
  )
}
