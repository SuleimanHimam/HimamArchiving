import { useEffect, useState } from 'react'
import { Search, X } from 'lucide-react'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { disposition, DESTRUCTION_METHODS, DISPOSITION_ACTION } from '../lib/disposition'
import { destructionMethods } from '../lib/destruction'
import { documents, type DocumentListItem } from '../lib/documents'
import { LabeledInput, LabeledSelect, LabeledTextarea } from './ui/labeled-input'
import { useToast } from './toast'
import './request-disposition.css'

/** DestructionMethod.Other — the enum slot that means "the text in customMethod is the method". */
const OTHER = 7
const OTHER_LABEL = 'أخرى (حدّد الطريقة)'

/** Form to raise a disposition request (Destroy or Renew) for a document.
 *
 * Opened from a document, it acts on that one. Opened from the disposition screen it opens on a
 * picker of the documents the user may act on — which, because the documents API is scoped to the
 * caller, is their own department's records and their own. It used to ask for a numeric document
 * id instead, a number nobody knows by heart and which no screen displays. */
export default function RequestDispositionModal({
  documentId, documentNumber, onClose, onDone,
}: { documentId?: number; documentNumber?: string; onClose: () => void; onDone?: () => void }) {
  const toast = useToast()
  const [picked, setPicked] = useState<DocumentListItem | null>(null)
  const [search, setSearch] = useState('')
  const [choices, setChoices] = useState<DocumentListItem[] | null>(null)
  const docId = documentId ?? picked?.id ?? 0
  const [action, setAction] = useState<number>(DISPOSITION_ACTION.Destroy)
  /* Methods come from the admin-managed catalogue (الإعدادات ← طرق الإتلاف), the same list the
     الإتلاف screen uses. This form used to render a hardcoded copy, so renaming or disabling a
     method in Settings changed nothing here. The built-in labels remain only as the fallback
     for a caller who cannot read the catalogue. */
  const [methodOptions, setMethodOptions] = useState<string[]>(
    // "أخرى" is appended separately below as the free-text choice.
    DESTRUCTION_METHODS.filter((m) => m.value !== OTHER).map((m) => m.label),
  )
  const [methodLabel, setMethodLabel] = useState('')
  const [customMethod, setCustomMethod] = useState('')
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)

  const isDestroy = action === DISPOSITION_ACTION.Destroy
  /* Derived, not synced into state: the catalogue arrives after the first render, and an effect
     that "corrects" the stored value would re-render on every list change. An empty (or stale)
     selection simply falls back to the first offered method. */
  const chosenMethod = methodLabel && [...methodOptions, OTHER_LABEL].includes(methodLabel)
    ? methodLabel
    : methodOptions[0] ?? OTHER_LABEL
  const isOther = chosenMethod === OTHER_LABEL

  /* The department's documents, narrowed as the user types. Debounced because the field is a
     free-text search against the register, not a local filter over a page already loaded. */
  useEffect(() => {
    if (documentId) return                       // opened on a specific document — no picker
    const t = setTimeout(() => {
      documents.list({ search: search.trim() || undefined, page: 1, pageSize: 25 })
        .then((r) => setChoices(r.items))
        .catch(() => setChoices([]))
    }, search ? 300 : 0)
    return () => clearTimeout(t)
  }, [documentId, search])

  useEffect(() => {
    destructionMethods.list()
      .then((list) => {
        const active = list.filter((m) => m.isActive).map((m) => m.label).filter((l) => l !== OTHER_LABEL)
        if (active.length) setMethodOptions(active)
      })
      .catch(() => { /* keep the built-in labels */ })
  }, [])


  async function submit() {
    const id = docId
    if (!id) { toast.error('اختر الوثيقة المطلوب التصرّف بها'); return }
    if (!reason.trim()) { toast.error('السبب مطلوب'); return }
    if (isDestroy && isOther && !customMethod.trim()) { toast.error('حدّد طريقة الإتلاف'); return }
    // The catalogue is a list of labels, not enum members, so the chosen label always travels in
    // customMethod — the same contract the الإتلاف screen uses. It is what the certificate prints.
    const label = isOther ? customMethod.trim() : chosenMethod
    setBusy(true)
    try {
      await disposition.create({
        documentId: id,
        requestedAction: action,
        reason: reason.trim(),
        method: isDestroy && isOther ? OTHER : 0,
        customMethod: isDestroy ? label : null,
      })
      toast.success('تم إنشاء طلب التصرّف — بانتظار موافقة مدير القسم')
      onDone?.(); onClose()
    } catch (e) {
      toast.error((e as AxiosError<{ error?: string }>).response?.data?.error ?? 'تعذّر إنشاء الطلب')
    } finally { setBusy(false) }
  }

  return (
    <div className="preview-overlay" onClick={() => !busy && onClose()}>
      <motion.div className="preview-modal" onClick={(e) => e.stopPropagation()}
        style={{ width: 'min(540px, 94vw)', height: 'auto', maxHeight: '92vh' }}
        initial={{ opacity: 0, scale: 0.97 }} animate={{ opacity: 1, scale: 1 }}>
        <header className="preview-head">
          <span className="preview-title">طلب تصرّف بالوثيقة{documentNumber ? ` · ${documentNumber}` : ''}</span>
          <button className="btn btn-ghost btn-sm" onClick={onClose}><X size={14} aria-hidden /> إغلاق</button>
        </header>

        <div className="form-grid" style={{ padding: '1rem', color: 'var(--ink-text, #211d17)' }}>
          {!documentId && (
            <div className="field--wide rd-picker">
              {picked ? (
                <div className="rd-picked">
                  <div>
                    <span className="mono">{picked.documentNumber}</span>
                    <strong className="rd-picked__title">{picked.title}</strong>
                    <span className="muted">{picked.documentTypeName}
                      {picked.boxCode ? ` · صندوق ${picked.boxCode}` : ''}
                      {picked.expiryDate ? ` · ينتهي حفظها ${picked.expiryDate}` : ''}</span>
                  </div>
                  <button type="button" className="btn btn-ghost btn-sm" onClick={() => setPicked(null)}>تغيير</button>
                </div>
              ) : (
                <>
                  <label className="rd-search">
                    <Search size={14} aria-hidden />
                    <input value={search} onChange={(e) => setSearch(e.target.value)}
                      placeholder="ابحث في وثائق قسمك بالرقم أو العنوان أو الكلمات المفتاحية…" />
                  </label>
                  <ul className="rd-list">
                    {choices === null && <li className="muted rd-list__note">جارٍ التحميل…</li>}
                    {choices?.length === 0 && <li className="muted rd-list__note">لا توجد وثائق مطابقة.</li>}
                    {choices?.map((d) => (
                      <li key={d.id}>
                        <button type="button" className="rd-row" onClick={() => setPicked(d)}>
                          <span className="mono rd-row__num">{d.documentNumber}</span>
                          <span className="rd-row__title">{d.title}</span>
                          <span className="muted rd-row__meta">
                            {d.documentTypeName}{d.boxCode ? ` · ${d.boxCode}` : ''}
                          </span>
                        </button>
                      </li>
                    ))}
                  </ul>
                </>
              )}
            </div>
          )}
          <LabeledSelect label="نوع الإجراء" value={action}
            onChange={(e) => setAction(Number(e.target.value))}>
            <option value={DISPOSITION_ACTION.Destroy}>إتلاف</option>
            <option value={DISPOSITION_ACTION.Renew}>تجديد مدة الحفظ</option>
          </LabeledSelect>

          {isDestroy && (
            <LabeledSelect label="طريقة الإتلاف" value={chosenMethod}
              onChange={(e) => setMethodLabel(e.target.value)}>
              {methodOptions.map((label) => <option key={label} value={label}>{label}</option>)}
              <option value={OTHER_LABEL}>{OTHER_LABEL}</option>
            </LabeledSelect>
          )}
          {isDestroy && isOther && (
            <LabeledInput label="حدّد الطريقة" required value={customMethod}
              onChange={(e) => setCustomMethod(e.target.value)} />
          )}

          <LabeledTextarea label="السبب" required rows={3} value={reason}
            onChange={(e) => setReason(e.target.value)} containerClassName="field--wide" />

          <p className="muted field--wide" style={{ fontSize: '.78rem' }}>
            يُرفع الطلب إلى مدير قسمك للموافقة، ثم إلى مدير قسم الأرشيف للمصادقة النهائية.
            طلبات موظفي الأرشيف تذهب إلى مدير الأرشيف مباشرة. الوثائق تحت حجز قانوني مستثناة.
          </p>
        </div>

        <div className="form-actions" style={{ padding: '0 1rem 1rem' }}>
          <button className="btn btn-seal" disabled={busy} onClick={submit}>{busy ? '…' : 'إنشاء الطلب'}</button>
          <button className="btn btn-ghost" disabled={busy} onClick={onClose}>إلغاء</button>
        </div>
      </motion.div>
    </div>
  )
}
