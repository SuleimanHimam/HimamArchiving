import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { FolderOpen, UserPlus } from 'lucide-react'
import { auth } from '../../lib/auth'
import { apiError, hrApi, type EmployeeCategory, type SaveEmployee } from '../../lib/hr'
import { LabeledInput, LabeledSelect } from '../../components/ui/labeled-input'
import { useToast } from '../../components/toast'
import { EmployeeStatusPill, PageHead } from './hrShared'
import { day, useOrgUnits, useSites } from '../../lib/hrHooks'

const PAGE_SIZE = 25
const CATEGORIES: EmployeeCategory[] = ['Military', 'Civil', 'Contract']

/** قائمة الموظفين: the core register, scoped by the server to the caller's units. */
export default function EmployeesPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [adding, setAdding] = useState(false)

  const { data, isLoading, isError } = useQuery({
    queryKey: ['hr', 'employees', search, status, page],
    queryFn: () => hrApi.employees({ search: search || undefined, status: status || undefined, page, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  })

  return (
    <div>
      <PageHead title={t('hr.employees.title')}>
        {auth.hasPermission('HrActions.Create') && (
          <button className="btn btn-primary" onClick={() => navigate('/app/hr-actions?new=Hire')}>
            <UserPlus size={15} aria-hidden /> {t('hr.employees.newHire')}
          </button>
        )}
        {auth.hasPermission('Employees.Create') && (
          <button className="btn btn-ghost" onClick={() => setAdding((v) => !v)}>{t('hr.employees.addExisting')}</button>
        )}
      </PageHead>

      {adding && <AddExistingForm onDone={() => setAdding(false)} />}

      <section className="doc-card">
        <div className="form-grid">
          <LabeledInput label={t('hr.common.search')} placeholder={t('hr.employees.searchPlaceholder')} value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1) }} containerClassName="field--wide" />
          <LabeledSelect label={t('hr.common.status')} value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
            <option value="">{t('hr.common.all')}</option>
            {(['Active', 'Suspended', 'Terminated'] as const).map((s) => <option key={s} value={s}>{t(`hr.employeeStatus.${s}`)}</option>)}
          </LabeledSelect>
        </div>

        <div className="table-scroll">
          <table className="reg-table">
            <thead>
              <tr>
                <th>{t('hr.employees.number')}</th><th>{t('hr.employees.name')}</th><th>{t('hr.employees.category')}</th>
                <th>{t('hr.employees.unit')}</th><th>{t('hr.employees.jobTitle')}</th><th>{t('hr.employees.hireDate')}</th>
                <th>{t('hr.common.status')}</th><th aria-label={t('hr.common.actions')} />
              </tr>
            </thead>
            <tbody>
              {isLoading && <tr><td colSpan={8} className="reg-empty">{t('hr.common.loading')}</td></tr>}
              {isError && <tr><td colSpan={8} className="reg-empty">{t('hr.common.loadFailed')}</td></tr>}
              {data && data.items.length === 0 && <tr><td colSpan={8} className="reg-empty">{t('hr.common.empty')}</td></tr>}
              {data?.items.map((e) => (
                <tr key={e.id} className="reg-row" onDoubleClick={() => navigate(`/app/hr-employees/${e.id}`)}>
                  <td>{e.employeeNumber}</td>
                  <td>{e.fullName}</td>
                  <td>{t(`hr.category.${e.category}`)}</td>
                  <td>{e.orgUnitName ?? '—'}</td>
                  <td>{e.jobTitle}</td>
                  <td>{day(e.hireDate)}</td>
                  <td><EmployeeStatusPill status={e.status} /></td>
                  <td className="row-actions">
                    {auth.hasPermission('HrProfile.View') && (
                      <button className="btn btn-ghost btn-sm" title={t('hr.employees.openFile')} aria-label={t('hr.employees.openFile')}
                        onClick={() => navigate(`/app/hr-employees/${e.id}`)}>
                        <FolderOpen size={14} aria-hidden />
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {data && (
          <div className="row-actions" style={{ justifyContent: 'space-between', marginTop: '.75rem' }}>
            <span className="muted">{t('hr.common.count', { count: data.totalCount })}</span>
            <span className="row-actions">
              <button className="btn btn-ghost btn-sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>{t('hr.common.prev')}</button>
              <span className="muted">{page} / {Math.max(1, data.totalPages)}</span>
              <button className="btn btn-ghost btn-sm" disabled={page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>{t('hr.common.next')}</button>
            </span>
          </div>
        )}
      </section>
    </div>
  )
}

/** Launch data only: current staff entered once at go-live. New staff come through a hire action. */
function AddExistingForm({ onDone }: { onDone: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const units = useOrgUnits()
  const sites = useSites()
  const [busy, setBusy] = useState(false)
  const [f, setF] = useState<SaveEmployee>({
    employeeNumber: '', fullName: '', fullNameEn: null, category: 'Civil', orgUnitId: 0, positionId: null,
    jobTitle: '', siteId: null, hireDate: null, userId: null,
  })
  const set = <K extends keyof SaveEmployee>(k: K, v: SaveEmployee[K]) => setF((x) => ({ ...x, [k]: v }))

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    try {
      await hrApi.createEmployee({ ...f, fullNameEn: f.fullNameEn || null })
      toast.success(t('hr.common.saved'))
      await qc.invalidateQueries({ queryKey: ['hr', 'employees'] })
      onDone()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
    finally { setBusy(false) }
  }

  return (
    <section className="doc-card">
      <p className="muted">{t('hr.employees.addExistingHint')}</p>
      <form className="form-grid" onSubmit={submit}>
        <LabeledInput label={t('hr.employees.number')} required value={f.employeeNumber} onChange={(e) => set('employeeNumber', e.target.value)} />
        <LabeledInput label={t('hr.employees.name')} required value={f.fullName} onChange={(e) => set('fullName', e.target.value)} containerClassName="field--wide" />
        <LabeledInput label={t('hr.employees.nameEn')} value={f.fullNameEn ?? ''} onChange={(e) => set('fullNameEn', e.target.value)} dir="ltr" />
        <LabeledSelect label={t('hr.employees.category')} value={f.category} onChange={(e) => set('category', e.target.value as EmployeeCategory)}>
          {CATEGORIES.map((c) => <option key={c} value={c}>{t(`hr.category.${c}`)}</option>)}
        </LabeledSelect>
        <LabeledSelect label={t('hr.employees.unit')} required value={f.orgUnitId || ''} onChange={(e) => set('orgUnitId', Number(e.target.value))}>
          <option value="" />
          {units.filter((u) => u.isActive).map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
        </LabeledSelect>
        <LabeledInput label={t('hr.employees.jobTitle')} required value={f.jobTitle} onChange={(e) => set('jobTitle', e.target.value)} />
        <LabeledSelect label={t('hr.employees.site')} value={f.siteId ?? ''} onChange={(e) => set('siteId', e.target.value ? Number(e.target.value) : null)}>
          <option value="">{t('hr.common.none')}</option>
          {sites.filter((s) => s.isActive).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
        </LabeledSelect>
        <LabeledInput label={t('hr.employees.hireDate')} type="date" value={f.hireDate ?? ''} onChange={(e) => set('hireDate', e.target.value || null)} />
        <div className="form-actions">
          <button className="btn btn-primary" disabled={busy}>{t('hr.common.add')}</button>
          <button type="button" className="btn btn-ghost" onClick={onDone}>{t('hr.common.cancel')}</button>
        </div>
      </form>
    </section>
  )
}
