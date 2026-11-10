import { useEffect, useState } from 'react'
import { Users, Trash2, Eye, Pencil, Globe, AlertTriangle } from 'lucide-react'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from './ui/dialog'
import { LabeledSelect } from './ui/labeled-input'
import { auth } from '../lib/auth'
import { documents } from '../lib/documents'
import type { ItemShare } from '../lib/userFeatures'
import { useToast } from './toast'
import './shareItemDialog.css'

/* Who else may open this folder, this file, or this document.
 *
 * One dialog for all three, because the grant is the same shape either way — a person and a
 * level — and separate dialogs that drift apart would let the same word mean three things. The
 * caller supplies the endpoints; everything else here is identical.
 */

const KIND_LABEL = { folder: 'المجلد', file: 'الملف', document: 'الوثيقة' } as const

export type ShareApi = {
  list: (id: number) => Promise<ItemShare[]>
  share: (id: number, userId: number, canEdit: boolean) => Promise<unknown>
  unshare: (id: number, userId: number) => Promise<unknown>
  /** Grant to every active account at once, and take it all back. */
  shareEveryone: (id: number, canEdit: boolean) => Promise<unknown>
  unshareEveryone: (id: number) => Promise<unknown>
}

export type ShareKind = 'folder' | 'file' | 'document'

export default function ShareItemDialog({
  id, name, kind, api, onClose,
}: {
  id: number
  name: string
  kind: ShareKind
  api: ShareApi
  onClose: () => void
}) {
  const toast = useToast()
  const [shares, setShares] = useState<ItemShare[]>([])
  const [people, setPeople] = useState<{ id: number; fullName: string }[]>([])
  const [userId, setUserId] = useState('')
  const [canEdit, setCanEdit] = useState(false)
  const [busy, setBusy] = useState(false)
  /* Sharing with the whole institution is one click away from sharing with one person, and on a
     document it also reaches past confidentiality. It asks twice. */
  const [confirmAll, setConfirmAll] = useState<'share' | 'clear' | null>(null)

  useEffect(() => {
    let alive = true
    Promise.all([api.list(id), documents.users()])
      .then(([s, u]) => { if (alive) { setShares(s); setPeople(u) } })
      .catch(() => { if (alive) toast.error('تعذّر تحميل المشاركات') })
    return () => { alive = false }
  // The dialog is mounted per item, so the item never changes under it.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id])

  // Someone already granted is offered again only through their existing row, where the level
  // can be changed — offering them twice in the picker suggests a second, separate grant.
  const candidates = people.filter((p) => !shares.some((s) => s.userId === p.id))
  // The server grants to every active account except the caller's, so the warning counts the
  // same way — a number that turns out to be one too many is a warning nobody trusts twice.
  const me = auth.getUser()?.id
  const everyoneCount = people.filter((p) => p.id !== me).length

  const reload = async () => setShares(await api.list(id))

  async function add() {
    if (!userId) return
    setBusy(true)
    try {
      await api.share(id, Number(userId), canEdit)
      await reload()
      setUserId('')
      toast.success('تمت المشاركة')
    } catch { toast.error('تعذّرت المشاركة') } finally { setBusy(false) }
  }

  async function shareAll() {
    setBusy(true)
    try {
      await api.shareEveryone(id, canEdit)
      await reload()
      setConfirmAll(null)
      toast.success('شُورك مع الجميع')
    } catch { toast.error('تعذّرت المشاركة مع الجميع') } finally { setBusy(false) }
  }

  async function clearAll() {
    setBusy(true)
    try {
      await api.unshareEveryone(id)
      await reload()
      setConfirmAll(null)
      toast.success('أُلغيت كل المشاركات')
    } catch { toast.error('تعذّر إلغاء المشاركات') } finally { setBusy(false) }
  }

  async function setLevel(share: ItemShare, next: boolean) {
    try { await api.share(id, share.userId, next); await reload() }
    catch { toast.error('تعذّر تغيير الصلاحية') }
  }

  async function remove(share: ItemShare) {
    try { await api.unshare(id, share.userId); await reload(); toast.success('أُلغيت المشاركة') }
    catch { toast.error('تعذّر إلغاء المشاركة') }
  }

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose() }}>
      <DialogContent className="shr" dir="rtl">
        <DialogHeader className="shr__head">
          <DialogTitle className="shr__title">
            <Users size={16} aria-hidden /> مشاركة {KIND_LABEL[kind]}
          </DialogTitle>
          <p className="shr__name">{name}</p>
        </DialogHeader>

        {/* Two rows, not three controls crammed into one: at this width the picker had no room
            for a name, and a native <select> of every account in the institution opened as a
            list taller than the screen. LabeledSelect turns into a searchable, portalled one. */}
        <div className="shr__add">
          <LabeledSelect label="مشاركة مع" value={userId} onChange={(e) => setUserId(e.target.value)}>
            <option value="">— اختر مستخدمًا —</option>
            {candidates.map((p) => <option key={p.id} value={p.id}>{p.fullName}</option>)}
          </LabeledSelect>

          <div className="shr__addrow">
            <LabeledSelect label="الصلاحية" containerClassName="shr__level"
              value={canEdit ? '1' : '0'} onChange={(e) => setCanEdit(e.target.value === '1')}>
              <option value="0">عرض فقط</option>
              <option value="1">عرض وتعديل</option>
            </LabeledSelect>
            <button type="button" className="btn btn-primary" disabled={!userId || busy} onClick={add}>
              مشاركة
            </button>
          </div>

          <div className="shr__bulk">
            {confirmAll === 'share' ? (
              <>
                <span className="shr__warn">
                  <AlertTriangle size={13} aria-hidden />
                  ستُمنح لـ{everyoneCount} مستخدمًا بصلاحية «{canEdit ? 'عرض وتعديل' : 'عرض فقط'}».
                </span>
                <button type="button" className="btn btn-danger btn-sm" disabled={busy} onClick={shareAll}>
                  تأكيد
                </button>
                <button type="button" className="btn btn-ghost btn-sm" onClick={() => setConfirmAll(null)}>
                  تراجع
                </button>
              </>
            ) : (
              <button type="button" className="btn btn-ghost btn-sm" disabled={busy}
                onClick={() => setConfirmAll('share')}>
                <Globe size={14} aria-hidden /> مشاركة مع الجميع
              </button>
            )}
          </div>
        </div>

        <div className="shr__listhead">
          <span>مُشارَك مع {shares.length}</span>
          {shares.length > 0 && (confirmAll === 'clear' ? (
            <span className="shr__clear">
              <button type="button" className="btn btn-danger btn-sm" disabled={busy} onClick={clearAll}>
                تأكيد الإلغاء
              </button>
              <button type="button" className="btn btn-ghost btn-sm" onClick={() => setConfirmAll(null)}>
                تراجع
              </button>
            </span>
          ) : (
            <button type="button" className="btn btn-ghost btn-sm" onClick={() => setConfirmAll('clear')}>
              إلغاء الكل
            </button>
          ))}
        </div>

        {/* Scrolls: sharing with everyone puts as many rows here as the institution has people. */}
        <ul className="shr__list">
          {shares.length === 0 && <li className="shr__empty">لم تتم المشاركة مع أحد بعد.</li>}
          {shares.map((s) => (
            <li key={s.userId} className="shr__row">
              <span className="shr__who">{s.userName}</span>
              <select value={s.canEdit ? '1' : '0'} onChange={(e) => setLevel(s, e.target.value === '1')}
                aria-label={`صلاحية ${s.userName}`}>
                <option value="0">عرض فقط</option>
                <option value="1">عرض وتعديل</option>
              </select>
              <button type="button" className="btn btn-ghost btn-sm shr__drop" onClick={() => remove(s)}
                aria-label={`إلغاء مشاركة ${s.userName}`}>
                <Trash2 size={14} aria-hidden />
              </button>
            </li>
          ))}
        </ul>

        <p className="shr__note">
          {kind === 'folder' ? (
            <>
              <Eye size={13} aria-hidden /> المشاركة تشمل ما داخل المجلد من ملفات ومجلدات فرعية.
              الوثائق المحفوظة فيه لا تُشارَك بهذه الطريقة — لكل وثيقة صلاحيتها وسرّيتها.
            </>
          ) : kind === 'document' ? (
            /* Not a footnote: DocumentService checks the share before it checks clearance, so a
               grant here reaches past both the department scope and the confidentiality level.
               Whoever clicks this is making that decision and should be told so. */
            <>
              <Eye size={13} aria-hidden /> مشاركة الوثيقة تفتحها لهذا الشخص حتى لو كان من قسم آخر،
              وحتى لو كانت سريّتها فوق تصنيفه — فالمنح صريح ويسبق القاعدتين.
            </>
          ) : (
            <><Pencil size={13} aria-hidden /> «عرض وتعديل» يسمح بإعادة التسمية والنقل. الحذف يبقى لك وحدك.</>
          )}
        </p>
      </DialogContent>
    </Dialog>
  )
}
