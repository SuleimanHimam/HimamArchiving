import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import RichTextEditor, { type WordCountStats } from '../../components/RichTextEditor'
import { type SSOption } from '../../components/SearchSelect'
import MultiSearchSelect from '../../components/MultiSearchSelect'
import { FileUploadCard, type UploadedFile } from '../../components/ui/file-upload-card'
import DatePicker from '../../components/DatePicker'
import { Mail, X, Plus, PenLine, Paperclip, LayoutTemplate, Scan, Eye } from 'lucide-react'
import DesignPickerDialog from '../../components/DesignPickerDialog'
import { useDateFormat } from '../../lib/useCalendar'
import { useToast } from '../../components/toast'
import { canPrintMail, failMailPreview, previewInternalDraft } from '../../lib/mailPrint'
import { closePreviewWindow } from '../../lib/reportTemplates'
import { internalMail, Addressing, TargetKind, type RecipientInput } from '../../lib/internalMail'
import { organization } from '../../lib/organization'
import { diwan } from '../../lib/diwan'
import { groups } from '../../lib/groups'
import { scanAgent } from '../../lib/scanAgent'
import { scannerSettings } from '../../lib/scannerSettings'
import '../../styles/mail.css'
import '../../styles/compose.css'

const CLASSES = [{ v: 0, k: 'Public' }, { v: 1, k: 'Internal' }, { v: 2, k: 'Confidential' }, { v: 3, k: 'HighlyConfidential' }]
const KIND_LABEL: Record<number, string> = {
  [TargetKind.Employee]: 'corr.target.employee',
  [TargetKind.Department]: 'corr.target.department',
  [TargetKind.Group]: 'corr.target.group',
}

const initials = (name: string) =>
  name.trim().split(/\s+/).slice(0, 2).map((w) => w[0] ?? '').join('').toUpperCase() || '؟'

interface RecipRow { addressing: number; targetKind: number; targetId: string; targetName: string }

// Recipient options come from three separate directories whose ids overlap, so the kind is
// folded into the option id for the flat picker list and split back out when building rows.
const KIND_SPAN = 10_000_000
const enc = (kind: number, id: number) => kind * KIND_SPAN + id
const dec = (v: number) => ({ kind: Math.floor(v / KIND_SPAN), id: v % KIND_SPAN })

/** Compose an internal message (or a reply/forward). Recipients are internal only — employees,
 * departments, or groups; external parties belong to الديوان. */
export default function InternalComposePage() {
  const { t, i18n } = useTranslation()
  const isRtl = i18n.language.startsWith('ar')
  const navigate = useNavigate()
  const { dateTime } = useDateFormat()
  const toast = useToast()
  const [params] = useSearchParams()
  const replyTo = params.get('replyTo') ? Number(params.get('replyTo')) : null
  const forwardOf = params.get('forward') ? Number(params.get('forward')) : null
  const replyAll = params.get('all') === '1'
  /* A new message always opens blank. Nothing is carried over from a previous session — the form
     is only ever prefilled by an explicit رد or تحويل, below. */
  const [subject, setSubject] = useState('')
  // Kept in state (loaded on reply) but no longer shown — internal compose matches the
  // simplified form: recipients, subject, reply-due date, body, attachments.
  const [classification, setClassification] = useState(1)
  const [dueDate, setDueDate] = useState('')
  /* نوع المراسلة — what kind of message this is. It picks the letterhead the message prints on,
     and it is the key a workflow route is bound to: a route configured for this type starts the
     moment the message is sent. Left off the form, every new message went out unclassified and
     every configured route sat waiting for a trigger that never came. */
  const [categoryId, setCategoryId] = useState('')
  const [mailTypes, setMailTypes] = useState<{ id: number; name: string }[]>([])
  const [body, setBody] = useState('')
  const [rows, setRows] = useState<RecipRow[]>([])
  // BCC is hidden until the user asks for it.
  const [showBcc, setShowBcc] = useState(false)

  const [users, setUsers] = useState<SSOption[]>([])
  const [depts, setDepts] = useState<SSOption[]>([])
  const [grps, setGrps] = useState<SSOption[]>([])
  const [attachments, setAttachments] = useState<File[]>([])
  // Matches the external register: content and attachments are separate panes, and the design
  // in use is reachable while writing rather than buried in settings.
  const [tab, setTab] = useState<'content' | 'attachments'>('content')
  const [designOpen, setDesignOpen] = useState(false)
  const [scanMsg, setScanMsg] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const scanFallback = useRef<HTMLInputElement>(null)

  // Editor stats (word/character count under the content box).
  const [stats, setStats] = useState<WordCountStats>({ words: 0, chars: 0, pages: 1 })

  useEffect(() => {
    organization.users().then((u) => setUsers(u.map((x) => ({ id: x.id, label: x.fullName, sub: x.email, avatar: initials(x.fullName) })))).catch(() => { })
    organization.orgUnits().then((o) => setDepts(o.map((x) => ({ id: x.id, label: x.name })))).catch(() => { })
    // Only the groups this user belongs to: a group is addressable from inside it.
    groups.list(true, true).then((g) => setGrps(g.map((x) => ({ id: x.id, label: x.name })))).catch(() => { })
    // An outgoing-only classification cannot apply to internal correspondence, so it is not offered.
    diwan.categories()
      .then((c) => setMailTypes(c.filter((x) => x.isActive && x.scope !== 'External').map((x) => ({ id: x.id, name: x.name }))))
      .catch(() => { })
  }, [])

  // Prefill when replying/forwarding.
  useEffect(() => {
    const src = replyTo ?? forwardOf
    if (src == null) return
    internalMail.get(src).then((m) => {
      setSubject(forwardOf ? `تحويل: ${m.subject}` : `رد: ${m.subject}`)
      setClassification(CLASSES.find((c) => c.k === m.classification)?.v ?? 1)
      // A reply to a salary slip is still about a salary slip — keep the type.
      if (m.categoryId != null) setCategoryId(String(m.categoryId))
    }).catch(() => { })
  }, [replyTo, forwardOf])

  // The pickers work on one flat option list, so the target kind is folded into the option id
  // and split back out when writing rows. `rows` stays the source of truth, which keeps submit
  // and reply prefill untouched.
  const allTargets: SSOption[] = useMemo(() => [
    ...users.map((o) => ({ ...o, id: enc(TargetKind.Employee, o.id) })),
    ...depts.map((o) => ({ ...o, id: enc(TargetKind.Department, o.id), sub: t(KIND_LABEL[TargetKind.Department]) })),
    ...grps.map((o) => ({ ...o, id: enc(TargetKind.Group, o.id), sub: t(KIND_LABEL[TargetKind.Group]) })),
  ], [users, depts, grps, t])

  const idsFor = (addressing: number) => rows
    .filter((r) => r.addressing === addressing && r.targetId)
    .map((r) => enc(r.targetKind, Number(r.targetId)))

  const setIdsFor = (addressing: number, ids: number[]) => setRows((prev) => [
    ...prev.filter((r) => r.addressing !== addressing),
    ...ids.map((v) => {
      const { kind, id } = dec(v)
      return { addressing, targetKind: kind, targetId: String(id), targetName: allTargets.find((o) => o.id === v)?.label ?? '' }
    }),
  ])

  function addFiles(list: FileList | null) {
    if (!list || list.length === 0) return
    setAttachments((a) => [...a, ...Array.from(list)])
  }
  const removeAttachment = (i: number) => setAttachments((a) => a.filter((_, idx) => idx !== i))

  async function startScan() {
    setError(''); setScanMsg(t('corr.scan.connecting'))
    try {
      const status = await scanAgent.status()
      if (!status) { setScanMsg(''); scanFallback.current?.click(); return }
      const chosen = scannerSettings.get() ?? undefined
      setScanMsg(chosen ? t('corr.scan.scanningFrom', { name: chosen }) : t('corr.scan.scanning'))
      const { blob, fileName } = await scanAgent.scan('pdf', { ...scannerSettings.getScanPrefs(), scanner: chosen })
      setAttachments((a) => [...a, new File([blob], fileName, { type: blob.type || 'application/pdf' })])
      setScanMsg(''); toast.success(t('corr.scan.done'))
    } catch (err) {
      setScanMsg('')
      const m = (err as Error)?.message || t('corr.scan.failed')
      setError(m); toast.error(m)
    }
  }

  const toInputs = (): RecipientInput[] => rows
    .filter((r) => r.targetId || r.targetName)
    .map((r) => ({ addressing: r.addressing, targetKind: r.targetKind, targetId: r.targetId ? Number(r.targetId) : null, targetName: r.targetName || null }))

  // Staged attachments mapped to the upload card's shape (ready = "completed").
  const uploadItems: UploadedFile[] = attachments.map((f, i) => ({ id: String(i), file: f, progress: 100, status: 'completed' }))

  async function submit() {
    setError('')
    // Subject + at least one recipient are enforced by the server; catch them here to
    // surface a clear message and open the relevant section, without "required" field markers.
    if (!subject.trim()) { setError(t('corr.errors.subjectRequired')); return }
    const recipients = toInputs()
    if (recipients.length === 0) { setError(t('corr.errors.recipientRequired')); return }
    setSaving(true)
    try {
      let created
      if (replyTo != null) {
        created = await internalMail.reply(replyTo, { subject, body, classification, priority: 1, replyAll, additionalRecipients: recipients })
      } else if (forwardOf != null) {
        created = await internalMail.forward(forwardOf, { body, recipients })
      } else {
        created = await internalMail.compose({
          subject: subject.trim(), referenceNumber: null,
          classification, priority: 1, dueDate: null,
          responseDeadline: dueDate ? new Date(dueDate).toISOString() : null,
          responsePeriodDays: null,
          remindOnNoReply: false, actionType: null,
          categoryId: categoryId ? Number(categoryId) : null,
          description: null, body, keywords: null, fileNumber: null, recipients,
        })
      }
      if (attachments.length > 0) {
        setScanMsg(t('corr.uploadingAttachments', { n: attachments.length }))
        for (const f of attachments) {
          try { await internalMail.addAttachment(created.id, f) }
          catch { toast.error(t('corr.errors.attachmentFailed', { name: f.name })) }
        }
        setScanMsg('')
      }
      toast.success(t('corr.savedToast'))
      navigate(`/app/internal/${created.id}`)
    } catch (e) {
      const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error
      setError(msg ?? t('corr.errors.saveFailed'))
    } finally { setSaving(false) }
  }

  /** Show the message on its letterhead as it stands, without sending anything. */
  async function preview() {
    try {
      await previewInternalDraft({
        subject, body, classification,
        categoryId: categoryId ? Number(categoryId) : null,
        // The To line is what a letterhead addresses; Cc and Bcc are routing, not the address.
        recipientNames: rows.filter((r) => r.addressing === Addressing.To).map((r) => r.targetName),
        attachments: attachments.map((f) => ({ name: f.name, size: f.size })),
      }, t, dateTime)
    } catch {
      failMailPreview(t('corr.errors.previewFailed'))
      toast.error(t('corr.errors.previewFailed'))
    }
  }

  /* Someone who may print lands straight in the print dialog — a preview with a printer
     attached — so the button says so rather than surprising them with it. */
  const previewLabel = canPrintMail()
    ? t('corr.previewPrint', { defaultValue: 'معاينة وطباعة' })
    : t('corr.preview')

  // Leaving the form closes the preview it opened — it is about this draft and nothing else.
  useEffect(() => closePreviewWindow, [])

  const title = replyTo ? t('corr.reply') : forwardOf ? t('corr.forward') : t('corr.compose')

  return (
    <div className="cx cx--full cx--internal">
      {/* Solid action bar — title + خروج / حفظ مسودة / إرسال, always in reach on any screen */}
      <header className="cx-bar">
        <div className="cx-bar__title">
          <Mail className="cx-bar__icon" size={19} aria-hidden />
          <h1>{title}</h1>
        </div>
        <div className="cx-bar__actions">
          <button type="button" className="cx-bar__btn cx-bar__btn--exit" onClick={() => navigate('/app/internal')}>
            <X size={15} aria-hidden /> {t('common.cancel')}
          </button>
          <button type="button" className="cx-bar__btn" onClick={() => void preview()}>
            <Eye size={15} aria-hidden /> {previewLabel}
          </button>
          <button type="button" className="cx-bar__btn cx-bar__btn--add" disabled={saving} onClick={submit}>
            <Plus size={15} aria-hidden /> {saving ? t('common.saving') : t('corr.send')}
          </button>
        </div>
      </header>

      <div className="cx-tabwrap">
        {/* Hidden picker — scanner fallback when no scan agent is present */}
        <input ref={scanFallback} type="file" accept=".pdf,image/*" style={{ display: 'none' }} onChange={(e) => { addFiles(e.target.files); if (scanFallback.current) scanFallback.current.value = '' }} />

        {/* Data + recipients + content merged into one pane */}
        <div className="cx-tabpane cx-tabpane--pad">
          {/* Two rows — recipients, then subject + reply-due date. BCC joins the
              recipients row as a third column instead of adding a row. */}
          <div className="cx-rows">
            <div className="cx-rows__recips">
              <label className="cx-field">
                <span className="cx-fl">{t('corr.to')}</span>
                <MultiSearchSelect
                  options={allTargets}
                  value={idsFor(Addressing.To)}
                  onChange={(ids) => setIdsFor(Addressing.To, ids)}
                  placeholder={t('corr.searchRecipient')}
                  ariaLabel={t('corr.to')}
                />
              </label>

              <label className="cx-field">
                <span className="cx-fl">{t('corr.cc')}</span>
                <MultiSearchSelect
                  options={allTargets}
                  value={idsFor(Addressing.Cc)}
                  onChange={(ids) => setIdsFor(Addressing.Cc, ids)}
                  placeholder={t('corr.searchRecipient')}
                  ariaLabel={t('corr.cc')}
                />
              </label>

              {showBcc && (
                <label className="cx-field">
                  <span className="cx-fl">{t('corr.bcc')}</span>
                  <MultiSearchSelect
                    options={allTargets}
                    value={idsFor(Addressing.Bcc)}
                    onChange={(ids) => setIdsFor(Addressing.Bcc, ids)}
                    placeholder={t('corr.searchRecipient')}
                    ariaLabel={t('corr.bcc')}
                  />
                </label>
              )}
            </div>
            <button
              type="button"
              className="cx-bcc-toggle"
              onClick={() => { if (showBcc) setIdsFor(Addressing.Bcc, []); setShowBcc((v) => !v) }}
            >
              {showBcc ? t('corr.hideBcc') : t('corr.showBcc')}
            </button>

            <div className="cx-rows__split">
              <label className="cx-field">
                <span className="cx-fl">{t('corr.subject')}</span>
                <input value={subject} onChange={(e) => setSubject(e.target.value)} placeholder=" " />
              </label>
              <label className="cx-field">
                <span className="cx-fl">{t('diwan.replyDueDate')}</span>
                <DatePicker value={dueDate} onChange={setDueDate} placeholder=" " />
              </label>
            </div>

            {/* Optional on an ordinary message; on a request it is what starts the route. */}
            <div className="cx-rows__split">
              <label className="cx-field">
                <span className="cx-fl">نوع المراسلة</span>
                <select value={categoryId} onChange={(e) => setCategoryId(e.target.value)}>
                  <option value="">— بلا تصنيف —</option>
                  {mailTypes.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </label>
            </div>
          </div>
          <p className="cx-recip__hint">{t('internal.internalOnlyHint')}</p>

          <nav className="cx-tabs" role="tablist">
            <button type="button" className="cx-tab" role="tab" aria-selected={tab === 'content'}
              data-on={tab === 'content'} onClick={() => setTab('content')}>
              <PenLine size={15} aria-hidden /> {t('corr.secContent')}
            </button>
            <button type="button" className="cx-tab" role="tab" aria-selected={tab === 'attachments'}
              data-on={tab === 'attachments'} onClick={() => setTab('attachments')}>
              <Paperclip size={15} aria-hidden /> {t('corr.attachments', { defaultValue: 'المرفقات' })}
              {attachments.length > 0 && <span className="cx-tab__count">{attachments.length}</span>}
            </button>
            {/* Not a tab: internal messages resolve against the Correspondence register, so this
                picker offers that register's designs — not the outgoing-letter ones. */}
            <button type="button" className="cx-tab cx-tab--action" onClick={() => setDesignOpen(true)}>
              <LayoutTemplate size={15} aria-hidden /> التصميم
            </button>
          </nav>

          <DesignPickerDialog open={designOpen} onClose={() => setDesignOpen(false)} target={0} />

          {tab === 'content' && <>
          <div className="cx-editor">
                <RichTextEditor
                  value={body}
                  onChange={setBody}
                  onWordCount={setStats}
                  placeholder={t('corr.bodyHint', { defaultValue: t('corr.body') })}
                  dir={isRtl ? 'rtl' : 'ltr'}
                  lang={isRtl ? 'ar' : 'en'}
                  onSave={() => { void submit() }}
                />
          </div>
          <div className="cx-editor__foot">
            <span>{t('editor.words', { defaultValue: 'الكلمات' })}: <b>{stats.words.toLocaleString(i18n.language)}</b></span>
            <span>{t('editor.chars', { defaultValue: 'الأحرف' })}: <b>{stats.chars.toLocaleString(i18n.language)}</b></span>
          </div>

          </>}

          {tab === 'attachments' && <FileUploadCard
            compact
            className="max-w-none shrink-0"
            files={uploadItems}
            onFilesChange={(list) => setAttachments((a) => [...a, ...list])}
            onFileRemove={(id) => removeAttachment(Number(id))}
            headerAction={
              <button type="button" className="cx-quick__btn cx-quick__btn--scan" onClick={startScan}>
                <Scan size={15} aria-hidden /> {t('corr.scanFile')}
              </button>
            }
          />}
        </div>

        {scanMsg && <p className="cx-scanmsg">{scanMsg}</p>}
        {error && <p className="cx-error">{error}</p>}
      </div>
    </div>
  )
}
