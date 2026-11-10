import { useCallback, useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { AxiosError } from 'axios'
import { Inbox, PackageCheck, Undo2, X } from 'lucide-react'
import { documents, type ArchiveIntakeItem } from '../../lib/documents'
import { clearanceLabel, clearanceClass } from '../../lib/shared'
import LocationPicker from '../../components/LocationPicker'
import { useToast } from '../../components/toast'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../components/ui/card'
import { LabeledTextarea } from '../../components/ui/labeled-input'
import '../../styles/forms.css'
// .preview-overlay / .preview-modal — the dialog shell the document pages use for this picker.
import '../documents/documents.css'

/**
 * The archive department's inbox.
 *
 * A department that registers a paper record sends it here («إرسال للأرشيف»); it waits in this
 * queue until an archivist either gives it a box — which is what makes it مؤرشف — or hands it back
 * with a reason. Filing is deliberately the archive's alone: the originating department can say a
 * record exists, but only the people who hold the shelves can say where it lives.
 */

const errOf = (err: unknown, fallback: string) =>
  (err as AxiosError<{ error?: string }>).response?.data?.error ?? fallback

/** «منذ ٣ أيام» — the queue is worked oldest-first, so age is the column that decides order. */
function waitedFor(iso: string | null) {
  if (!iso) return '—'
  const days = Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000)
  if (days <= 0) return 'اليوم'
  if (days === 1) return 'منذ يوم'
  if (days === 2) return 'منذ يومين'
  if (days <= 10) return `منذ ${days} أيام`
  return `منذ ${days} يومًا`
}

export default function ArchiveIntakeQueue() {
  const navigate = useNavigate()
  const toast = useToast()

  const [items, setItems] = useState<ArchiveIntakeItem[] | null>(null)
  const [busy, setBusy] = useState(false)
  const [filing, setFiling] = useState<ArchiveIntakeItem | null>(null)
  const [boxId, setBoxId] = useState<number | null>(null)
  const [note, setNote] = useState('')
  const [returning, setReturning] = useState<ArchiveIntakeItem | null>(null)
  const [reason, setReason] = useState('')

  const load = useCallback(async () => {
    try { setItems(await documents.archiveQueue()) }
    catch { toast.error('تعذّر تحميل وارد الأرشفة') }
  }, [toast])

  useEffect(() => { void load() }, [load])

  function openFiling(it: ArchiveIntakeItem) { setFiling(it); setBoxId(null); setNote('') }
  function openReturn(it: ArchiveIntakeItem) { setReturning(it); setReason('') }

  async function submitFiling() {
    if (!filing || boxId == null) return
    setBusy(true)
    try {
      await documents.fileToArchive(filing.id, boxId, note.trim() || null)
      setFiling(null)
      await load()
      toast.success('حُفظت الوثيقة في الصندوق')
    } catch (err) { toast.error(errOf(err, 'تعذّر حفظ الوثيقة')) }
    finally { setBusy(false) }
  }

  async function submitReturn() {
    if (!returning || !reason.trim()) return
    setBusy(true)
    try {
      await documents.returnFromArchive(returning.id, reason.trim())
      setReturning(null)
      await load()
      toast.success('أُعيدت الوثيقة إلى مُرسِلها')
    } catch (err) { toast.error(errOf(err, 'تعذّر إرجاع الوثيقة')) }
    finally { setBusy(false) }
  }

  const count = useMemo(() => items?.length ?? 0, [items])

  return (
    <Card>
      <CardHeader>
        <CardTitle>وارد الأرشفة ({count})</CardTitle>
        <CardDescription>
          وثائق أرسلتها الأقسام لحفظها فعليًا. اختر لها صندوقًا لإتمام الأرشفة، أو أعِدها
          إلى مُرسِلها موضّحًا السبب. الأقدم أولًا.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <div className="overflow-x-auto">
          <table className="reg-table">
            <thead><tr>
              <th>رقم الوثيقة</th>
              <th>العنوان</th>
              <th>النوع</th>
              <th>القسم المُرسِل</th>
              <th>المُرسِل</th>
              <th>التصنيف</th>
              <th>المرفقات</th>
              <th>الانتظار</th>
              <th>إجراءات</th>
            </tr></thead>
            <tbody>
              {items === null && <tr><td colSpan={9} className="reg-empty">جارٍ التحميل…</td></tr>}
              {items !== null && items.length === 0 && (
                <tr><td colSpan={9} className="reg-empty">
                  <Inbox size={16} aria-hidden style={{ marginInlineEnd: 6, verticalAlign: 'middle' }} />
                  لا توجد وثائق بانتظار الأرشفة
                </td></tr>
              )}
              {(items ?? []).map((it) => (
                <tr key={it.id} className="reg-row">
                  <td className="mono">
                    <button type="button" className="btn btn-ghost btn-sm"
                      onClick={() => navigate(`/app/documents/${it.id}`)}>{it.documentNumber}</button>
                  </td>
                  <td title={it.title}>
                    {it.title}
                    {/* A second attempt: the archivist should see what was wrong last time before
                        deciding whether it was actually fixed. */}
                    {it.previousReturnReason && (
                      <div className="muted" style={{ fontSize: '.75rem' }}>
                        أُعيدت سابقًا: {it.previousReturnReason}
                      </div>
                    )}
                  </td>
                  <td>{it.documentTypeName}</td>
                  <td>{it.owningOrgUnitName}</td>
                  <td>{it.sentByName ?? '—'}</td>
                  <td><span className={`badge ${clearanceClass(it.confidentiality)}`}>{clearanceLabel(it.confidentiality)}</span></td>
                  <td>{it.attachmentCount}</td>
                  <td>{waitedFor(it.sentAt)}</td>
                  <td className="user-actions">
                    <button type="button" className="btn btn-primary btn-sm" disabled={busy}
                      onClick={() => openFiling(it)}>
                      <PackageCheck size={14} aria-hidden /> حفظ في صندوق
                    </button>{' '}
                    <button type="button" className="btn btn-ghost btn-sm" disabled={busy}
                      onClick={() => openReturn(it)}>
                      <Undo2 size={14} aria-hidden /> إرجاع
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </CardContent>

      {/* Filing — the same Building → Room → Cabinet → Shelf → Box picker used when registering a
          document with a place, so an archivist learns one control, not two.

          The overlay is the app's own, not the Radix Dialog: LocationPicker portals its option list
          to <body>, and inside a Radix dialog that list drew above the panel but the panel still
          took the click, so no box could actually be chosen. This is the pattern «تغيير مكان الحفظ
          الفعلي» on the document page already uses with the same picker. */}
      {filing && (
        <div className="preview-overlay" onClick={() => !busy && setFiling(null)}>
          <div className="preview-modal" onClick={(e) => e.stopPropagation()}
            role="dialog" aria-modal="true" aria-label="تحديد مكان الحفظ"
            style={{ width: 'min(560px, 92vw)', height: 'auto', maxHeight: '90vh' }}>
            <header className="preview-head">
              <span className="preview-title">تحديد مكان الحفظ · {filing.documentNumber}</span>
              <button type="button" className="btn btn-ghost btn-sm" onClick={() => setFiling(null)}>
                <X size={15} aria-hidden /> إغلاق
              </button>
            </header>
            <div className="form-grid" style={{ padding: '1rem', overflow: 'auto', color: 'var(--ink-text, #211d17)' }}>
              <p className="muted field--wide">«{filing.title}» — {filing.owningOrgUnitName}</p>
              <LocationPicker value={boxId} onChange={setBoxId} />
              <LabeledTextarea label="ملاحظة (اختياري)" rows={2} value={note}
                onChange={(e) => setNote(e.target.value)} />
            </div>
            <div className="form-actions" style={{ padding: '0 1rem 1rem' }}>
              <button type="button" className="btn btn-primary" disabled={busy || boxId == null}
                onClick={submitFiling}>{busy ? '…' : 'إتمام الأرشفة'}</button>
              <button type="button" className="btn btn-ghost" onClick={() => setFiling(null)}>إلغاء</button>
            </div>
          </div>
        </div>
      )}

      {/* Returning — the reason is required, and it is what the submitter reads in their notification. */}
      {returning && (
        <div className="preview-overlay" onClick={() => !busy && setReturning(null)}>
          <div className="preview-modal" onClick={(e) => e.stopPropagation()}
            role="dialog" aria-modal="true" aria-label="إرجاع الوثيقة إلى مُرسِلها"
            style={{ width: 'min(520px, 92vw)', height: 'auto', maxHeight: '90vh' }}>
            <header className="preview-head">
              <span className="preview-title">إرجاع الوثيقة إلى مُرسِلها · {returning.documentNumber}</span>
              <button type="button" className="btn btn-ghost btn-sm" onClick={() => setReturning(null)}>
                <X size={15} aria-hidden /> إغلاق
              </button>
            </header>
            <div className="form-grid" style={{ padding: '1rem', overflow: 'auto', color: 'var(--ink-text, #211d17)' }}>
              <p className="muted field--wide">«{returning.title}» — {returning.owningOrgUnitName}</p>
              <LabeledTextarea label="سبب الإرجاع" required rows={3} value={reason}
                onChange={(e) => setReason(e.target.value)} />
            </div>
            <div className="form-actions" style={{ padding: '0 1rem 1rem' }}>
              <button type="button" className="btn btn-primary" disabled={busy || !reason.trim()}
                onClick={submitReturn}>{busy ? '…' : 'إرجاع'}</button>
              <button type="button" className="btn btn-ghost" onClick={() => setReturning(null)}>إلغاء</button>
            </div>
          </div>
        </div>
      )}
    </Card>
  )
}
