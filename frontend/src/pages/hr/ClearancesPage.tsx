import { Fragment, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Check } from 'lucide-react'
import { auth } from '../../lib/auth'
import { apiError, hrApi, type ClearanceDto, type ClearanceItemDto } from '../../lib/hr'
import { LabeledSelect } from '../../components/ui/labeled-input'
import { useExpandedRow, ExpandCell, ExpandHeadCell, ExpandedRow } from '../../components/ui/expandable-row'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { PageHead } from './hrShared'
import { day } from '../../lib/hrHooks'

/** براءة الذمة (operations sequence §8.4): one item per configured party; issued when all are cleared. */
export default function ClearancesPage() {
  const { t } = useTranslation()
  const toast = useToast()
  const { prompt } = useConfirmDialog()
  const qc = useQueryClient()
  const [status, setStatus] = useState('Open')
  const { toggle, isExpanded } = useExpandedRow<number>()
  const canClear = auth.hasPermission('HrClearance.Edit')
  const { data, isLoading, isError } = useQuery({
    queryKey: ['hr', 'clearances', status],
    queryFn: () => hrApi.clearances(status || undefined),
  })

  async function clear(c: ClearanceDto, item: ClearanceItemDto) {
    const note = await prompt({ title: `${t('hr.clearance.clear')}: ${item.partyName}`, label: t('hr.clearance.clearPrompt'), allowEmpty: true })
    if (note === null) return
    try {
      const updated = await hrApi.clearItem(c.id, item.id, note.trim() || null)
      toast.success(t(updated.status === 'Issued' ? 'hr.clearance.issuedNow' : 'hr.clearance.itemCleared'))
      await qc.invalidateQueries({ queryKey: ['hr', 'clearances'] })
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <div>
      <PageHead title={t('hr.clearance.title')} />
      <section className="doc-card">
        <div className="form-grid">
          <LabeledSelect label={t('hr.common.status')} value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="">{t('hr.common.all')}</option>
            <option value="Open">{t('hr.clearance.open')}</option>
            <option value="Issued">{t('hr.clearance.issued')}</option>
          </LabeledSelect>
        </div>
        <div className="table-scroll">
          <table className="reg-table">
            <thead>
              <tr>
                <ExpandHeadCell />
                <th>{t('hr.clearance.number')}</th><th>{t('hr.clearance.employee')}</th>
                <th>{t('hr.common.status')}</th><th>{t('hr.clearance.issuedAt')}</th>
              </tr>
            </thead>
            <tbody>
              {isLoading && <tr><td colSpan={5} className="reg-empty">{t('hr.common.loading')}</td></tr>}
              {isError && <tr><td colSpan={5} className="reg-empty">{t('hr.common.loadFailed')}</td></tr>}
              {data && data.length === 0 && <tr><td colSpan={5} className="reg-empty">{t('hr.common.empty')}</td></tr>}
              {data?.map((c) => {
                const done = c.items.filter((i) => i.isCleared).length
                return (
                  <Fragment key={c.id}>
                    <tr className="reg-row">
                      <ExpandCell expanded={isExpanded(c.id)} onToggle={() => toggle(c.id)} />
                      <td dir="ltr">{c.number}</td>
                      <td>{c.employeeName ?? '—'}</td>
                      <td>
                        <span className={`status-pill ${c.status === 'Issued' ? 's-closed' : 's-pending'}`}>
                          {c.status === 'Issued' ? t('hr.clearance.issued') : t('hr.clearance.progress', { done, total: c.items.length })}
                        </span>
                      </td>
                      <td>{day(c.issuedAt)}</td>
                    </tr>
                    <ExpandedRow colSpan={5} expanded={isExpanded(c.id)}>
                      <table className="reg-table">
                        <thead><tr><th>{t('hr.clearance.party')}</th><th>{t('hr.common.status')}</th><th>{t('hr.common.notes')}</th><th aria-label={t('hr.common.actions')} /></tr></thead>
                        <tbody>
                          {c.items.map((i) => (
                            <tr key={i.id}>
                              <td>{i.partyName}</td>
                              <td>
                                <span className={`status-pill ${i.isCleared ? 's-closed' : 's-pending'}`}>
                                  {i.isCleared ? `${t('hr.clearance.cleared')} · ${day(i.clearedAt)}` : t('hr.clearance.pending')}
                                </span>
                              </td>
                              <td>{i.note ?? '—'}</td>
                              <td className="row-actions">
                                {canClear && !i.isCleared && c.status === 'Open' && (
                                  <button className="btn btn-ghost btn-sm" onClick={() => clear(c, i)}>
                                    <Check size={14} aria-hidden /> {t('hr.clearance.clear')}
                                  </button>
                                )}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </ExpandedRow>
                  </Fragment>
                )
              })}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  )
}
