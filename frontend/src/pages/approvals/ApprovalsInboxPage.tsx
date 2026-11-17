import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Undo2, X } from 'lucide-react'
import { approvalsApi, type ApprovalInboxItem } from '../../lib/approvals'
import { stageAllows, workflow, WF_ACTION, type WfActionName } from '../../lib/workflow'
import { apiError } from '../../lib/hr'
import { day } from '../../lib/hrHooks'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { PageHead } from '../hr/hrShared'
import './approvals.css'

type Decision = Extract<WfActionName, 'Approve' | 'RequestChanges' | 'Reject'>

/** صندوق الموافقات (standards §5.1): every open approval task assigned to the caller, from any module.
 *  The server re-checks task ownership and requires a reason for reject and return. */
export default function ApprovalsInboxPage() {
  const { t } = useTranslation()
  const toast = useToast()
  const { confirm, prompt } = useConfirmDialog()
  const qc = useQueryClient()
  const [busy, setBusy] = useState<number | null>(null)
  const { data, isLoading, isError } = useQuery({ queryKey: ['approvals', 'inbox'], queryFn: approvalsApi.inbox })

  async function decide(item: ApprovalInboxItem, decision: Decision) {
    let note: string | null = null
    if (decision === 'Approve') {
      if (!(await confirm({ title: t('approvals.Approve'), message: t('approvals.approveConfirm', { number: item.number }) }))) return
    } else {
      note = await prompt({ title: `${t(`approvals.${decision}`)}: ${item.number}`, label: t('approvals.reason') })
      if (note === null) return
    }
    setBusy(item.taskId)
    try {
      await workflow.act(item.taskId, WF_ACTION[decision], note?.trim() || null)
      toast.success(t(`approvals.done.${decision}`))
      await qc.invalidateQueries({ queryKey: ['approvals'] })
      await qc.invalidateQueries({ queryKey: ['hr'] })
    } catch (err) { toast.error(apiError(err, t('approvals.failed'))) }
    finally { setBusy(null) }
  }

  const buttons: { decision: Decision; Icon: typeof Check; cls: string }[] = [
    { decision: 'Approve', Icon: Check, cls: 'btn-primary' },
    { decision: 'RequestChanges', Icon: Undo2, cls: 'btn-ghost' },
    { decision: 'Reject', Icon: X, cls: 'btn-ghost' },
  ]

  return (
    <div>
      <PageHead title={t('approvals.title')} />
      <section className="doc-card">
        <p className="muted">{t('approvals.hint')}</p>
        <div className="table-scroll">
          <table className="reg-table">
            <thead>
              <tr>
                <th>{t('approvals.number')}</th><th>{t('approvals.subject')}</th><th>{t('approvals.requester')}</th>
                <th>{t('approvals.stage')}</th><th>{t('approvals.submittedAt')}</th><th>{t('approvals.dueAt')}</th>
                <th aria-label={t('hr.common.actions')} />
              </tr>
            </thead>
            <tbody>
              {isLoading && <tr><td colSpan={7} className="reg-empty">{t('hr.common.loading')}</td></tr>}
              {isError && <tr><td colSpan={7} className="reg-empty">{t('hr.common.loadFailed')}</td></tr>}
              {data && data.length === 0 && <tr><td colSpan={7} className="reg-empty">{t('approvals.empty')}</td></tr>}
              {data?.map((i) => (
                <tr key={i.taskId} className={`reg-row${i.isOverdue ? ' is-overdue' : ''}`}>
                  <td dir="ltr">{i.number}</td>
                  <td>{i.title}</td>
                  <td>{i.requesterName ?? '—'}</td>
                  <td>{i.stageName}</td>
                  <td>{day(i.submittedAt)}</td>
                  <td>
                    {day(i.dueAt)}
                    {i.isOverdue && <span className="status-pill s-overdue" style={{ marginInlineStart: '.4rem' }}>{t('approvals.overdue')}</span>}
                  </td>
                  <td className="row-actions">
                    {buttons.filter((b) => stageAllows(i.allowedActions, b.decision)).map(({ decision, Icon, cls }) => (
                      <button key={decision} className={`btn btn-sm ${cls}`} disabled={busy === i.taskId}
                        onClick={() => decide(i, decision)}>
                        <Icon size={14} aria-hidden /> {t(`approvals.${decision}`)}
                      </button>
                    ))}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  )
}
