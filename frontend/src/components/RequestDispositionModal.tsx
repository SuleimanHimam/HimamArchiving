import { useState } from 'react'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { disposition, DESTRUCTION_METHODS, DISPOSITION_ACTION } from '../lib/disposition'
import { useToast } from './toast'

const OTHER = 7

/** Form to raise a two-step disposition request (Destroy or Renew) for a document.
 * If `documentId` is omitted, the user enters one (used from the disposition queues). */
export default function RequestDispositionModal({
  documentId, documentNumber, onClose, onDone,
}: { documentId?: number; documentNumber?: string; onClose: () => void; onDone?: () => void }) {
  const toast = useToast()
  const [docId, setDocId] = useState(documentId ? String(documentId) : '')
  const [action, setAction] = useState<number>(DISPOSITION_ACTION.Destroy)
  const [method, setMethod] = useState(0)
  const [customMethod, setCustomMethod] = useState('')
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)

  const isDestroy = action === DISPOSITION_ACTION.Destroy

  async function submit() {
    const id = Number(docId)
    if (!id) { toast.error('معرّف الوثيقة مطلوب'); return }
    if (!reason.trim()) { toast.error('السبب مطلوب'); return }
    if (isDestroy && method === OTHER && !customMethod.trim()) { toast.error('حدّد طريقة الإتلاف'); return }
    setBusy(true)
    try {
      await disposition.create({
        documentId: id,
        requestedAction: action,
        reason: reason.trim(),
        method: isDestroy ? method : 0,
        customMethod: isDestroy && method === OTHER ? customMethod.trim() : null,
      })
      toast.success('تم إنشاء طلب التصرّف (بانتظار التحقق)')
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
          <button className="btn btn-ghost btn-sm" onClick={onClose}>✕ إغلاق</button>
        </header>

        <div className="form-grid" style={{ padding: '1rem', color: 'var(--ink-text, #211d17)' }}>
          {!documentId && (
            <label className="field"><span>معرّف الوثيقة *</span>
              <input value={docId} dir="ltr" onChange={(e) => setDocId(e.target.value)} placeholder="مثال: 13" /></label>
          )}
          <label className="field"><span>نوع الإجراء</span>
            <select value={action} onChange={(e) => setAction(Number(e.target.value))}>
              <option value={DISPOSITION_ACTION.Destroy}>إتلاف</option>
              <option value={DISPOSITION_ACTION.Renew}>تجديد مدة الحفظ</option>
            </select></label>

          {isDestroy && (
            <label className="field"><span>طريقة الإتلاف</span>
              <select value={method} onChange={(e) => setMethod(Number(e.target.value))}>
                {DESTRUCTION_METHODS.map((m) => <option key={m.value} value={m.value}>{m.label}</option>)}
              </select></label>
          )}
          {isDestroy && method === OTHER && (
            <label className="field"><span>حدّد الطريقة *</span>
              <input value={customMethod} onChange={(e) => setCustomMethod(e.target.value)} placeholder="اكتب طريقة الإتلاف" /></label>
          )}

          <label className="field field--wide"><span>السبب *</span>
            <textarea rows={3} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="سبب طلب التصرّف بالوثيقة" /></label>

          <p className="muted field--wide" style={{ fontSize: '.78rem' }}>
            يمر الطلب بخطوتين: تحقّق من مسؤول السجلات ثم موافقة نهائية من الشؤون القانونية. الوثائق تحت حجز قانوني مستثناة.
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
