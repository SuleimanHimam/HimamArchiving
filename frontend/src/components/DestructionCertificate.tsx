import { useEffect, useState } from 'react'
import { X, Printer } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { motion } from 'motion/react'
import { disposition, type DispositionCertificate as Cert } from '../lib/disposition'
import { getBranding } from '../lib/branding'
import { formatDateTime } from '../lib/datetime'

/** Printable/exportable Certificate of Destruction. Opens an isolated print window (→ "Save as PDF"). */
export default function DestructionCertificate({ requestId, onClose }: { requestId: number; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const [c, setC] = useState<Cert | null>(null)
  const [err, setErr] = useState('')
  useEffect(() => {
    disposition.certificate(requestId).then(setC).catch(() => setErr(t('destruction.cert.loadError')))
  }, [requestId, t])

  const branding = getBranding()
  const org = (i18n.language.startsWith('ar') ? branding.nameAr : branding.nameEn || branding.nameAr) || t('app.name')

  function print() {
    if (!c) return
    // A certificate is immutable once issued (§26.3) — it must render in the
    // language and direction of the session that issued it, not a baked-in one.
    const lang = i18n.language.startsWith('ar') ? 'ar' : 'en'
    const dir = lang === 'ar' ? 'rtl' : 'ltr'
    const rows = c.documentNumbers.map((n, i) => `<tr><td>${i + 1}</td><td>${n}</td><td>${c.documentIds[i] ?? ''}</td></tr>`).join('')
    const html = `<!doctype html><html dir="${dir}" lang="${lang}"><head><meta charset="utf-8"><title>${c.certificateNumber}</title>
      <style>
        body{font-family:'Segoe UI',Tahoma,sans-serif;color:#211d17;padding:40px;max-width:760px;margin:auto}
        h1{text-align:center;color:#9B2226;margin:.2rem 0}
        .sub{text-align:center;color:#6e6552;margin-bottom:1.5rem}
        .num{text-align:center;font-family:monospace;font-size:1.1rem;letter-spacing:1px;margin-bottom:1.5rem}
        table{width:100%;border-collapse:collapse;margin:1rem 0}
        th,td{border:1px solid #cabf9f;padding:8px 10px;text-align:start;font-size:.9rem}
        th{background:#f3eddc}
        .meta{display:flex;justify-content:space-between;gap:2rem;margin-top:1rem}
        .sign{margin-top:3rem;display:flex;justify-content:space-between;gap:3rem}
        .sign div{flex:1;border-top:1px solid #211d17;padding-top:.4rem;text-align:center;font-size:.85rem}
      </style></head><body>
      <h1>${t('destruction.cert.title')}</h1>
      <div class="sub">${org}</div>
      <div class="num">${c.certificateNumber}</div>
      <div class="meta">
        <span><strong>${t('destruction.cert.issuedAt')}:</strong> ${formatDateTime(c.generatedAtUtc, lang)}</span>
        <span><strong>${t('destruction.cert.method')}:</strong> ${c.destructionMethod}</span>
      </div>
      <table><thead><tr><th>#</th><th>${t('destruction.cert.docNumber')}</th><th>${t('destruction.cert.docId')}</th></tr></thead><tbody>${rows}</tbody></table>
      <p>${t('destruction.cert.attestation')}</p>
      <div class="sign">
        <div>${t('destruction.cert.verifier')}<br><br>${c.verifiedByName ?? '—'}</div>
        <div>${t('destruction.cert.approver')}<br><br>${c.finalApprovedByName ?? '—'}</div>
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
          <button className="btn btn-ghost btn-sm" onClick={onClose}><X size={14} aria-hidden /> إغلاق</button>
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
                <dt>تاريخ الإصدار</dt><dd className="mono">{formatDateTime(c.generatedAtUtc, i18n.language)}</dd>
              </dl>
            </>
          )}
        </div>
        <div className="form-actions" style={{ padding: '0 1rem 1rem' }}>
          <button className="btn btn-primary" disabled={!c} onClick={print}><Printer size={16} aria-hidden /> طباعة / تصدير PDF</button>
          <button className="btn btn-ghost" onClick={onClose}>إغلاق</button>
        </div>
      </motion.div>
    </div>
  )
}
