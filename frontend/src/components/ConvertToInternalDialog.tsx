import { useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Dialog, DialogContent } from './ui/dialog'
import { Tabs, TabsContent, TabsList, TabsTrigger } from './ui/tabs'
import MultiSearchSelect from './MultiSearchSelect'
import RichTextEditor from './RichTextEditor'
import DatePicker from './DatePicker'
import { LabeledInput } from './ui/labeled-input'
import type { SSOption } from './SearchSelect'
import { Send, X, User, Users, FileText, Paperclip, Scan } from 'lucide-react'
import { useToast } from './toast'
import { diwan, type DiwanListItem } from '../lib/diwan'
import { Addressing, TargetKind, internalMail } from '../lib/internalMail'
import { organization } from '../lib/organization'
import { groups } from '../lib/groups'
import { scanAgent } from '../lib/scanAgent'
import { scannerSettings } from '../lib/scannerSettings'

const fmtBytes = (n: number) =>
  n < 1024 ? `${n} B` : n < 1048576 ? `${Math.round(n / 1024)} KB` : `${(n / 1048576).toFixed(1)} MB`

/* Employees, departments and groups share one picker, but their ids overlap across kinds —
   so options carry a composite key and are decoded back into (kind, id) on submit. */
const KEY_STEP = 1_000_000
const encode = (kind: number, id: number) => kind * KEY_STEP + id
const decode = (key: number) => ({ kind: Math.floor(key / KEY_STEP), id: key % KEY_STEP })

/** Convert a Diwan register entry into an internal message (تحويل البريد الى مراسلة).
 * The register entry stays in place; the internal copy cites its register number. */
export default function ConvertToInternalDialog({
  item, open, onOpenChange,
}: { item: DiwanListItem | null; open: boolean; onOpenChange: (o: boolean) => void }) {
  const { t, i18n } = useTranslation()
  const ar = i18n.language.startsWith('ar')
  const navigate = useNavigate()
  const toast = useToast()

  const [users, setUsers] = useState<SSOption[]>([])
  const [depts, setDepts] = useState<SSOption[]>([])
  const [grps, setGrps] = useState<SSOption[]>([])

  const [to, setTo] = useState<number[]>([])
  const [cc, setCc] = useState<number[]>([])
  const [notes, setNotes] = useState('')
  const [subject, setSubject] = useState('')
  const [replyDue, setReplyDue] = useState('')
  const [body, setBody] = useState('')

  const [copyFiles, setCopyFiles] = useState(true)
  const [extras, setExtras] = useState<File[]>([])
  const [dragOver, setDragOver] = useState(false)
  const [scanMsg, setScanMsg] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const fileInput = useRef<HTMLInputElement>(null)
  const scanFallback = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (!open) return
    organization.users().then((u) => setUsers(u.map((x) => ({
      id: encode(TargetKind.Employee, x.id), label: x.fullName, sub: x.email,
    })))).catch(() => { })
    organization.orgUnits().then((o) => setDepts(o.map((x) => ({
      id: encode(TargetKind.Department, x.id), label: x.name, sub: ar ? 'إدارة' : 'Department',
    })))).catch(() => { })
    // Only the groups this user belongs to: a group is addressable from inside it.
    groups.list(true, true).then((g) => setGrps(g.map((x) => ({
      id: encode(TargetKind.Group, x.id), label: x.name, sub: ar ? 'مجموعة' : 'Group',
    })))).catch(() => { })
  }, [open, ar])

  const everyone = useMemo(() => [...users, ...depts, ...grps], [users, depts, grps])

  // Seed the form from the letter being converted whenever the dialog opens on a new row.
  useEffect(() => {
    setTo([]); setCc([]); setNotes(''); setReplyDue('')
    setSubject(item?.subject ?? '')
    setBody('')
    setCopyFiles(true); setExtras([]); setScanMsg(''); setError('')
  }, [item?.id, open])

  const addFiles = (list: FileList | null) => {
    if (!list?.length) return
    setExtras((a) => [...a, ...Array.from(list)])
  }

  async function startScan() {
    setError(''); setScanMsg(ar ? 'جارِ الاتصال بالماسح…' : 'Connecting to scanner…')
    try {
      const status = await scanAgent.status()
      if (!status) { setScanMsg(''); scanFallback.current?.click(); return }
      const chosen = scannerSettings.get() ?? undefined
      setScanMsg(ar ? 'جارِ المسح…' : 'Scanning…')
      const { blob, fileName } = await scanAgent.scan('pdf', { ...scannerSettings.getScanPrefs(), scanner: chosen })
      setExtras((a) => [...a, new File([blob], fileName, { type: blob.type || 'application/pdf' })])
      setScanMsg('')
    } catch (e) {
      setScanMsg('')
      setError((e as Error)?.message || (ar ? 'تعذّر المسح الضوئي' : 'Scan failed'))
    }
  }

  const recipientsOf = (keys: number[], addressing: number) => keys.map((k) => {
    const { kind, id } = decode(k)
    return {
      addressing, targetKind: kind, targetId: id,
      targetName: everyone.find((o) => o.id === k)?.label ?? null,
    }
  })

  async function submit() {
    if (!item || to.length === 0) return
    setError(''); setBusy(true)
    try {
      const { internalMailId } = await diwan.convertToInternal(item.id, {
        subject: subject.trim() || null,
        body: body || null,
        notes: notes.trim() || null,
        classification: null, priority: null, actionType: null,
        dueDate: null,
        responseDeadline: replyDue ? new Date(replyDue).toISOString() : null,
        copyAttachments: copyFiles,
        recipients: [...recipientsOf(to, Addressing.To), ...recipientsOf(cc, Addressing.Cc)],
      })

      let failed = 0
      if (extras.length) {
        setScanMsg(ar ? `جارِ رفع ${extras.length} مرفق…` : `Uploading ${extras.length} file(s)…`)
        for (const f of extras) {
          try { await internalMail.addAttachment(internalMailId, f) }
          catch { failed++ }
        }
        setScanMsg('')
      }

      // The message exists either way — report partial success rather than claiming a clean run.
      if (failed > 0) {
        toast.error(ar
          ? `تم التحويل، لكن تعذّر رفع ${failed} من ${extras.length} مرفق`
          : `Converted, but ${failed} of ${extras.length} file(s) failed to upload`)
      } else {
        toast.success(ar ? 'تم التحويل إلى مراسلة داخلية' : 'Converted to an internal message')
      }
      onOpenChange(false)
      navigate(`/app/internal/${internalMailId}`)
    } catch (e) {
      const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error
      setError(msg ?? t('corr.errors.saveFailed'))
    } finally { setBusy(false) }
  }

  const kindLabel = (kind: number) =>
    kind === TargetKind.Employee ? (ar ? 'موظف' : 'Employee')
      : kind === TargetKind.Department ? (ar ? 'إدارة' : 'Department')
        : (ar ? 'مجموعة' : 'Group')

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="cti">
        <header className="cti__titlebar">
          <h2 className="cti__title">{ar ? 'تحويل البريد الى مراسلة' : 'Convert mail to a message'}</h2>
          <span className="cti__ref">
            {item ? `${item.registerNumber} — ${item.subject || (ar ? '(بلا موضوع)' : '(no subject)')}` : ''}
          </span>
        </header>

        {/* Action bar — send / exit, always reachable at the top. */}
        <div className="cti__bar">
          <button type="button" className="cti__send" disabled={busy || to.length === 0} onClick={submit}>
            <Send size={15} aria-hidden /> {busy ? t('common.saving') : (ar ? 'ارسال' : 'Send')}
          </button>
          <button type="button" className="cti__exit" onClick={() => onOpenChange(false)}>
            <X size={15} aria-hidden /> {ar ? 'خروج' : 'Exit'}
          </button>
        </div>

        <div className="cti__body">
          <div className="cti__row">
            <span className="cti__ico" aria-hidden><User size={16} /></span>
            <label className="cti__field">
              <span>{ar ? 'الى' : 'To'}</span>
              <MultiSearchSelect
                options={everyone} value={to} onChange={setTo}
                placeholder={ar ? 'ابحث عن موظف أو إدارة أو مجموعة…' : 'Search staff, departments, groups…'}
                ariaLabel={ar ? 'الى' : 'To'}
              />
            </label>
          </div>

          <div className="cti__row">
            <span className="cti__ico" aria-hidden><Users size={16} /></span>
            <label className="cti__field">
              <span>{ar ? 'نسخة الى' : 'Cc'}</span>
              <MultiSearchSelect
                options={everyone} value={cc} onChange={setCc}
                placeholder={ar ? 'نسخة إلى…' : 'Copy to…'}
                ariaLabel={ar ? 'نسخة الى' : 'Cc'}
              />
            </label>
          </div>

          <div className="cti__row">
            <span className="cti__ico" aria-hidden><FileText size={16} /></span>
            <LabeledInput containerClassName="flex-1" label={ar ? 'ملاحظات' : 'Notes'}
              value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>

          <Tabs defaultValue="content" className="cti__tabs">
            <TabsList>
              <TabsTrigger value="content">{ar ? 'المحتوى' : 'Content'}</TabsTrigger>
              <TabsTrigger value="files">
                {ar ? 'المرفقات' : 'Attachments'}
                {extras.length > 0 && <span className="cti__count">{extras.length}</span>}
              </TabsTrigger>
              <TabsTrigger value="routing">
                {ar ? 'محوّل الى' : 'Routed to'}
                {to.length + cc.length > 0 && <span className="cti__count">{to.length + cc.length}</span>}
              </TabsTrigger>
            </TabsList>

            <TabsContent value="content">
              <div className="cti__grid">
                <LabeledInput label={ar ? 'الموضوع' : 'Subject'} value={subject} onChange={(e) => setSubject(e.target.value)} />
                <label className="cti__field">
                  <DatePicker value={replyDue} onChange={setReplyDue}
                    placeholder={ar ? 'تاريخ الرد على البريد' : 'Reply deadline'} />
                </label>
              </div>
              <div className="cti__editor">
                <RichTextEditor
                  value={body} onChange={setBody}
                  placeholder={ar ? 'اكتب المحتوى…' : 'Write the content…'}
                  dir={ar ? 'rtl' : 'ltr'} lang={ar ? 'ar' : 'en'}
                />
              </div>
            </TabsContent>

            <TabsContent value="files">
              <label className="cti__check">
                <input type="checkbox" checked={copyFiles} onChange={(e) => setCopyFiles(e.target.checked)} />
                <span>{ar ? 'نسخ مرفقات البريد الأصلي' : 'Copy the original letter’s attachments'}</span>
              </label>

              <div
                className={`corr-dz ${dragOver ? 'is-drag' : ''}`}
                onDragOver={(e) => { e.preventDefault(); setDragOver(true) }}
                onDragLeave={() => setDragOver(false)}
                onDrop={(e) => { e.preventDefault(); setDragOver(false); addFiles(e.dataTransfer.files) }}
              >
                <div className="corr-dz__actions">
                  <button type="button" className="corr-dz__btn" onClick={() => fileInput.current?.click()}>
                    <Paperclip className="corr-dz__btn-ic" size={16} aria-hidden /><span>{t('corr.attachFile')}</span>
                  </button>
                  <button type="button" className="corr-dz__btn corr-dz__btn--scan" onClick={startScan}>
                    <Scan className="corr-dz__btn-ic" size={16} aria-hidden /><span>{t('corr.scanFile')}</span>
                  </button>
                </div>
                <p className="corr-dz__hint">{scanMsg || t('corr.dropHint')}</p>
                <input ref={fileInput} type="file" multiple style={{ display: 'none' }}
                  onChange={(e) => { addFiles(e.target.files); if (fileInput.current) fileInput.current.value = '' }} />
                <input ref={scanFallback} type="file" accept=".pdf,image/*" style={{ display: 'none' }}
                  onChange={(e) => { addFiles(e.target.files); if (scanFallback.current) scanFallback.current.value = '' }} />
                {extras.length > 0 && (
                  <ul className="corr-att__list">
                    {extras.map((f, i) => (
                      <li key={i} className="corr-att__item">
                        <span className="corr-att__name"><FileText size={14} aria-hidden /> {f.name}</span>
                        <span className="muted mono">{fmtBytes(f.size)}</span>
                        <button type="button" className="corr-att__x" aria-label={t('common.remove')}
                          onClick={() => setExtras((a) => a.filter((_, idx) => idx !== i))}><X size={14} aria-hidden /></button>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </TabsContent>

            <TabsContent value="routing">
              {to.length + cc.length === 0 ? (
                <p className="cti__empty">{ar ? 'لم يتم اختيار أي مستلم بعد' : 'No recipients chosen yet'}</p>
              ) : (
                <ul className="cti__routes">
                  {[...to.map((k) => [k, ar ? 'الى' : 'To'] as const),
                    ...cc.map((k) => [k, ar ? 'نسخة' : 'Cc'] as const)].map(([k, role]) => {
                    const { kind } = decode(k)
                    return (
                      <li key={`${role}-${k}`}>
                        <span className="cti__route-role">{role}</span>
                        <span className="cti__route-name">{everyone.find((o) => o.id === k)?.label ?? '—'}</span>
                        <span className="cti__route-kind">{kindLabel(kind)}</span>
                      </li>
                    )
                  })}
                </ul>
              )}
            </TabsContent>
          </Tabs>

          {error && <p className="login__error">{error}</p>}
        </div>
      </DialogContent>
    </Dialog>
  )
}
