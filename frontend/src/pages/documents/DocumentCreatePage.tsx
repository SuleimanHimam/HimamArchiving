import { File, FileText, FileType, FileSpreadsheet, FileImage, Scan, type LucideIcon, ArrowRight } from 'lucide-react'
import { useEffect, useState, useRef, type FormEvent } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { useTranslation } from 'react-i18next'
import { CloudUpload, FolderOpen, Plus, ScanLine, X } from 'lucide-react'
import { documents, formatBytes, type DocumentTypeDto, type OrgUnitDto } from '../../lib/documents'
import { auth } from '../../lib/auth'
import LocationPicker from '../../components/LocationPicker'
import { LabeledInput, LabeledSelect, LabeledTextarea } from '../../components/ui/labeled-input'
import { scanAgent } from '../../lib/scanAgent'
import { scannerSettings } from '../../lib/scannerSettings'
import { type Confidentiality } from '../../lib/shared'
import { useToast } from '../../components/toast'
import '../../styles/forms.css'

const today = new Date().toISOString().slice(0, 10)

/** A staged file pending upload, with an animated progress percent (0→100). */
interface StagedFile {
  id: string
  file: File
  progress: number
}

const FILE_ICON: Record<string, LucideIcon> = {
  pdf: FileType, doc: FileText, docx: FileText, xls: FileSpreadsheet, xlsx: FileSpreadsheet,
  jpg: FileImage, jpeg: FileImage, png: FileImage, gif: FileImage,
}

export default function DocumentCreatePage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const toast = useToast()
  const scanInput = useRef<HTMLInputElement>(null)
  const dropInput = useRef<HTMLInputElement>(null)
  const [units, setUnits] = useState<OrgUnitDto[]>([])
  const [types, setTypes] = useState<DocumentTypeDto[]>([])
  const [form, setForm] = useState({
    title: '', description: '', documentTypeId: '', owningOrgUnitId: '',
    confidentiality: 1 as Confidentiality, keywords: '', documentDate: today,
  })
  const [boxId, setBoxId] = useState<number | null>(null)
  /* Filing a paper original on a shelf is the archive department's job. Everyone else registers the
     document and hands it to the archive, which assigns the place as part of intake — so for them the
     location picker is replaced by that handover. The API enforces the same rule. */
  const canFilePhysically = auth.hasPermission('Archive.Archive')
  const [sendToArchive, setSendToArchive] = useState(true)
  /* Maintaining the type catalogue is a classification privilege, so the inline "new type" row is
     offered only to those who already have it — everyone else is pointed at an administrator rather
     than shown a button that would come back 403. */
  const canAddType = auth.hasPermission('Classification.Edit')
  const [newType, setNewType] = useState<{ name: string; shared: boolean } | null>(null)
  const [addingType, setAddingType] = useState(false)
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const [scanMsg, setScanMsg] = useState('')
  const [staged, setStaged] = useState<StagedFile[]>([])
  const [dragOver, setDragOver] = useState(false)

  useEffect(() => {
    documents.orgUnits()
      .then((u) => {
        setUnits(u)
        setForm((f) => ({ ...f, owningOrgUnitId: f.owningOrgUnitId || (u[0] ? String(u[0].id) : '') }))
      })
      .catch(() => setError(t('documents.loadError')))
  }, [t])

  // Types belong to a department (or are عام), so they are re-fetched for the owning unit —
  // otherwise the auto-selected default could be a type from an unrelated department.
  useEffect(() => {
    if (!form.owningOrgUnitId) return
    documents.types(Number(form.owningOrgUnitId))
      .then((fetched) => {
        const active = fetched.filter((x) => x.isActive)
        setTypes(active)
        setForm((f) => (active.some((x) => String(x.id) === f.documentTypeId)
          ? f
          : { ...f, documentTypeId: active[0] ? String(active[0].id) : '' }))
      })
      .catch(() => setError(t('documents.loadError')))
  }, [form.owningOrgUnitId, t])

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) =>
    setForm((f) => ({ ...f, [k]: e.target.value }))

  /** Creates a document type without leaving the form, and selects it.
   *
   *  Registering a document is exactly when someone discovers the type they need does not exist, and
   *  sending them to Settings means abandoning a half-filled form. The retention and confidentiality
   *  defaults match the settings screen, where they can be refined later. */
  async function addType() {
    const name = newType?.name.trim()
    if (!name) return
    if (types.some((x) => x.name.trim() === name)) {
      toast.error('يوجد نوع بهذا الاسم بالفعل'); return
    }
    setAddingType(true)
    try {
      const created = await documents.createType({
        name,
        defaultConfidentiality: Number(form.confidentiality),
        retentionValue: 10,
        retentionUnit: 2,          // years
        retentionMonths: 0,        // derived server-side from the pair above
        requiresApproval: false,
        allowedUploadSources: 15,  // All
        isActive: true,
        orgUnitId: newType!.shared ? null : Number(form.owningOrgUnitId),
      })
      setTypes((prev) => [...prev, created])
      setForm((f) => ({ ...f, documentTypeId: String(created.id) }))
      setNewType(null)
      setError('')
      toast.success(`تم إنشاء النوع «${created.name}» واختياره`)
    } catch (err) {
      const ax = err as AxiosError<{ error?: string }>
      toast.error(ax.response?.data?.error ?? 'تعذّر إنشاء النوع')
    } finally { setAddingType(false) }
  }

  /* Names the field that is actually empty. A fixed "title and type are required" sends someone
     hunting through a form where the title is filled and the real gap is elsewhere — and when a
     department has no active types there is nothing to pick, which the message has to say outright
     rather than leave as an empty dropdown. */
  function validate(): string | null {
    const missing: string[] = []
    if (!form.title.trim()) missing.push(t('documents.create.titleField'))
    if (!form.owningOrgUnitId) missing.push('الوحدة المالكة')
    if (!form.documentTypeId) {
      if (form.owningOrgUnitId && types.length === 0) {
        return canAddType ? t('documents.create.errors.noTypesCanAdd') : t('documents.create.errors.noTypes')
      }
      missing.push(t('documents.create.type'))
    }
    if (missing.length === 0) return null
    return t('documents.create.errors.missing', { fields: missing.join('، ') })
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
      boxId: canFilePhysically ? boxId : null,
    }
  }

  /** Hands a freshly registered document to the archive department. Best-effort: the document is
   *  already saved, and a type that needs no physical archiving refuses this — which is not a
   *  failure of the registration, so it is reported as a note rather than an error. */
  async function handOverToArchive(id: number) {
    if (canFilePhysically || !sendToArchive) return
    try {
      await documents.sendToArchive(id)
      toast.success('أُرسلت الوثيقة إلى قسم الأرشيف لتحديد مكان الحفظ')
    } catch (err) {
      const ax = err as AxiosError<{ error?: string }>
      toast.toast(ax.response?.data?.error ?? 'تعذّر الإرسال إلى قسم الأرشيف — يمكنك إرسالها من صفحة الوثيقة', 'info')
    }
  }

  // Stage dropped/selected files with an animated progress bar (prototype initDz).
  function stageFiles(files: FileList | File[]) {
    const incoming = Array.from(files)
    const next: StagedFile[] = incoming.map((file, i) => ({
      id: `${Date.now()}-${i}-${file.name}`,
      file,
      progress: 0,
    }))
    setStaged((prev) => [...prev, ...next])
    next.forEach((sf, i) => {
      setTimeout(() => {
        setStaged((prev) => prev.map((p) => (p.id === sf.id ? { ...p, progress: 100 } : p)))
      }, 80 + i * 120)
    })
  }

  function removeStaged(id: string) {
    setStaged((prev) => prev.filter((p) => p.id !== id))
  }

  function onDragOver(e: React.DragEvent) { e.preventDefault(); setDragOver(true) }
  function onDragLeave(e: React.DragEvent) { e.preventDefault(); setDragOver(false) }
  function onDrop(e: React.DragEvent) {
    e.preventDefault(); setDragOver(false)
    if (e.dataTransfer.files.length) stageFiles(e.dataTransfer.files)
  }

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError('')
    const v = validate(); if (v) { setError(v); return }
    setSaving(true)
    try {
      const created = await documents.create(buildCreateBody())
      for (const sf of staged) {
        await documents.upload(created.id, sf.file)
      }
      if (staged.length > 0) toast.success(`تم رفع ${staged.length} مرفق`)
      await handOverToArchive(created.id)
      navigate(`/app/documents/${created.id}`, { replace: true })
    } catch (err) {
      const ax = err as AxiosError<{ error?: string }>
      setError(ax.response?.data?.error ?? t('documents.create.errors.failed'))
    } finally { setSaving(false) }
  }

  async function createAndAttach(blob: Blob) {
    setScanMsg('جارٍ إنشاء الوثيقة وإرفاق المسح…')
    const created = await documents.create(buildCreateBody())
    await documents.scan(created.id, blob, `${form.title.trim()}.pdf`, 'pdf')
    toast.success('تم إنشاء الوثيقة وإرفاق المسح الضوئي')
    await handOverToArchive(created.id)
    navigate(`/app/documents/${created.id}`, { replace: true })
  }

  async function startScan() {
    const v = validate(); if (v) { setError(v); return }
    setError(''); setSaving(true); setScanMsg('جارٍ الاتصال بالماسح الضوئي…')
    try {
      const status = await scanAgent.status()
      if (!status) { setScanMsg(''); setSaving(false); scanInput.current?.click(); return }
      const chosen = scannerSettings.get() ?? undefined
      setScanMsg(chosen ? `جارٍ المسح من: ${chosen}…` : 'جارٍ المسح الضوئي…')
      const { blob } = await scanAgent.scan('jpeg', chosen)
      await createAndAttach(blob)
    } catch (err) {
      reportScanError(err)
    }
  }

  async function onScanFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (scanInput.current) scanInput.current.value = ''
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
    <div style={{ width: 'min(900px, 100%)', margin: '0 auto' }}>
      <header className="page__head" style={{ marginBottom: '1rem' }}>
        <div>
          <h1>{t('documents.create.title')}</h1>
        </div>
        <Link to="/app/documents" className="btn btn-ghost"><ArrowRight size={16} className="icon-dir" aria-hidden /> {t('documents.create.back')}</Link>
      </header>

      {/* Interactive drag-drop upload zone (prototype .dz) */}
      <motion.div
        className={`dz dz--compact ${dragOver ? 'is-over' : ''}`}
        initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
        onDragOver={onDragOver} onDragLeave={onDragLeave} onDrop={onDrop}
        onClick={() => dropInput.current?.click()}
        style={{ marginBottom: 12 }}
      >
        <span className="dz__icon"><CloudUpload size={30} /></span>
        <div className="dz__title">{dragOver ? 'أفلت الملفات هنا…' : 'اسحب وأفلت الملفات هنا، أو انقر للتصفح'}</div>
        <div className="dz__hint">المدعوم: PDF، Word، Excel، صور، مستندات ممسوحة</div>
        <div className="dz__actions">
          <button type="button" className="btn btn-ghost btn-sm" onClick={(e) => { e.stopPropagation(); dropInput.current?.click() }}>
            <FolderOpen size={14} style={{ marginInlineEnd: 6 }} /> اختر ملفات
          </button>
          <button type="button" className="btn btn-seal btn-sm" disabled={saving} onClick={(e) => { e.stopPropagation(); startScan() }}>
            <ScanLine size={14} style={{ marginInlineEnd: 6 }} /> {t('documents.scanButton')}
          </button>
        </div>
      </motion.div>
      <input ref={dropInput} type="file" multiple style={{ display: 'none' }} onChange={(e) => { if (e.target.files) stageFiles(e.target.files) }} />

      {/* Staged files with progress bars */}
      {staged.length > 0 && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginBottom: 16 }}>
          {staged.map((sf) => {
            const ext = sf.file.name.split('.').pop()?.toLowerCase() ?? ''
            return (
              <motion.div key={sf.id} className="upload-item" initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }}>
                {(() => { const Ic = FILE_ICON[ext] ?? File; return <Ic size={18} aria-hidden /> })()}
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div className="upload-item__name">{sf.file.name}</div>
                  <div className="upload-item__size">{formatBytes(sf.file.size)}</div>
                </div>
                <div style={{ width: 90 }}>
                  <div className="pb"><div className="pf" style={{ width: `${sf.progress}%` }} /></div>
                </div>
                <button type="button" className="btn btn-ghost btn-sm" style={{ width: 26, height: 26, padding: 0, display: 'grid', placeItems: 'center' }} onClick={() => removeStaged(sf.id)} title="إزالة">
                  <X size={12} />
                </button>
              </motion.div>
            )
          })}
        </div>
      )}

      <motion.form className="doc-card form-card" onSubmit={submit} initial={{ opacity: 0, y: 14 }} animate={{ opacity: 1, y: 0 }}>
        <div className="form-grid form-grid--auto">
          <LabeledInput label={t('documents.create.titleField')} required containerClassName="field--wide"
            value={form.title} onChange={set('title')} />
          <LabeledSelect label="الوحدة المالكة" required value={form.owningOrgUnitId} onChange={set('owningOrgUnitId')}
            hint={units.length === 0 ? 'لا توجد وحدات تنظيمية متاحة لك — راجع مدير النظام.' : undefined}>
            {units.length === 0 && <option value="">—</option>}
            {units.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
          </LabeledSelect>
          <div className="doc-type-field">
            <LabeledSelect label={t('documents.create.type')} required value={form.documentTypeId} onChange={set('documentTypeId')}
              hint={form.owningOrgUnitId && types.length === 0
                ? (canAddType ? t('documents.create.errors.noTypesCanAdd') : t('documents.create.errors.noTypes'))
                : undefined}>
              {types.length === 0 && <option value="">—</option>}
              {types.map((tp) => <option key={tp.id} value={tp.id}>{tp.name}</option>)}
            </LabeledSelect>
            {canAddType && !newType && (
              <button type="button" className="btn btn-ghost btn-sm doc-type-field__add"
                onClick={() => setNewType({ name: '', shared: false })}>
                <Plus size={14} aria-hidden /> نوع جديد
              </button>
            )}
          </div>

          {newType && (
            <div className="field--wide new-type">
              <LabeledInput label="اسم النوع الجديد" autoFocus value={newType.name}
                onChange={(e) => setNewType({ ...newType, name: e.target.value })}
                onKeyDown={(e) => {
                  // Enter here must not submit the half-filled document behind the row.
                  if (e.key === 'Enter') { e.preventDefault(); addType() }
                  if (e.key === 'Escape') setNewType(null)
                }} />
              <label className="new-type__scope">
                <input type="checkbox" checked={newType.shared}
                  onChange={(e) => setNewType({ ...newType, shared: e.target.checked })} />
                <span>متاح لكل الوحدات (نوع عام)</span>
              </label>
              <div className="new-type__actions">
                <button type="button" className="btn btn-primary btn-sm" disabled={addingType || !newType.name.trim()}
                  onClick={addType}>{addingType ? 'جارٍ الحفظ…' : 'إضافة'}</button>
                <button type="button" className="btn btn-ghost btn-sm" disabled={addingType}
                  onClick={() => setNewType(null)}>إلغاء</button>
              </div>
              <p className="new-type__hint">
                يُنشأ بمدّة حفظ ١٠ سنوات ودرجة سرية مطابقة للوثيقة — يمكن ضبطهما لاحقًا من الإعدادات ← أنواع الوثائق.
              </p>
            </div>
          )}
          <LabeledSelect label={t('incoming.columns.confidentiality')} value={form.confidentiality} onChange={set('confidentiality')}>
            <option value={0}>{t('common.confidentiality.public')}</option>
            <option value={1}>{t('common.confidentiality.internal')}</option>
            <option value={2}>{t('common.confidentiality.confidential')}</option>
            <option value={3}>{t('common.confidentiality.highlyConfidential')}</option>
          </LabeledSelect>
          <LabeledInput label={t('documents.columns.date')} type="date" dir="ltr"
            value={form.documentDate} onChange={set('documentDate')} />
          <LabeledInput label={t('incoming.create.keywords')} containerClassName="field--wide"
            value={form.keywords} onChange={set('keywords')} />
          <LabeledTextarea label={t('documents.create.body')} containerClassName="field--wide" rows={2}
            value={form.description} onChange={set('description')} />

          {canFilePhysically ? (
            <LocationPicker value={boxId} onChange={setBoxId} />
          ) : (
            <div className="field--wide archive-handover">
              <label className="archive-handover__opt">
                <input type="checkbox" checked={sendToArchive}
                  onChange={(e) => setSendToArchive(e.target.checked)} />
                <span>إرسال الوثيقة إلى قسم الأرشيف بعد التسجيل</span>
              </label>
              <p className="archive-handover__hint">
                تحديد مكان الحفظ الفعلي (المبنى والغرفة والصندوق) من اختصاص قسم الأرشيف — يتولاه عند استلام الوثيقة.
              </p>
            </div>
          )}

        </div>

        {error && <p className="login__error">{error}</p>}
        {scanMsg && <p className="muted scan-msg">{scanMsg}</p>}

        <input ref={scanInput} type="file" style={{ display: 'none' }} onChange={onScanFile} accept=".pdf" />

        <div className="form-actions">
          <button className="btn btn-primary" disabled={saving}>
            {saving ? t('documents.create.submitting') : t('documents.create.submit')}
          </button>
          <button type="button" className="btn btn-seal" disabled={saving} onClick={startScan}>
            <Scan size={16} aria-hidden /> {t('documents.scanButton')}
          </button>
          <Link to="/app/documents" className="btn btn-ghost">{t('documents.create.cancel')}</Link>
        </div>
      </motion.form>
    </div>
  )
}