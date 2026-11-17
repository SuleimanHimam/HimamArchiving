import { useTranslation } from 'react-i18next'
import type { ActionStatus, EmployeeStatus } from '../../lib/hr'
import '../documents/documents.css'

const EMPLOYEE_PILL: Record<EmployeeStatus, string> = { Active: 's-active', Suspended: 's-onhold', Terminated: 's-archived' }
const ACTION_PILL: Record<ActionStatus, string> = {
  Draft: 's-new', Submitted: 's-inprogress', Executed: 's-closed', Rejected: 's-overdue', Returned: 's-onhold', Cancelled: 's-archived',
}

export function EmployeeStatusPill({ status }: { status: EmployeeStatus }) {
  const { t } = useTranslation()
  return <span className={`status-pill ${EMPLOYEE_PILL[status]}`}>{t(`hr.employeeStatus.${status}`)}</span>
}

export function ActionStatusPill({ status }: { status: ActionStatus }) {
  const { t } = useTranslation()
  return <span className={`status-pill ${ACTION_PILL[status]}`}>{t(`hr.actionStatus.${status}`)}</span>
}

export function PageHead({ title, children }: { title: string; children?: React.ReactNode }) {
  return (
    <header className="page__head">
      <div><h1>{title}</h1></div>
      {children && <div className="row-actions">{children}</div>}
    </header>
  )
}
