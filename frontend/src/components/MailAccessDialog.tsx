import { useMemo, useState } from 'react'
import { Save, Search, ShieldCheck } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Dialog, DialogContent } from './ui/dialog'
import { useToast } from './toast'
import { diwan, type DiwanListItem } from '../lib/diwan'

/**
 * من يطّلع على البريد — who may open one external letter.
 *
 * Routing addresses a letter to a department, not to everyone in it, so arrival puts it in front
 * of the head; this is where the head says which of their staff handles it. Access that comes with
 * the letter — its registrar, the heads of the departments it reached — is listed but not offered
 * as a checkbox: it is not the head's to withhold, and hiding it would invite them to "grant"
 * someone access they already had.
 */
export default function MailAccessDialog({
  item, open, onOpenChange, onSaved,
}: {
  item: DiwanListItem | null
  open: boolean
  onOpenChange: (o: boolean) => void
  onSaved?: () => void
}) {
  const { t, i18n } = useTranslation()
  const ar = i18n.language.startsWith('ar')
  const toast = useToast()
  const qc = useQueryClient()

  const [note, setNote] = useState('')
  const [query, setQuery] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const { data: view, isLoading, isError } = useQuery({
    queryKey: ['diwan-access', item?.id],
    queryFn: () => diwan.access(item!.id),
    enabled: open && item != null,
  })

  /* The selection starts as whatever the letter already carries and is only *held* once the head
     touches it — derived rather than copied into state on load, so there is no moment where the
     boxes and the letter disagree. */
  const granted = useMemo(
    () => new Set((view?.entries ?? []).filter((e) => e.reason === 'Granted').map((e) => e.userId)),
    [view],
  )
  const [edited, setEdited] = useState<Set<number> | null>(null)
  const picked = edited ?? granted

  // Those who hold the letter by their seat — shown, never offered as a choice.
  const inherent = useMemo(() => view?.entries.filter((e) => e.reason !== 'Granted') ?? [], [view])
  const candidates = (view?.candidates ?? [])
    .filter((c) => !inherent.some((e) => e.userId === c.userId))
    .filter((c) => !query || c.fullName.includes(query) || (c.orgUnitName ?? '').includes(query))

  const toggle = (id: number) =>
    setEdited(() => {
      const next = new Set(picked)
      if (!next.delete(id)) next.add(id)
      return next
    })

  // Closing discards the draft: the dialog outlives the row it was opened from.
  function close(o: boolean) {
    if (!o) { setEdited(null); setNote(''); setQuery(''); setError('') }
    onOpenChange(o)
  }

  async function save() {
    if (!item) return
    setError(''); setBusy(true)
    try {
      await diwan.setAccess(item.id, [...picked], note.trim() || null)
      // Drop the cached panel: reopening the row must show the access as it now stands, not as it
      // stood when the dialog was last filled in.
      await qc.invalidateQueries({ queryKey: ['diwan-access', item.id] })
      toast.success(t('common.saved'))
      onSaved?.()
      close(false)
    } catch (e) {
      const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error
      setError(msg ?? t('corr.errors.saveFailed'))
    } finally { setBusy(false) }
  }

  const reasonLabel = (r: string) =>
    r === 'Registrar' ? (ar ? 'سجّل البريد' : 'Registered it')
      : r === 'Head' ? (ar ? 'مدير القسم' : 'Department head')
        : (ar ? 'مسموح له' : 'Granted')

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="cms">
        <header className="cms__head">
          <h2 className="cms__title">{ar ? 'من يطّلع على البريد' : 'Who may open this letter'}</h2>
          <span className="cms__ref">
            {item ? `${item.registerNumber} — ${item.subject || (ar ? '(بلا موضوع)' : '(no subject)')}` : ''}
          </span>
        </header>

        <div className="cms__body">
          {isLoading ? <p className="cms__hint">{t('common.loading')}</p>
            : isError ? <p className="login__error">{ar ? 'تعذّر تحميل قائمة الاطّلاع' : 'Could not load the access list'}</p> : (
            <>
              {inherent.length > 0 && (
                <div className="cms__field">
                  <span>{ar ? 'يطّلع عليه بحكم موقعه' : 'Access that comes with the letter'}</span>
                  <ul className="acc__inherent">
                    {inherent.map((e) => (
                      <li key={e.userId}>
                        <ShieldCheck size={13} aria-hidden />
                        <span className="acc__name">{e.fullName}</span>
                        <span className="acc__why">{reasonLabel(e.reason)}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              )}

              {view?.canManage ? (
                <>
                  <div className="cms__field">
                    <span>{ar ? 'اختر من يطّلع عليه من موظفي قسمك' : 'Choose who in your department may open it'}</span>
                    <div className="acc__search">
                      <Search size={14} aria-hidden />
                      <input
                        value={query}
                        onChange={(e) => setQuery(e.target.value)}
                        placeholder={ar ? 'ابحث بالاسم أو القسم…' : 'Search by name or department…'}
                        aria-label={ar ? 'بحث' : 'Search'}
                      />
                    </div>
                    <ul className="acc__list">
                      {candidates.length === 0 && (
                        <li className="acc__empty">{ar ? 'لا يوجد موظفون مطابقون' : 'No matching staff'}</li>
                      )}
                      {candidates.map((c) => (
                        <li key={c.userId}>
                          <label className="acc__row">
                            <input type="checkbox" checked={picked.has(c.userId)} onChange={() => toggle(c.userId)} />
                            <span className="acc__name">{c.fullName}</span>
                            {c.orgUnitName && <span className="acc__why">{c.orgUnitName}</span>}
                          </label>
                        </li>
                      ))}
                    </ul>
                  </div>

                  <label className="cms__field">
                    <span>{ar ? 'ملاحظة' : 'Note'}</span>
                    <input value={note} onChange={(e) => setNote(e.target.value)}
                      placeholder={ar ? 'سبب السماح (اختياري)' : 'Reason for the access (optional)'} />
                  </label>
                </>
              ) : (
                <p className="cms__hint">
                  {ar
                    ? 'تحديد من يطّلع على البريد الخارجي من صلاحية مدير القسم.'
                    : 'Only a department head decides who may open an external letter.'}
                </p>
              )}
            </>
          )}
          {error && <p className="login__error">{error}</p>}
        </div>

        <footer className="cms__foot">
          <button type="button" className="btn btn-ghost" onClick={() => close(false)}>
            {view?.canManage ? t('common.cancel') : t('common.close', { defaultValue: ar ? 'إغلاق' : 'Close' })}
          </button>
          {view?.canManage && (
            <button type="button" className="btn btn-primary" disabled={busy || isLoading} onClick={save}>
              <Save size={15} aria-hidden /> {busy ? t('common.saving') : t('common.save')}
            </button>
          )}
        </footer>
      </DialogContent>
    </Dialog>
  )
}
