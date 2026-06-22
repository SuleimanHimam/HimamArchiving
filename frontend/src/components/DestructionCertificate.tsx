import { useEffect, useState } from 'react'
import { motion } from 'motion/react'
import { disposition, type DispositionCertificate as Cert } from '../lib/disposition'
import { getBranding } from '../lib/branding'

/** Printable/exportable Certificate of Destruction. Opens an isolated print window (→ "Save as PDF"). */
export default function DestructionCertificate({ requestId, onClose }: { requestId: number; onClose: () => void }) {
  const [c, setC] = useState<Cert | null>(null)
  const [err, setErr] = useState('')
  useEffect(() => { disposition.certificate(requestId).then(setC).catch(() => setErr('تعذّر تحميل الشهادة')) }, [requestId])

  const org = getBranding().nameAr || 'نظام الأرشفة'

  function print() {
    if (!c) return
    const rows = c.documentNumbers.map((n, i) => `<tr><td>${i + 1}</td><td>${n}</td><td>${c.documentIds[i] ?? ''}</td></tr>`).join('')
    const html = `<!doctype html><html dir="rtl" lang="ar"><head><meta charset="utf-8"><title>${c.certificateNumber}</title>
      <style>
        body{font-family:'Segoe UI',Tahoma,sans-serif;color:#211d17;padding:40px;max-width:760px;margin:auto}
        h1{text-align:center;color:#9B2226;margin:.2rem 0}
        .sub{text-align:center;color:#6e6552;margin-bottom:1.5rem}
        .num{text-align:center;font-family:monospace;font-size:1.1rem;letter-spacing:1px;margin-bottom:1.5rem}
        table{width:100%;border-collapse:collapse;margin:1rem 0}
        th,td{border:1px solid #cabf9f;padding:8px 10px;text-align:right;font-size:.9rem}
        th{background:#f3eddc}
        .meta{display:flex;justify-content:space-between;gap:2rem;margin-top:1rem}
        .sign{margin-top:3rem;display:flex;justify-content:space-between;gap:3rem}
        .sign div{flex:1;border-top:1px solid #211d17;padding-top:.4rem;text-align:center;font-size:.85rem}
      </style></head><body>
      <h1>شهادة إتلاف وثائق</h1>
      <div class="sub">${org}</div>
      <div class="num">${c.certificateNumber}</div>
      <div class="meta">
        <span><strong>تاريخ الإصدار:</strong> ${new Date(c.generatedAtUtc).toLocaleString('ar')}</span>
        <span><strong>طريقة الإتلاف:</strong> ${c.destructionMethod}</span>
      </div>
      <table><thead><tr><th>#</th><th>رقم الوثيقة</th><th>المعرّف</th></tr></thead><tbody>${rows}</tbody></table>
      <p>تشهد هذه الوثيقة بأن السجلات المذكورة أعلاه قد أُتلِفت إتلافًا نهائيًا بعد اكتمال مدة حفظها النظامية،
         وفق دورة موافقة من خطوتين (تحقّق ثم موافقة نهائية).</p>
      <div class="sign">
        <div>المحقِّق (مسؤول السجلات)<br><br>${c.verifiedByName ?? '—'}</div>
        <div>المعتمِد النهائي (الشؤون القانونية)<br><br>${c.finalApprovedByName ?? '—'}</div>
      </div>
      </body></html>`
    const w = window.open('', '_blank', 'width=820,height=900')
    if (!w) return
    w.document.write(html); w.document.close(); w.focus()
    setTimeout(() => w.print(), 300)
  }

  return (
    <div className="preview-overlay" onClick={onClose}>
      <motion.div className="preview-modal" onClick={(e) => e.stopPropagation()}
        style={{ width: 'min(560px, 94vw)', height: 'auto', maxHeight: '90vh' }}
        initial={{ opacity: 0, scale: 0.97 }} animate={{ opacity: 1, scale: 1 }}>
        <header className="preview-head">
          <span className="preview-title">شهادة الإتلاف</span>
          <button className="btn btn-ghost btn-sm" onClick={onClose}>✕ إغلاق</button>
        </header>
        <div style={{ padding: '1rem', color: 'var(--ink-text, #211d17)' }}>
          {err && <p className="login__error">{err}</p>}
          {c && (
            <>
              <h2 style={{ textAlign: 'center', color: '#9B2226', margin: '.2rem 0' }}>شهادة إتلاف وثائق</h2>
              <p className="mono" style={{ textAlign: 'center', letterSpacing: 1 }}>{c.certificateNumber}</p>
              <dl className="detail-list">
                <dt>طريقة الإتلاف</dt><dd>{c.destructionMethod}</dd>
                <dt>عدد الوثائق</dt><dd>{c.documentNumbers.length}</dd>
                <dt>الوثائق</dt><dd className="mono">{c.documentNumbers.join('، ') || '—'}</dd>
                <dt>المحقِّق</dt><dd>{c.verifiedByName ?? '—'}</dd>
                <dt>المعتمِد النهائي</dt><dd>{c.finalApprovedByName ?? '—'}</dd>
                <dt>تاريخ الإصدار</dt><dd className="mono">{new Date(c.generatedAtUtc).toLocaleString('ar')}</dd>
              </dl>
            </>
          )}
        </div>
        <div className="form-actions" style={{ padding: '0 1rem 1rem' }}>
          <button className="btn btn-primary" disabled={!c} onClick={print}>🖨 طباعة / تصدير PDF</button>
          <button className="btn btn-ghost" onClick={onClose}>إغلاق</button>
        </div>
      </motion.div>
    </div>
  )
}
