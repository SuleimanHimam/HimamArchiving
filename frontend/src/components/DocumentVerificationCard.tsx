import { useEffect, useState } from 'react'
import { QrCode, Printer, Copy, ExternalLink } from 'lucide-react'
import { documents, type DocumentVerification } from '../lib/documents'
import { useToast } from './toast'
import './documentVerification.css'

/* ختم التحقق — the QR that answers «هل هذه الورقة من هذا النظام؟».
 *
 * The square is rendered by the server, not here: the same image URL then prints from this page,
 * from a browser print, and from any PDF the server builds, so what is on the paper is always what
 * the register will recognise. This component only fetches the code (minting it on first view for
 * documents registered before verification existed) and gives it somewhere to be printed from. */

/** The API's origin, since the SPA and the API are not always the same host. */
const apiBase = (import.meta.env.VITE_API_URL as string | undefined) ?? 'http://localhost:5105/api'
const serverOrigin = apiBase.replace(/\/api\/?$/, '')

export default function DocumentVerificationCard({ documentId }: { documentId: number }) {
  const toast = useToast()
  const [info, setInfo] = useState<DocumentVerification | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    let alive = true
    documents.verification(documentId)
      .then((v) => { if (alive) setInfo(v) })
      .catch(() => { if (alive) setFailed(true) })
    return () => { alive = false }
  }, [documentId])

  if (failed || !info) return null

  const qrUrl = `${serverOrigin}${info.qrPath}`
  // Where a scan lands. Built from the page the user is on, not from the API host: the QR the
  // server draws already encodes its own public address, and this is only the human-readable copy.
  const verifyUrl = `${window.location.origin}${info.verifyPath}`

  async function copyCode() {
    try {
      await navigator.clipboard.writeText(info!.code)
      toast.success('نُسخ رمز التحقق')
    } catch { toast.error('تعذّر النسخ') }
  }

  /** Prints the card alone — a stamp to attach to the paper copy, not the whole page. */
  function printCard() {
    const w = window.open('', '_blank', 'width=420,height=520')
    if (!w) { toast.error('تعذّر فتح نافذة الطباعة'); return }
    w.document.write(`<!doctype html><html dir="rtl" lang="ar"><head><meta charset="utf-8">
      <title>ختم التحقق — ${info!.documentNumber}</title>
      <style>
        body { font-family: "Segoe UI", Tahoma, sans-serif; text-align: center; padding: 24px; }
        img { inline-size: 190px; block-size: 190px; }
        h1 { font-size: 15px; margin: 12px 0 4px; }
        .num { font-size: 13px; color: #444; }
        .code { font-family: Consolas, monospace; letter-spacing: .14em; margin-top: 8px; font-size: 13px; }
        .hint { font-size: 11px; color: #666; margin-top: 10px; line-height: 1.7; }
      </style></head><body>
      <img src="${qrUrl}" alt="">
      <h1>للتحقق من صحة هذه الوثيقة</h1>
      <div class="num">رقم الوثيقة: ${info!.documentNumber}</div>
      <div class="code">${info!.code}</div>
      <div class="hint">امسح الرمز ضوئيًا، أو افتح<br>${verifyUrl}</div>
      <script>window.onload=function(){setTimeout(function(){window.print()},150)}<\/script>
      </body></html>`)
    w.document.close()
  }

  return (
    <section className="dvf">
      <div className="dvf__head">
        <QrCode size={16} aria-hidden />
        <h4>ختم التحقق</h4>
      </div>

      <img className="dvf__qr" src={qrUrl} alt={`رمز التحقق للوثيقة ${info.documentNumber}`} />

      <p className="dvf__code mono">{info.code}</p>
      <p className="dvf__hint">
        اطبع هذا الرمز على الورقة. من يمسحه يصل إلى صفحة تؤكّد أن الوثيقة صادرة عن النظام — دون كشف محتواها.
      </p>

      <div className="dvf__acts">
        <button type="button" className="btn btn-ghost btn-sm" onClick={printCard}>
          <Printer size={14} aria-hidden /> طباعة الختم
        </button>
        <button type="button" className="btn btn-ghost btn-sm" onClick={copyCode}>
          <Copy size={14} aria-hidden /> نسخ الرمز
        </button>
        <a className="btn btn-ghost btn-sm" href={verifyUrl} target="_blank" rel="noreferrer">
          <ExternalLink size={14} aria-hidden /> معاينة الصفحة
        </a>
      </div>
    </section>
  )
}
