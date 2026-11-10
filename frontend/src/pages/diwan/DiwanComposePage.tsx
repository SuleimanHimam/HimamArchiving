import { useEffect, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useDateFormat } from '../../lib/useCalendar'
import { Inbox, Send, X, Eye, Plus, PenLine, Paperclip, LayoutTemplate, Scan } from 'lucide-react'
import DesignPickerDialog from '../../components/DesignPickerDialog'
import RichTextEditor from '../../components/RichTextEditor'
import SearchSelect, { type SSOption } from '../../components/SearchSelect'
import MultiSearchSelect from '../../components/MultiSearchSelect'
import { FileUploadCard, type UploadedFile } from '../../components/ui/file-upload-card'
import DatePicker from '../../components/DatePicker'
import { useToast } from '../../components/toast'
import { diwan, DiwanDirection, type MailCategory } from '../../lib/diwan'
import { CLASS_VALUES } from '../../lib/internalMail'
import { canPrintMail, failMailPreview, previewDiwanDraft } from '../../lib/mailPrint'
import { closePreviewWindow } from '../../lib/reportTemplates'
import { external, ORG_TYPE_AR } from '../../lib/external'
import { organization } from '../../lib/organization'
import { scanAgent } from '../../lib/scanAgent'
import { scannerSettings } from '../../lib/scannerSettings'
import '../../styles/mail.css'
import '../../styles/compose.css'

const CLASSES = [{ v: 0, k: 'Public' }, { v: 1, k: 'Internal' }, { v: 2, k: 'Confidential' }, { v: 3, k: 'HighlyConfidential' }]

/** Register (وارد) or compose (صادر) an external Diwan letter. The counterparty is always an external
 * organization — internal staff are addressed from المراسلات instead. */
export default function DiwanComposePage() {
  const { t, i18n } = useTranslation()
  const { dateTime } = useDateFormat()
  const [designOpen, setDesignOpen] = useState(false)
  const isRtl = i18n.language.startsWith('ar')
  const navigate = useNavigate()
  const toast = useToast()
  const [params] = useSearchParams()
  const dirParam = Number(params.get('dir') ?? DiwanDirection.Outgoing)
  const replyTo = params.get('replyTo') ? Number(params.get('replyTo')) : null

  const [direction, setDirection] = useState<number>(dirParam === DiwanDirection.Incoming ? DiwanDirection.Incoming : DiwanDirection.Outgoing)
  const isIncoming = direction === DiwanDirection.Incoming

  const [subject, setSubject] = useState('')
  const [referenceNumber, setReferenceNumber] = useState('')
  const [orgId, setOrgId] = useState<number | null>(null)
  const [orgName, setOrgName] = useState('')
  const [addresseeName, setAddresseeName] = useState('')
  const [addresseeTitle, setAddresseeTitle] = useState('')
  const [classification, setClassification] = useState(1)
  const [mailDate, setMailDate] = useState(new Date().toISOString().slice(0, 10))
  const [issueDate, setIssueDate] = useState('')
  const [sentDate, setSentDate] = useState('')
  const [replyDueDate, setReplyDueDate] = useState('')
  const [categoryId, setCategoryId] = useState<number | ''>('')
  const [referenceToId, setReferenceToId] = useState<number | null>(null)
  const [referenceToLabel, setReferenceToLabel] = useState('')
  // الى ديوان — a letter may be routed to several bureaus at once.
  const [toDiwanIds, setToDiwanIds] = useState<number[]>([])
  const [fileNumber, setFileNumber] = useState('')
  const [remindOnNoReply, setRemindOnNoReply] = useState(false)
  const [keywords, setKeywords] = useState('')
  const [body, setBody] = useState('')

  const [orgs, setOrgs] = useState<SSOption[]>([])
  const [cats, setCats] = useState<MailCategory[]>([])
  const [units, setUnits] = useState<SSOption[]>([])
  const [priorMails, setPriorMails] = useState<SSOption[]>([])
  const [attachments, setAttachments] = useState<File[]>([])
  const [scanMsg, setScanMsg] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [tab, setTab] = useState<'content' | 'attachments'>('content')
  const scanFallback = useRef<HTMLInputElement>(null)

  useEffect(() => {
    external.orgs().then((r) => setOrgs(r.items.map((x) => ({ id: x.id, label: x.name, sub: ORG_TYPE_AR[x.type] ?? x.type })))).catch(() => { })
    diwan.categories().then(setCats).catch(() => { })
    organization.orgUnits().then((o) => setUnits(o.map((x) => ({ id: x.id, label: x.name })))).catch(() => { })
    // "مرجع الى" picks an earlier register entry.
    diwan.list({ pageSize: 100 }).then((r) => setPriorMails(
      r.items.map((x) => ({ id: x.id, label: x.registerNumber, sub: x.subject })))).catch(() => { })
  }, [])

  /* Replying: prefill from the letter being answered and flip the direction, so a reply
     to a وارد is registered as صادر and carries the original as its مرجع. */
  useEffect(() => {
    if (!replyTo) return
    diwan.get(replyTo).then((src) => {
      setDirection(src.direction === 'Incoming' ? DiwanDirection.Outgoing : DiwanDirection.Incoming)
      setSubject(src.subject?.startsWith('رد:') ? src.subject : `رد: ${src.subject ?? ''}`)
      setReferenceNumber(src.registerNumber)
      setOrgId(src.externalOrgId)
      setOrgName(src.externalOrgName ?? '')
      setAddresseeName(src.addresseeName ?? '')
      setAddresseeTitle(src.addresseeTitle ?? '')
      setClassification(CLASS_VALUES[src.classification] ?? 1)
      setCategoryId(src.categoryId ?? '')
      setReferenceToId(src.id)
      setReferenceToLabel(src.registerNumber)
      setToDiwanIds(src.toDiwanOrgUnits.map((u) => u.id))
      setFileNumber(src.fileNumber ?? '')
    }).catch(() => { })
  }, [replyTo])

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

  async function submit() {
    // الديوان: no field is mandatory — a clerk can register a letter with whatever is known
    // at the counter and complete the rest later.
    setError('')
    setSaving(true)
    try {
      // Replying goes through /reply so the chain links (ReplyToId/ParentId) are set server-side.
      const created = replyTo
        ? await diwan.reply(replyTo, {
            subject: subject.trim(),
            body,
            classification,
            priority: 1,
            mailDate: new Date(mailDate).toISOString(),
            dueDate: null,
            responseDeadline: replyDueDate ? new Date(replyDueDate).toISOString() : null,
            toDiwanOrgUnitIds: toDiwanIds,
          })
        : await diwan.create({
            direction, subject: subject.trim(), referenceNumber: referenceNumber || null,
            externalOrgId: orgId, externalOrgName: orgName || null,
            addresseeName: addresseeName || null, addresseeTitle: addresseeTitle || null,
            classification, priority: 1,
            mailDate: new Date(mailDate).toISOString(),
            issueDate: issueDate ? new Date(issueDate).toISOString() : null,
            sentDate: sentDate ? new Date(sentDate).toISOString() : null,
            dueDate: null,
            responseDeadline: replyDueDate ? new Date(replyDueDate).toISOString() : null,
            responsePeriodDays: null,
            remindOnNoReply, actionType: null,
            categoryId: categoryId === '' ? null : categoryId,
            referenceToId, toDiwanOrgUnitIds: toDiwanIds,
            description: null, body, keywords: keywords || null, fileNumber: fileNumber || null,
          })
      if (attachments.length > 0) {
        setScanMsg(t('corr.uploadingAttachments', { n: attachments.length }))
        for (const f of attachments) {
          try { await diwan.addAttachment(created.id, f) }
          catch { toast.error(t('corr.errors.attachmentFailed', { name: f.name })) }
        }
        setScanMsg('')
      }
      toast.success(t('corr.savedToast'))
      navigate(`/app/diwan/${created.id}`)
    } catch (e) {
      const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error
      setError(msg ?? t('corr.errors.saveFailed'))
    } finally { setSaving(false) }
  }

  /** Show the letter on its letterhead as it stands, without saving anything. */
  async function preview() {
    try {
      await previewDiwanDraft({
        subject, body, mailDate, sentDate, classification,
        categoryId: categoryId === '' ? null : categoryId,
        categoryName: cats.find((c) => c.id === categoryId)?.name ?? null,
        externalOrgName: orgName, addresseeName, addresseeTitle,
        fileNumber, keywords,
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

  // Staged attachments mapped to the upload card's shape (ready = "completed").
  const uploadItems: UploadedFile[] = attachments.map((f, i) => ({ id: String(i), file: f, progress: 100, status: 'completed' }))

  return (
    <div className="cx cx--full">
      {/* Solid action bar — title + خروج / اضافة, always in reach on any screen */}
      <header className="cx-bar">
        <div className="cx-bar__title">
          {isIncoming ? <Inbox className="cx-bar__icon" size={19} aria-hidden /> : <Send className="cx-bar__icon" size={19} aria-hidden />}
          <h1>{isIncoming ? t('corr.registerIncoming') : t('diwan.composeOutgoing')}</h1>
        </div>
        <div className="cx-bar__actions">
          <button type="button" className="cx-bar__btn cx-bar__btn--exit" onClick={() => navigate('/app/diwan')}>
            <X size={15} aria-hidden /> {t('common.exit', { defaultValue: 'خروج' })}
          </button>
          <button type="button" className="cx-bar__btn" onClick={() => void preview()}>
            <Eye size={15} aria-hidden /> {previewLabel}
          </button>
          <button type="button" className="cx-bar__btn cx-bar__btn--add" disabled={saving} onClick={submit}>
            <Plus size={15} aria-hidden /> {saving ? t('common.saving') : t('common.add', { defaultValue: 'إضافة' })}
          </button>
        </div>
      </header>

      <div className="cx-tabwrap">
        {/* Hidden picker — scanner fallback when no scan agent is present */}
        <input ref={scanFallback} type="file" accept=".pdf,image/*" style={{ display: 'none' }} onChange={(e) => { addFiles(e.target.files); if (scanFallback.current) scanFallback.current.value = '' }} />

        {/* Data + content in one pane */}
        <div className="cx-tabpane cx-tabpane--pad">
            <div className="cx-grid cx-grid--auto">
              <label className="cx-field">
                <span className="cx-fl">{t('corr.direction')}</span>
                <select value={direction} onChange={(e) => setDirection(Number(e.target.value))} aria-label={t('corr.direction')}>
                  <option value={DiwanDirection.Incoming}>{t('corr.dir.Incoming')}</option>
                  <option value={DiwanDirection.Outgoing}>{t('corr.dir.Outgoing')}</option>
                </select>
              </label>
              {/* Auto-assigned on save; shown read-only like the register form */}
              <label className="cx-field">
                <span className="cx-fl">{t('diwan.mailNumber')}</span>
                <input value={t('diwan.autoNew')} readOnly disabled className="mono" aria-label={t('diwan.mailNumber')} />
              </label>
              <label className="cx-field">
                <span className="cx-fl">{t('corr.date')}</span>
                <DatePicker value={mailDate} onChange={setMailDate} placeholder=" " />
              </label>
              <label className="cx-field">
                <span className="cx-fl">{t('diwan.issueDate')}</span>
                <DatePicker value={issueDate} onChange={setIssueDate} placeholder=" " />
              </label>
              <label className="cx-field">
                <span className="cx-fl">{t('diwan.sentDate')}</span>
                <DatePicker value={sentDate} onChange={setSentDate} placeholder=" " />
              </label>
              <label className="cx-field cx-field--2">
                <span className="cx-fl">{t('corr.subject')}</span>
                <input value={subject} onChange={(e) => setSubject(e.target.value)} placeholder=" " />
              </label>
              <label className="cx-field cx-field--2">
                <span className="cx-fl">{t('diwan.externalParty')}</span>
                <SearchSelect
                  options={orgs} value={orgName}
                  onPick={(o) => { setOrgName(o.label); setOrgId(o.id) }}
                  onText={(txt) => { setOrgName(txt); setOrgId(null) }}
                  placeholder=" " ariaLabel={t('diwan.externalParty')}
                  linked={orgId !== null} linkedTitle={t('diwan.linkedToDirectory')}
                />
              </label>
              {isIncoming && (
                <label className="cx-field">
                  <span className="cx-fl">{t('corr.reference')}</span>
                  <input value={referenceNumber} onChange={(e) => setReferenceNumber(e.target.value)} className="mono" placeholder=" " />
                </label>
              )}
              {!isIncoming && (
                <>
                  <label className="cx-field">
                    <span className="cx-fl">{t('corr.addresseeName')}</span>
                    <input value={addresseeName} onChange={(e) => setAddresseeName(e.target.value)} placeholder=" " />
                  </label>
                  <label className="cx-field">
                    <span className="cx-fl">{t('corr.addresseeTitle')}</span>
                    <input value={addresseeTitle} onChange={(e) => setAddresseeTitle(e.target.value)} placeholder=" " />
                  </label>
                </>
              )}
              <label className="cx-field cx-field--2">
                <span className="cx-fl">{t('diwan.referenceTo')}</span>
                <SearchSelect
                  options={priorMails} value={referenceToLabel}
                  onPick={(o) => { setReferenceToLabel(o.label); setReferenceToId(o.id) }}
                  onText={(txt) => { setReferenceToLabel(txt); setReferenceToId(null) }}
                  placeholder=" " ariaLabel={t('diwan.referenceTo')}
                />
              </label>
              <label className="cx-field">
                <span className="cx-fl">{t('diwan.mailCategory')}</span>
                {/* Searchable: the categories are admin-managed and the list only grows.
                    Clearing the text clears the choice, which is what the old "—" option did. */}
                <SearchSelect
                  options={cats.map((c) => ({ id: c.id, label: c.name }))}
                  value={cats.find((c) => c.id === categoryId)?.name ?? ''}
                  linked={categoryId !== ''}
                  showAllOnOpen
                  onPick={(o) => setCategoryId(o.id)}
                  onText={(txt) => { if (!txt.trim()) setCategoryId('') }}
                  placeholder=" " ariaLabel={t('diwan.mailCategory')}
                />
              </label>
              <label className="cx-field">
                <span className="cx-fl">{t('corr.keywords')}</span>
                <input value={keywords} onChange={(e) => setKeywords(e.target.value)} placeholder=" " />
              </label>
              {isIncoming && (
                <>
                  <label className="cx-field cx-field--2">
                    <span className="cx-fl">{t('diwan.toDiwan')}</span>
                    <MultiSearchSelect
                      options={units} value={toDiwanIds} onChange={setToDiwanIds}
                      placeholder=" " ariaLabel={t('diwan.toDiwan')}
                    />
                  </label>
                  <label className="cx-field">
                    <span className="cx-fl">{t('diwan.fileName')}</span>
                    <input value={fileNumber} onChange={(e) => setFileNumber(e.target.value)} placeholder=" " />
                  </label>
                  <label className="cx-field">
                    <span className="cx-fl">{t('corr.classification')}</span>
                    <select value={classification} onChange={(e) => setClassification(Number(e.target.value))} aria-label={t('corr.classification')}>
                      {CLASSES.map((c) => <option key={c.v} value={c.v}>{t(`corr.class.${c.k}`, { defaultValue: c.k })}</option>)}
                    </select>
                  </label>
                  <label className="cx-field">
                    <span className="cx-fl">{t('diwan.replyDueDate')}</span>
                    <DatePicker value={replyDueDate} onChange={setReplyDueDate} placeholder=" " />
                  </label>
                  <label className="cx-field cx-check">
                    <input type="checkbox" checked={remindOnNoReply} onChange={(e) => setRemindOnNoReply(e.target.checked)} />
                    <span className="cx-lbl">{t('corr.remindOnNoReply')}</span>
                  </label>
                </>
              )}
            </div>

            {/* Content / attachments tabs */}
            <nav className="cx-tabs" role="tablist">
              <button type="button" className="cx-tab" role="tab" aria-selected={tab === 'content'} data-on={tab === 'content'} onClick={() => setTab('content')}>
                <PenLine size={15} aria-hidden /> {t('corr.secContent')}
              </button>
              <button type="button" className="cx-tab" role="tab" aria-selected={tab === 'attachments'} data-on={tab === 'attachments'} onClick={() => setTab('attachments')}>
                <Paperclip size={15} aria-hidden /> {t('corr.attachments', { defaultValue: 'المرفقات' })}
                {attachments.length > 0 && <span className="cx-tab__count">{attachments.length}</span>}
              </button>
              {/* Not a tab: it opens the designs available to this writer. It sits here because
                  "which letterhead is this going out on" is a question asked while writing, not
                  one worth hunting through settings for. */}
              <button type="button" className="cx-tab cx-tab--action" onClick={() => setDesignOpen(true)}>
                <LayoutTemplate size={15} aria-hidden /> التصميم
              </button>
            </nav>

            <DesignPickerDialog open={designOpen} onClose={() => setDesignOpen(false)} />

            {tab === 'content' && (
              <div className="cx-editor">
                <RichTextEditor
                  value={body}
                  onChange={setBody}
                  placeholder={t('corr.bodyHint', { defaultValue: t('corr.body') })}
                  dir={isRtl ? 'rtl' : 'ltr'}
                  lang={isRtl ? 'ar' : 'en'}
                  onSave={() => { void submit() }}
                />
              </div>
            )}

            {tab === 'attachments' && (
              <FileUploadCard
                className="max-w-none"
                files={uploadItems}
                onFilesChange={(list) => setAttachments((a) => [...a, ...list])}
                onFileRemove={(id) => removeAttachment(Number(id))}
                headerAction={
                  <button type="button" className="cx-quick__btn cx-quick__btn--scan" onClick={startScan}>
                    <Scan size={15} aria-hidden /> {t('corr.scanFile')}
                  </button>
                }
              />
            )}
        </div>

        {scanMsg && <p className="cx-scanmsg">{scanMsg}</p>}
        {error && <p className="cx-error">{error}</p>}
      </div>
    </div>
  )
}
