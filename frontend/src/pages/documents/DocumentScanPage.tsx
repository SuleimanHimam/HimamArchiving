import { useEffect, useState, useRef } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { documents, type DocumentTypeDto, type OrgUnitDto, type ScanFormat } from '../../lib/documents'
import { scanAgent } from '../../lib/scanAgent'
import { scannerSettings } from '../../lib/scannerSettings'
import { type Confidentiality } from '../../lib/incomingMail'
import { useToast } from '../../components/toast'
import '../incoming/incoming.css'
import './documents.css'

export default function DocumentScanPage() {
  const navigate = useNavigate()
  const toast = useToast()
  const fallbackInput = useRef<HTMLInputElement>(null)
  const [types, setTypes] = useState<DocumentTypeDto[]>([])
  const [units, setUnits] = useState<OrgUnitDto[]>([])
  const [form, setForm] = useState({
    title: '', documentTypeId: '', owningOrgUnitId: '',
    confidentiality: 1 as Confidentiality, keywords: '',
  })
  const [format, setFormat] = useState<ScanFormat>('pdf')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [msg, setMsg] = useState('')

  useEffect(() => {
    Promise.all([documents.types(), documents.orgUnits()])
      .then(([t, u]) => {
        setTypes(t); setUnits(u)
        setForm((f) => ({
          ...f,
          documentTypeId: t[0] ? String(t[0].id) : '',
          owningOrgUnitId: u[0] ? String(u[0].id) : '',
        }))
      })
      .catch(() => setError('تعذّر تحميل أنواع الوثائق والوحدات التنظيمية'))
  }, [])

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
    setForm((f) => ({ ...f, [k]: e.target.value }))

  // Name and type are mandatory before any scan.
  function validate(): string | null {
    if (!form.title.trim()) return 'يجب إدخال اسم الوثيقة'
    if (!form.documentTypeId) return 'يجب اختيار نوع الوثيقة'
    if (!form.owningOrgUnitId) return 'يجب اختيار الوحدة المالكة'
    return null
  }

  async function startScan() {
    const v = validate()
    if (v) { setError(v); return }
    setError(''); setBusy(true); setMsg('جارٍ الاتصال بالماسح الضوئي…')
    try {
      const status = await scanAgent.status()
      if (!status) {
        setMsg(''); setBusy(false)
        fallbackInput.current?.click() // no agent → pick a scanned file instead
        return
      }
      const chosen = scannerSettings.get() ?? undefined
      setMsg(chosen ? `جارٍ المسح من: ${chosen}…` : 'جارٍ المسح الضوئي…')
      const { blob } = await scanAgent.scan('jpeg', chosen)
      await createAndAttach(blob)
    } catch (err) {
      reportError(err, 'تعذّر المسح الضوئي')
      setBusy(false); setMsg('')
    }
  }

  async function onFallbackFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (fallbackInput.current) fallbackInput.current.value = ''
    if (!file) return
    const v = validate()
    if (v) { setError(v); return }
    setBusy(true); setError('')
    try {
      await createAndAttach(file)
    } catch (err) {
      reportError(err, 'تعذّر رفع المسح الضوئي')
      setBusy(false); setMsg('')
    }
  }

  // Create the document of the chosen type, then attach the scan in the chosen format (server-side).
  async function createAndAttach(blob: Blob) {
    setMsg('جارٍ إنشاء الوثيقة وإرفاق المسح…')
    const created = await documents.create({
      title: form.title.trim(),
      documentTypeId: Number(form.documentTypeId),
      owningOrgUnitId: Number(form.owningOrgUnitId),
      confidentiality: Number(form.confidentiality) as Confidentiality,
      keywords: form.keywords || null,
      documentDate: new Date().toISOString().slice(0, 10),
    })
    await documents.scan(created.id, blob, `${form.title.trim()}.${format}`, format)
    toast.success('تم إنشاء الوثيقة وإرفاق المسح الضوئي')
    navigate(`/app/documents/${created.id}`, { replace: true })
  }

  function reportError(err: unknown, fallback: string) {
    const e = err as AxiosError<{ error?: string }> & { message?: string }
    const m = e.response?.data?.error ?? e.message ?? fallback
    setError(m)
    toast.error(m)
  }

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">SCAN · مسح وثيقة جديدة</span>
          <h1>مسح وثيقة جديدة</h1>
        </div>
        <Link to="/app/documents" className="btn btn-ghost">← رجوع للقائمة</Link>
      </header>

      <motion.div className="doc-card form-card" initial={{ opacity: 0, y: 14 }} animate={{ opacity: 1, y: 0 }}>
        <div className="form-grid">
          <label className="field field--wide"><span>اسم الوثيقة *</span>
            <input value={form.title} onChange={set('title')} placeholder="مثال: محضر اجتماع مجلس الإدارة" /></label>
          <label className="field"><span>نوع الوثيقة *</span>
            <select value={form.documentTypeId} onChange={set('documentTypeId')}>
              {types.length === 0 && <option value="">— لا توجد أنواع —</option>}
              {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select></label>
          <label className="field"><span>الوحدة المالكة *</span>
            <select value={form.owningOrgUnitId} onChange={set('owningOrgUnitId')}>
              {units.length === 0 && <option value="">— لا توجد وحدات —</option>}
              {units.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
            </select></label>
          <label className="field"><span>السرية</span>
            <select value={form.confidentiality} onChange={set('confidentiality')}>
              <option value={0}>عام</option><option value={1}>داخلي</option>
              <option value={2}>سري</option><option value={3}>سري للغاية</option>
            </select></label>
          <label className="field"><span>صيغة المستند الممسوح *</span>
            <select value={format} onChange={(e) => setFormat(e.target.value as ScanFormat)}>
              <option value="pdf">PDF</option>
              <option value="jpg">JPG (صورة)</option>
              <option value="png">PNG (صورة)</option>
            </select></label>
          <label className="field field--wide"><span>الكلمات المفتاحية</span>
            <input value={form.keywords} onChange={set('keywords')} placeholder="مفصولة بمسافات" /></label>
        </div>

        {error && <p className="login__error">{error}</p>}
        {msg && <p className="muted scan-msg">{msg}</p>}

        <input ref={fallbackInput} type="file" style={{ display: 'none' }} onChange={onFallbackFile}
          accept=".pdf,.jpg,.jpeg,.png" />
        <div className="form-actions">
          <button className="btn btn-seal" disabled={busy} onClick={startScan}>
            {busy ? '…جارٍ التنفيذ' : '⎙ مسح وإنشاء الوثيقة'}
          </button>
          <Link to="/app/documents" className="btn btn-ghost">إلغاء</Link>
        </div>
      </motion.div>
    </div>
  )
}
