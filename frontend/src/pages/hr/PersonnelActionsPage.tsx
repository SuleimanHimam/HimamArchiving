import { Fragment, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { Ban, Pencil, Plus, Send } from 'lucide-react'
import { auth } from '../../lib/auth'
import { organization } from '../../lib/organization'
import {
  apiError, hrApi, type ActionEffect, type ActionPayload, type ActionTypeDto, type EmployeeCategory,
  type PersonnelActionDto, type SavePersonnelAction,
} from '../../lib/hr'
import { LabeledInput, LabeledSelect, LabeledTextarea } from '../../components/ui/labeled-input'
import { useExpandedRow, ExpandCell, ExpandHeadCell, ExpandedRow, DetailFields, DetailField } from '../../components/ui/expandable-row'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { ActionStatusPill, PageHead } from './hrShared'
import { day, useLookups, useOrgUnits, useSites } from '../../lib/hrHooks'

const PAGE_SIZE = 25
const today = () => new Date().toISOString().slice(0, 10)

/** الحركات الإدارية (operations sequence §8.3): draft → submit to the type's approval path → executed on approval. */
export default function PersonnelActionsPage() {
  const { t } = useTranslation()
  const toast = useToast()
  const { confirm } = useConfirmDialog()
  const qc = useQueryClient()
  const [params, setParams] = useSearchParams()
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState<PersonnelActionDto | null>(null)
  const { toggle, isExpanded } = useExpandedRow<number>()
  const canCreate = auth.hasPermission('HrActions.Create')
  const newEffect = params.get('new') as ActionEffect | null

  const types = useQuery({ queryKey: ['hr', 'actionTypes'], queryFn: () => hrApi.actionTypes(true) }).data ?? []
  const { data, isLoading, isError } = useQuery({
    queryKey: ['hr', 'actions', status, page],
    queryFn: () => hrApi.actions({ status: status || undefined, page, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  })

  const formOpen = canCreate && (editing !== null || newEffect !== null)
  const closeForm = () => { setEditing(null); if (newEffect) setParams({}) }
  const refresh = () => qc.invalidateQueries({ queryKey: ['hr', 'actions'] })

  async function act(a: PersonnelActionDto, kind: 'submit' | 'cancel') {
    const ok = await confirm({ message: t(kind === 'submit' ? 'hr.actions.confirmSubmit' : 'hr.actions.confirmCancel'), destructive: kind === 'cancel' })
    if (!ok) return
    try {
      if (kind === 'submit') await hrApi.submitAction(a.id)
      else await hrApi.cancelAction(a.id)
      toast.success(t(kind === 'submit' ? 'hr.actions.submitted' : 'hr.actions.cancelled'))
      await refresh()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <div>
      <PageHead title={t('hr.actions.title')}>
        {canCreate && !formOpen && (
          <button className="btn btn-primary" onClick={() => setParams({ new: '' })}><Plus size={15} aria-hidden /> {t('hr.actions.new')}</button>
        )}
      </PageHead>

      {formOpen && (
        <ActionForm key={editing?.id ?? `new-${newEffect}-${types.length}`} types={types} existing={editing} presetEffect={newEffect || null}
          onDone={async () => { closeForm(); await refresh() }} onCancel={closeForm} />
      )}

      <section className="doc-card">
        <div className="form-grid">
          <LabeledSelect label={t('hr.common.status')} value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
            <option value="">{t('hr.common.all')}</option>
            {(['Draft', 'Submitted', 'Returned', 'Executed', 'Rejected', 'Cancelled'] as const)
              .map((s) => <option key={s} value={s}>{t(`hr.actionStatus.${s}`)}</option>)}
          </LabeledSelect>
        </div>
        <div className="table-scroll">
          <table className="reg-table">
            <thead>
              <tr>
                <ExpandHeadCell />
                <th>{t('hr.actions.number')}</th><th>{t('hr.actions.type')}</th><th>{t('hr.actions.employee')}</th>
                <th>{t('hr.actions.effectiveDate')}</th><th>{t('hr.common.status')}</th><th aria-label={t('hr.common.actions')} />
              </tr>
            </thead>
            <tbody>
              {isLoading && <tr><td colSpan={7} className="reg-empty">{t('hr.common.loading')}</td></tr>}
              {isError && <tr><td colSpan={7} className="reg-empty">{t('hr.common.loadFailed')}</td></tr>}
              {data && data.items.length === 0 && <tr><td colSpan={7} className="reg-empty">{t('hr.common.empty')}</td></tr>}
              {data?.items.map((a) => {
                const open = isExpanded(a.id)
                const editable = canCreate && (a.status === 'Draft' || a.status === 'Returned')
                return (
                  <Fragment key={a.id}>
                    <tr className="reg-row">
                      <ExpandCell expanded={open} onToggle={() => toggle(a.id)} />
                      <td dir="ltr">{a.number ?? '—'}</td>
                      <td>{a.actionTypeName}</td>
                      <td>{a.employeeName ?? '—'}</td>
                      <td>{day(a.effectiveDate)}</td>
                      <td><ActionStatusPill status={a.status} /></td>
                      <td className="row-actions">
                        {editable && (
                          <>
                            <button className="btn btn-ghost btn-sm" title={t('hr.common.edit')} aria-label={t('hr.common.edit')}
                              onClick={() => { setParams({}); setEditing(a) }}><Pencil size={14} aria-hidden /></button>
                            <button className="btn btn-ghost btn-sm" title={t('hr.actions.submit')} aria-label={t('hr.actions.submit')}
                              onClick={() => act(a, 'submit')}><Send size={14} aria-hidden /></button>
                            <button className="btn btn-ghost btn-sm" title={t('hr.actions.cancelAction')} aria-label={t('hr.actions.cancelAction')}
                              onClick={() => act(a, 'cancel')}><Ban size={14} aria-hidden /></button>
                          </>
                        )}
                      </td>
                    </tr>
                    <ExpandedRow colSpan={7} expanded={open}>
                      <DetailFields>
                        <DetailField label={t('hr.actions.reason')} wide>{a.reason}</DetailField>
                        {a.decisionNote && <DetailField label={t('hr.actions.decisionNote')} wide>{a.decisionNote}</DetailField>}
                        {a.beforeJson && <DetailField label={t('hr.actions.before')}><code dir="ltr">{a.beforeJson}</code></DetailField>}
                        {a.afterJson && <DetailField label={t('hr.actions.after')}><code dir="ltr">{a.afterJson}</code></DetailField>}
                      </DetailFields>
                    </ExpandedRow>
                  </Fragment>
                )
              })}
            </tbody>
          </table>
        </div>
        {data && data.totalPages > 1 && (
          <div className="row-actions" style={{ justifyContent: 'flex-end', marginTop: '.75rem' }}>
            <button className="btn btn-ghost btn-sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>{t('hr.common.prev')}</button>
            <span className="muted">{page} / {data.totalPages}</span>
            <button className="btn btn-ghost btn-sm" disabled={page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>{t('hr.common.next')}</button>
          </div>
        )}
      </section>
    </div>
  )
}

// ---- the form: one set of fields per effect, as PersonnelActionPayload documents ----

function ActionForm({ types, existing, presetEffect, onDone, onCancel }: {
  types: ActionTypeDto[]; existing: PersonnelActionDto | null; presetEffect: ActionEffect | null
  onDone: () => void; onCancel: () => void
}) {
  const { t } = useTranslation()
  const toast = useToast()
  const units = useOrgUnits()
  const sites = useSites()
  const grades = useLookups('grade')
  const appointments = useLookups('appointment_type')
  const employees = useQuery({
    queryKey: ['hr', 'employees', 'picker'],
    queryFn: () => hrApi.employees({ pageSize: 200 }),
  }).data?.items.filter((e) => e.status !== 'Terminated') ?? []

  const preset = presetEffect ? types.find((x) => x.effect === presetEffect) : undefined
  const [f, setF] = useState<SavePersonnelAction>(() => existing
    ? { actionTypeId: existing.actionTypeId, employeeId: existing.employeeId, effectiveDate: existing.effectiveDate, reason: existing.reason, payload: existing.payload, rowVersion: existing.rowVersion }
    : { actionTypeId: preset?.id ?? 0, employeeId: null, effectiveDate: today(), reason: '', payload: {} })
  const [busy, setBusy] = useState(false)
  const type = types.find((x) => x.id === f.actionTypeId)
  const effect = type?.effect
  const p = f.payload
  const setP = (patch: ActionPayload) => setF((x) => ({ ...x, payload: { ...x.payload, ...patch } }))

  const positions = useQuery({
    queryKey: ['hr', 'positions', p.orgUnitId],
    queryFn: () => organization.positions(p.orgUnitId!),
    enabled: !!p.orgUnitId && (effect === 'Transfer' || effect === 'Hire'),
  }).data ?? []

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    try {
      const body = { ...f, employeeId: effect === 'Hire' ? null : f.employeeId }
      if (existing) await hrApi.updateAction(existing.id, body)
      else await hrApi.createAction(body)
      toast.success(t('hr.common.saved'))
      onDone()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
    finally { setBusy(false) }
  }

  const num = (v: string) => (v ? Number(v) : null)
  const unitSelect = (label: string, required = true) => (
    <LabeledSelect label={label} required={required} value={p.orgUnitId ?? ''} onChange={(e) => setP({ orgUnitId: num(e.target.value), positionId: null })}>
      <option value="" />
      {units.filter((u) => u.isActive).map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
    </LabeledSelect>
  )
  const positionSelect = (
    <LabeledSelect label={t('hr.actions.newPosition')} value={p.positionId ?? ''} onChange={(e) => setP({ positionId: num(e.target.value) })}>
      <option value="">{t('hr.actions.keepCurrent')}</option>
      {positions.filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.title}</option>)}
    </LabeledSelect>
  )
  const siteSelect = (label: string, required = false) => (
    <LabeledSelect label={label} required={required} value={p.siteId ?? ''} onChange={(e) => setP({ siteId: num(e.target.value) })}>
      <option value="">{required ? '' : t('hr.actions.keepCurrent')}</option>
      {sites.filter((s) => s.isActive).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
    </LabeledSelect>
  )
  const lookupSelect = (label: string, items: typeof grades, value: number | null | undefined, onChange: (v: number | null) => void, required = true) => (
    <LabeledSelect label={label} required={required} value={value ?? ''} onChange={(e) => onChange(num(e.target.value))}>
      <option value="" />
      {items.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
    </LabeledSelect>
  )

  return (
    <section className="doc-card">
      <h3 className="detail-h3">{existing ? t('hr.actions.editTitle') : t('hr.actions.new')}</h3>
      <form className="form-grid" onSubmit={submit}>
        <LabeledSelect label={t('hr.actions.type')} required value={f.actionTypeId || ''} disabled={!!existing}
          onChange={(e) => setF((x) => ({ ...x, actionTypeId: Number(e.target.value), payload: {} }))}>
          <option value="">{t('hr.actions.pickType')}</option>
          {types.map((x) => <option key={x.id} value={x.id}>{x.name}{x.workflowDefinitionId ? '' : ` (${t('hr.actions.noPath')})`}</option>)}
        </LabeledSelect>

        {effect && effect !== 'Hire' && (
          <LabeledSelect label={t('hr.actions.employee')} required value={f.employeeId ?? ''}
            onChange={(e) => setF((x) => ({ ...x, employeeId: num(e.target.value) }))}>
            <option value="">{t('hr.actions.pickEmployee')}</option>
            {employees.map((x) => <option key={x.id} value={x.id}>{x.employeeNumber} · {x.fullName}</option>)}
          </LabeledSelect>
        )}

        <LabeledInput label={t('hr.actions.effectiveDate')} type="date" required value={f.effectiveDate}
          onChange={(e) => setF((x) => ({ ...x, effectiveDate: e.target.value }))} />

        {effect === 'Hire' && (
          <>
            <LabeledInput label={t('hr.employees.number')} required value={p.employeeNumber ?? ''} onChange={(e) => setP({ employeeNumber: e.target.value })} />
            <LabeledInput label={t('hr.employees.name')} required value={p.fullName ?? ''} onChange={(e) => setP({ fullName: e.target.value })} containerClassName="field--wide" />
            <LabeledInput label={t('hr.employees.nameEn')} dir="ltr" value={p.fullNameEn ?? ''} onChange={(e) => setP({ fullNameEn: e.target.value || null })} />
            <LabeledSelect label={t('hr.employees.category')} required value={p.category ?? ''} onChange={(e) => setP({ category: (e.target.value || null) as EmployeeCategory | null })}>
              <option value="" />
              {(['Military', 'Civil', 'Contract'] as const).map((c) => <option key={c} value={c}>{t(`hr.category.${c}`)}</option>)}
            </LabeledSelect>
            {unitSelect(t('hr.employees.unit'))}
            {positionSelect}
            <LabeledInput label={t('hr.employees.jobTitle')} required value={p.jobTitle ?? ''} onChange={(e) => setP({ jobTitle: e.target.value })} />
            {siteSelect(t('hr.employees.site'))}
            {lookupSelect(t('hr.file.grade'), grades, p.gradeId, (v) => setP({ gradeId: v }), false)}
            {lookupSelect(t('hr.file.appointmentType'), appointments, p.appointmentTypeId, (v) => setP({ appointmentTypeId: v }), false)}
          </>
        )}
        {effect === 'Transfer' && (
          <>
            {unitSelect(t('hr.actions.newUnit'))}
            {positionSelect}
            <LabeledInput label={t('hr.employees.jobTitle')} value={p.jobTitle ?? ''} placeholder={t('hr.actions.keepCurrent')}
              onChange={(e) => setP({ jobTitle: e.target.value || null })} />
            {siteSelect(t('hr.employees.site'))}
          </>
        )}
        {effect === 'Promote' && lookupSelect(t('hr.actions.newGrade'), grades, p.gradeId, (v) => setP({ gradeId: v }))}
        {effect === 'Confirm' && lookupSelect(t('hr.actions.newAppointment'), appointments, p.appointmentTypeId, (v) => setP({ appointmentTypeId: v }))}
        {effect === 'Second' && (
          <>
            {siteSelect(t('hr.actions.secondSite'), true)}
            <LabeledInput label={t('hr.actions.toDate')} type="date" required value={p.toDate ?? ''} onChange={(e) => setP({ toDate: e.target.value || null })} />
          </>
        )}
        {effect && (
          <LabeledTextarea label={t('hr.actions.reason')} required containerClassName="field--wide" value={f.reason}
            onChange={(e) => setF((x) => ({ ...x, reason: e.target.value }))} />
        )}
        {effect && ['Second', 'Allowance', 'Penalty', 'Suspend', 'EndService'].includes(effect) && (
          <LabeledTextarea label={t('hr.common.notes')} containerClassName="field--wide" value={p.notes ?? ''}
            hint={['Second', 'Allowance', 'Penalty'].includes(effect) ? t('hr.actions.recordOnly') : undefined}
            onChange={(e) => setP({ notes: e.target.value || null })} />
        )}
        {type && !type.workflowDefinitionId && <p className="muted field--wide">{t('hr.actions.noPathHint')}</p>}

        <div className="form-actions">
          <button className="btn btn-primary" disabled={busy || !effect}>{t('hr.common.save')}</button>
          <button type="button" className="btn btn-ghost" onClick={onCancel}>{t('hr.common.cancel')}</button>
        </div>
      </form>
    </section>
  )
}
