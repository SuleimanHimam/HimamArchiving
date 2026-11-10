import { useEffect, useMemo, useState } from 'react'
import { Save } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Dialog, DialogContent } from './ui/dialog'
import SearchSelect, { type SSOption } from './SearchSelect'
import DatePicker from './DatePicker'
import { useToast } from './toast'
import { diwan, STATUS_NAMES, type DiwanListItem } from '../lib/diwan'

/** حالة البريد — move a register entry to another status, with an optional note. */
export default function ChangeMailStatusDialog({
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

  const [status, setStatus] = useState<number | null>(null)
  const [statusLabel, setStatusLabel] = useState('')
  const [date, setDate] = useState('')
  const [note, setNote] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const options: SSOption[] = useMemo(
    () => STATUS_NAMES.map((k, v) => ({ id: v, label: t(`corr.status.${k}`, { defaultValue: k }) })),
    [t],
  )

  // Seed from the row so the dialog opens showing where the letter stands today.
  useEffect(() => {
    if (!item) return
    const current = STATUS_NAMES.indexOf(item.status as (typeof STATUS_NAMES)[number])
    setStatus(current === -1 ? null : current)
    setStatusLabel(current === -1 ? '' : t(`corr.status.${STATUS_NAMES[current]}`, { defaultValue: item.status }))
    setDate(new Date().toISOString().slice(0, 10))
    setNote('')
    setError('')
  }, [item?.id, open, t])

  const unchanged =
    item != null && status != null && STATUS_NAMES[status] === item.status

  async function save() {
    if (!item || status == null) return
    setError(''); setBusy(true)
    try {
      await diwan.changeStatus(item.id, {
        status,
        note: note.trim() || null,
        effectiveDate: date ? new Date(date).toISOString() : null,
      })
      toast.success(t('common.saved'))
      onSaved?.()
      onOpenChange(false)
    } catch (e) {
      const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error
      setError(msg ?? t('corr.errors.saveFailed'))
    } finally { setBusy(false) }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="cms">
        <header className="cms__head">
          <h2 className="cms__title">{ar ? 'حالة البريد' : 'Mail status'}</h2>
          <span className="cms__ref">
            {item ? `${item.registerNumber} — ${item.subject || (ar ? '(بلا موضوع)' : '(no subject)')}` : ''}
          </span>
        </header>

        <div className="cms__body">
          <div className="cms__grid">
            <label className="cms__field">
              <span>{ar ? 'حالة البريد' : 'Status'}</span>
              <SearchSelect
                options={options}
                value={statusLabel}
                onPick={(o) => { setStatus(o.id); setStatusLabel(o.label) }}
                placeholder={ar ? 'ابحث عن حالة…' : 'Search a status…'}
                ariaLabel={ar ? 'حالة البريد' : 'Status'}
                linked={status != null}
                showAllOnOpen
              />
            </label>
            <label className="cms__field">
              <span>{ar ? 'التاريخ' : 'Date'}</span>
              <DatePicker value={date} onChange={setDate} ariaLabel={ar ? 'التاريخ' : 'Date'} />
            </label>
          </div>

          <label className="cms__field">
            <span>{ar ? 'الجهة' : 'Party'}</span>
            <input value={item?.externalOrgName ?? '—'} readOnly className="cms__ro" />
          </label>

          <label className="cms__field">
            <span>{ar ? 'ملاحظة' : 'Note'}</span>
            <input value={note} onChange={(e) => setNote(e.target.value)}
              placeholder={ar ? 'سبب تغيير الحالة (اختياري)' : 'Reason for the change (optional)'} />
          </label>

          {unchanged && (
            <p className="cms__hint">{ar ? 'الحالة الحالية — اختر حالة مختلفة للحفظ' : 'Already the current status — pick a different one'}</p>
          )}
          {error && <p className="login__error">{error}</p>}
        </div>

        <footer className="cms__foot">
          <button type="button" className="btn btn-ghost" onClick={() => onOpenChange(false)}>
            {t('common.cancel')}
          </button>
          <button type="button" className="btn btn-primary" disabled={busy || status == null || unchanged} onClick={save}>
            <Save size={15} aria-hidden /> {busy ? t('common.saving') : t('common.save')}
          </button>
        </footer>
      </DialogContent>
    </Dialog>
  )
}
