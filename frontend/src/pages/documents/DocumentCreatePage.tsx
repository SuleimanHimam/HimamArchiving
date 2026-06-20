import { useEffect, useState, useRef, type FormEvent } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { useTranslation } from 'react-i18next'
import { documents, type DocumentTypeDto, type OrgUnitDto, type ScanFormat } from '../../lib/documents'
import { archive, type PhysicalLocationDto } from '../../lib/archive'
import { scanAgent } from '../../lib/scanAgent'
import { scannerSettings } from '../../lib/scannerSettings'
import { type Confidentiality } from '../../lib/incomingMail'
import { useToast } from '../../components/toast'
import '../incoming/incoming.css'

const today = new Date().toISOString().slice(0, 10)

export default function DocumentCreatePage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const toast = useToast()
  const fallbackInput = useRef<HTMLInputElement>(null)
  const [types, setTypes] = useState<DocumentTypeDto[]>([])
  const [units, setUnits] = useState<OrgUnitDto[]>([])
  const [locations, setLocations] = useState<PhysicalLocationDto[]>([])
  const [form, setForm] = useState({
    title: '', description: '', documentTypeId: '', owningOrgUnitId: '',
    confidentiality: 1 as Confidentiality, keywords: '', documentDate: today,
    physicalLocationId: '', boxNumber: '', fileNumber: '',
  })
  const [format, setFormat] = useState<ScanFormat>('pdf')
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const [scanMsg, setScanMsg] = useState('')

  useEffect(() => {
    Promise.all([documents.types(), documents.orgUnits()])
      .then(([t, u]) => {
        const active = t.filter((x) => x.isActive)
        setTypes(active)
        setUnits(u)
        setForm((f) => ({
          ...f,
          documentTypeId: active[0] ? String(active[0].id) : '',
          owningOrgUnitId: u[0] ? String(u[0].id) : '',
        }))
      })
      .catch(() => setError(t('documents.loadError')))
    archive.locations().then(setLocations).catch(() => {})
  }, [t])

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) =>
    setForm((f) => ({ ...f, [k]: e.target.value }))

  function validate(): string | null {
    if (!form.title || !form.documentTypeId || !form.owningOrgUnitId) return t('documents.create.errors.required')
    return null
  }

  function buildCreateBody() {
    return {
      title: form.title,
      description: form.description || null,
      documentTypeId: Number(form.documentTypeId),
      owningOrgUnitId: Number(form.owningOrgUnitId),
      confidentiality: Number(form.confidentiality) as Confidentiality,
      keywords: form.keywords || null,
      documentDate: form.documentDate || null,
      physicalLocationId: form.physicalLocationId ? Number(form.physicalLocationId) : null,
      boxNumber: form.boxNumber || null,
      fileNumber: form.fileNumber || null,
    }
  }

  // Create only (metadata).
  async function submit(e: FormEvent) {
    e.preventDefault()
    setError('')
    const v = validate(); if (v) { setError(v); return }
    setSaving(true)
    try {
      const created = await documents.create(buildCreateBody())
      navigate(`/app/documents/${created.id}`, { replace: true })
    } catch (err) {
      const ax = err as AxiosError<{ error?: string }>
      setError(ax.response?.data?.error ?? t('documents.create.errors.failed'))
    } finally { setSaving(false) }
  }

  // Create + attach a scanned page in one step.
  async function createAndAttach(blob: Blob) {
    setScanMsg('جارٍ إنشاء الوثيقة وإرفاق المسح…')
    const created = await documents.create(buildCreateBody())
    await documents.scan(created.id, blob, `${form.title.trim()}.${format}`, format)
    toast.success('تم إنشاء الوثيقة وإرفاق المسح الضوئي')
    navigate(`/app/documents/${created.id}`, { replace: true })
  }

  async function startScan() {
    const v = validate(); if (v) { setError(v); return }
    setError(''); setSaving(true); setScanMsg('جارٍ الاتصال بالماسح الضوئي…')
    try {
      const status = await scanAgent.status()
      if (!status) { setScanMsg(''); setSaving(false); fallbackInput.current?.click(); return }
      const chosen = scannerSettings.get() ?? undefined
      setScanMsg(chosen ? `جارٍ المسح من: ${chosen}…` : 'جارٍ المسح الضوئي…')
      const { blob } = await scanAgent.scan('jpeg', chosen)
      await createAndAttach(blob)
    } catch (err) {
      reportScanError(err)
    }
  }

  async function onFallbackFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (fallbackInput.current) fallbackInput.current.value = ''
    if (!file) return
    const v = validate(); if (v) { setError(v); return }
    setSaving(true); setError('')
    try { await createAndAttach(file) }
    catch (err) { reportScanError(err) }
  }

  function reportScanError(err: unknown) {
    const e = err as AxiosError<{ error?: string }> & { message?: string }
    const m = e.response?.data?.error ?? e.message ?? 'تعذّر المسح الضوئي'
    setError(m); toast.error(m); setSaving(false); setScanMsg('')
  }

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">{t('documents.create.kicker')}</span>
          <h1>{t('documents.create.title')}</h1>
        </div>
        <Link to="/app/documents" className="btn btn-ghost">{t('documents.create.back')}</Link>
      </header>

      <motion.form className="doc-card form-card" onSubmit={submit} initial={{ opacity: 0, y: 14 }} animate={{ opacity: 1, y: 0 }}>
        <div className="form-grid">
          <label className="field field--wide"><span>{t('documents.create.titleField')} *</span>
            <input value={form.title} onChange={set('title')} /></label>
          <label className="field"><span>{t('documents.create.type')} *</span>
            <select value={form.documentTypeId} onChange={set('documentTypeId')}>
              {types.length === 0 && <option value="">—</option>}
              {types.map((tp) => <option key={tp.id} value={tp.id}>{tp.name}</option>)}
            </select></label>
          <label className="field"><span>{t('documents.columns.category')}</span>
            <select value={form.owningOrgUnitId} onChange={set('owningOrgUnitId')}>
              {units.length === 0 && <option value="">—</option>}
              {units.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
            </select></label>
          <label className="field"><span>{t('incoming.columns.confidentiality')}</span>
            <select value={form.confidentiality} onChange={set('confidentiality')}>
              <option value={0}>{t('common.confidentiality.public')}</option>
              <option value={1}>{t('common.confidentiality.internal')}</option>
              <option value={2}>{t('common.confidentiality.confidential')}</option>
              <option value={3}>{t('common.confidentiality.highlyConfidential')}</option>
            </select></label>
          <label className="field"><span>{t('documents.columns.date')}</span>
            <input type="date" value={form.documentDate} onChange={set('documentDate')} dir="ltr" /></label>
          <label className="field"><span>صيغة المستند الممسوح</span>
            <select value={format} onChange={(e) => setFormat(e.target.value as ScanFormat)}>
              <option value="pdf">PDF</option>
              <option value="jpg">JPG (صورة)</option>
              <option value="png">PNG (صورة)</option>
            </select></label>
          <label className="field field--wide"><span>{t('incoming.create.keywords')}</span>
            <input value={form.keywords} onChange={set('keywords')} placeholder={t('incoming.create.keywordsPlaceholder')} /></label>
          <label className="field field--wide"><span>{t('documents.create.body')}</span>
            <textarea rows={4} value={form.description} onChange={set('description')} /></label>

          {locations.length > 0 && (
            <>
              <label className="field"><span>{t('archive.title')}</span>
                <select value={form.physicalLocationId} onChange={set('physicalLocationId')}>
                  <option value="">—</option>
                  {locations.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
                </select></label>
              <label className="field"><span>{t('archive.fields.code')}</span>
                <input value={form.boxNumber} onChange={set('boxNumber')} dir="ltr" /></label>
              <label className="field"><span>{t('archive.fields.name')}</span>
                <input value={form.fileNumber} onChange={set('fileNumber')} dir="ltr" /></label>
            </>
          )}
        </div>

        {error && <p className="login__error">{error}</p>}
        {scanMsg && <p className="muted scan-msg">{scanMsg}</p>}

        <input ref={fallbackInput} type="file" style={{ display: 'none' }} onChange={onFallbackFile} accept=".pdf,.jpg,.jpeg,.png" />

        <div className="form-actions">
          <button className="btn btn-primary" disabled={saving}>
            {saving ? t('documents.create.submitting') : t('documents.create.submit')}
          </button>
          <button type="button" className="btn btn-seal" disabled={saving} onClick={startScan}>
            ⎙ {t('documents.scanButton')}
          </button>
          <Link to="/app/documents" className="btn btn-ghost">{t('documents.create.cancel')}</Link>
        </div>
      </motion.form>
    </div>
  )
}
