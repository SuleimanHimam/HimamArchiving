import { useState, type FormEvent, type ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Eye, EyeOff, Pencil, Trash2 } from 'lucide-react'
import { auth } from '../../lib/auth'
import {
  apiError, hrApi, type BankAccountDto, type EmployeeFileDto, type FamilyMemberDto, type QualificationDto,
  type SaveProfile, type SensitiveField,
} from '../../lib/hr'
import { LabeledInput, LabeledSelect } from '../../components/ui/labeled-input'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../components/ui/tabs'
import CustodyTable from '../supply/CustodyTable'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { ActionStatusPill, EmployeeStatusPill, PageHead } from './hrShared'
import { day, lookupName, useLookups } from '../../lib/hrHooks'
import { hrTime } from '../../lib/hrTime'

/** الملف الوظيفي (operations sequence §8.2). Each tab saves on its own; the server re-checks scope on every call. */
export default function EmployeeFilePage() {
  const { t, i18n } = useTranslation()
  const id = Number(useParams().id)
  const { data, isLoading, isError } = useQuery({ queryKey: ['hr', 'file', id], queryFn: () => hrApi.file(id), enabled: id > 0 })
  const canEdit = auth.hasPermission('HrProfile.Edit')

  if (isLoading) return <p className="muted">{t('hr.common.loading')}</p>
  if (isError || !data) return <p className="muted">{t('hr.common.loadFailed')}</p>

  const e = data.employee
  return (
    <div>
      <PageHead title={`${e.fullName} · ${e.employeeNumber}`}>
        <EmployeeStatusPill status={e.status} />
        <Link className="btn btn-ghost btn-sm" to="/app/hr-employees">{t('hr.file.back')}</Link>
      </PageHead>

      <Tabs defaultValue="personal" dir={i18n.language === 'ar' ? 'rtl' : 'ltr'}>
        <TabsList>
          {(['personal', 'family', 'qualifications', 'job', 'financial', 'actions', 'leaves', 'custody', 'documents'] as const)
            .map((k) => <TabsTrigger key={k} value={k}>{t(`hr.file.tabs.${k}`)}</TabsTrigger>)}
        </TabsList>
        {/* Keyed on the row version so a save (or another editor's) re-seeds the form. */}
        <TabsContent value="personal"><PersonalTab key={data.profile.rowVersion ?? 'new'} file={data} canEdit={canEdit} /></TabsContent>
        <TabsContent value="family"><FamilyTab file={data} canEdit={canEdit} /></TabsContent>
        <TabsContent value="qualifications"><QualificationsTab file={data} canEdit={canEdit} /></TabsContent>
        <TabsContent value="job"><JobTab file={data} /></TabsContent>
        <TabsContent value="financial"><FinancialTab file={data} canEdit={canEdit} /></TabsContent>
        <TabsContent value="actions"><ActionsTab file={data} /></TabsContent>
        <TabsContent value="custody"><section className="doc-card"><CustodyTable employeeId={data.employee.id} /></section></TabsContent>
        {(['leaves', 'documents'] as const).map((k) => (
          <TabsContent key={k} value={k}><section className="doc-card"><p className="muted">{t('hr.file.laterPhase')}</p></section></TabsContent>
        ))}
      </Tabs>
    </div>
  )
}

// ---- shared bits ----

function useRefresh(employeeId: number) {
  const qc = useQueryClient()
  return () => qc.invalidateQueries({ queryKey: ['hr', 'file', employeeId] })
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return <div className="detail-field"><span className="muted">{label}</span><div>{children}</div></div>
}

/** A masked value with an explicit, audited "show" button (standards §7.3). */
function Sensitive({ employeeId, field, recordId, masked }: { employeeId: number; field: SensitiveField; recordId?: number; masked: string | null }) {
  const { t } = useTranslation()
  const toast = useToast()
  const [value, setValue] = useState<string | null>(null)
  if (!masked) return <span className="muted">—</span>
  const canReveal = auth.hasPermission('HrSensitive.View')

  async function toggle() {
    if (value) { setValue(null); return }
    try { setValue(await hrApi.reveal(employeeId, field, recordId)) }
    catch (err) { toast.error(apiError(err, t('hr.common.loadFailed'))) }
  }

  return (
    <span className="row-actions">
      <span dir="ltr">{value ?? masked}</span>
      {canReveal && (
        <button type="button" className="btn btn-ghost btn-sm" onClick={toggle} title={t('hr.file.revealHint')}
          aria-label={value ? t('hr.file.hide') : t('hr.file.reveal')}>
          {value ? <EyeOff size={14} aria-hidden /> : <Eye size={14} aria-hidden />}
        </button>
      )}
    </span>
  )
}

// ---- tabs ----

function PersonalTab({ file, canEdit }: { file: EmployeeFileDto; canEdit: boolean }) {
  const { t } = useTranslation()
  const toast = useToast()
  const refresh = useRefresh(file.employee.id)
  const marital = useLookups('marital_status')
  const grades = useLookups('grade')
  const appointments = useLookups('appointment_type')
  const p = file.profile
  const [f, setF] = useState<SaveProfile>(() => ({
    birthDate: p.birthDate, maritalStatusId: p.maritalStatusId, address: p.address,
    appointmentTypeId: p.appointmentTypeId, gradeId: p.gradeId, nationalId: null, mobile: null, rowVersion: p.rowVersion,
  }))
  const [busy, setBusy] = useState(false)
  const set = <K extends keyof SaveProfile>(k: K, v: SaveProfile[K]) => setF((x) => ({ ...x, [k]: v }))

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    try {
      await hrApi.saveProfile(file.employee.id, {
        ...f, nationalId: f.nationalId?.trim() ? f.nationalId.trim() : null, mobile: f.mobile?.trim() ? f.mobile.trim() : null,
      })
      toast.success(t('hr.common.saved'))
      await refresh()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
    finally { setBusy(false) }
  }

  return (
    <section className="doc-card">
      <div className="form-grid">
        <Field label={t('hr.file.nationalId')}><Sensitive employeeId={file.employee.id} field="nationalId" masked={p.nationalIdMasked} /></Field>
        <Field label={t('hr.file.mobile')}><Sensitive employeeId={file.employee.id} field="mobile" masked={p.mobileMasked} /></Field>
      </div>
      <form className="form-grid" onSubmit={submit}>
        <LabeledInput label={t('hr.employees.name')} value={file.employee.fullName} disabled containerClassName="field--wide" />
        <LabeledInput label={t('hr.file.birthDate')} type="date" value={f.birthDate ?? ''} disabled={!canEdit}
          onChange={(e) => set('birthDate', e.target.value || null)} />
        <LabeledSelect label={t('hr.file.maritalStatus')} value={f.maritalStatusId ?? ''} disabled={!canEdit}
          onChange={(e) => set('maritalStatusId', e.target.value ? Number(e.target.value) : null)}>
          <option value="">{t('hr.common.none')}</option>
          {marital.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
        </LabeledSelect>
        <LabeledInput label={t('hr.file.address')} value={f.address ?? ''} disabled={!canEdit} containerClassName="field--wide"
          onChange={(e) => set('address', e.target.value || null)} />
        {canEdit && (
          <>
            <LabeledInput label={t('hr.file.nationalId')} hint={t('hr.file.sensitiveKeep')} dir="ltr" autoComplete="off"
              value={f.nationalId ?? ''} onChange={(e) => set('nationalId', e.target.value)} />
            <LabeledInput label={t('hr.file.mobile')} hint={t('hr.file.sensitiveKeep')} dir="ltr" autoComplete="off"
              value={f.mobile ?? ''} onChange={(e) => set('mobile', e.target.value)} />
          </>
        )}
        <LabeledSelect label={t('hr.file.grade')} hint={t('hr.file.setOnceHint')} value={f.gradeId ?? ''}
          disabled={!canEdit || p.gradeId != null} onChange={(e) => set('gradeId', e.target.value ? Number(e.target.value) : null)}>
          <option value="">{t('hr.common.none')}</option>
          {grades.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
        </LabeledSelect>
        <LabeledSelect label={t('hr.file.appointmentType')} hint={t('hr.file.setOnceHint')} value={f.appointmentTypeId ?? ''}
          disabled={!canEdit || p.appointmentTypeId != null}
          onChange={(e) => set('appointmentTypeId', e.target.value ? Number(e.target.value) : null)}>
          <option value="">{t('hr.common.none')}</option>
          {appointments.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
        </LabeledSelect>
        {canEdit && <div className="form-actions"><button className="btn btn-primary" disabled={busy}>{t('hr.common.save')}</button></div>}
      </form>
    </section>
  )
}

function FamilyTab({ file, canEdit }: { file: EmployeeFileDto; canEdit: boolean }) {
  const { t } = useTranslation()
  const relations = useLookups('relationship')
  return (
    <RowEditor<FamilyMemberDto, { fullName: string; relationshipId: number | null; birthDate: string; notes: string }>
      file={file} canEdit={canEdit} rows={file.family}
      empty={{ fullName: '', relationshipId: null, birthDate: '', notes: '' }}
      toForm={(r) => ({ fullName: r.fullName, relationshipId: r.relationshipId, birthDate: r.birthDate ?? '', notes: r.notes ?? '' })}
      headers={[t('hr.file.memberName'), t('hr.file.relationship'), t('hr.file.birthDate'), t('hr.common.notes')]}
      cells={(r) => [r.fullName, lookupName(relations, r.relationshipId) ?? '—', day(r.birthDate), r.notes ?? '—']}
      save={(emp, f, row) => {
        const body = { fullName: f.fullName, relationshipId: f.relationshipId ?? 0, birthDate: f.birthDate || null, notes: f.notes || null }
        return row ? hrApi.updateFamily(emp, row.id, { ...body, rowVersion: row.rowVersion }) : hrApi.addFamily(emp, body)
      }}
      remove={(emp, row) => hrApi.deleteFamily(emp, row.id)}
      fields={(f, set) => (
        <>
          <LabeledInput label={t('hr.file.memberName')} required value={f.fullName} onChange={(e) => set({ fullName: e.target.value })} containerClassName="field--wide" />
          <LabeledSelect label={t('hr.file.relationship')} required value={f.relationshipId ?? ''}
            onChange={(e) => set({ relationshipId: e.target.value ? Number(e.target.value) : null })}>
            <option value="" />
            {relations.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
          </LabeledSelect>
          <LabeledInput label={t('hr.file.birthDate')} type="date" value={f.birthDate} onChange={(e) => set({ birthDate: e.target.value })} />
          <LabeledInput label={t('hr.common.notes')} value={f.notes} onChange={(e) => set({ notes: e.target.value })} containerClassName="field--wide" />
        </>
      )}
    />
  )
}

function QualificationsTab({ file, canEdit }: { file: EmployeeFileDto; canEdit: boolean }) {
  const { t } = useTranslation()
  const types = useLookups('qualification_type')
  return (
    <RowEditor<QualificationDto, { qualificationTypeId: number | null; title: string; institution: string; year: string }>
      file={file} canEdit={canEdit} rows={file.qualifications}
      empty={{ qualificationTypeId: null, title: '', institution: '', year: '' }}
      toForm={(r) => ({ qualificationTypeId: r.qualificationTypeId, title: r.title, institution: r.institution ?? '', year: r.year?.toString() ?? '' })}
      headers={[t('hr.file.qualificationType'), t('hr.file.qualificationTitle'), t('hr.file.institution'), t('hr.file.year')]}
      cells={(r) => [lookupName(types, r.qualificationTypeId) ?? '—', r.title, r.institution ?? '—', r.year?.toString() ?? '—']}
      save={(emp, f, row) => {
        const body = { qualificationTypeId: f.qualificationTypeId ?? 0, title: f.title, institution: f.institution || null, year: f.year ? Number(f.year) : null }
        return row ? hrApi.updateQualification(emp, row.id, { ...body, rowVersion: row.rowVersion }) : hrApi.addQualification(emp, body)
      }}
      remove={(emp, row) => hrApi.deleteQualification(emp, row.id)}
      fields={(f, set) => (
        <>
          <LabeledSelect label={t('hr.file.qualificationType')} required value={f.qualificationTypeId ?? ''}
            onChange={(e) => set({ qualificationTypeId: e.target.value ? Number(e.target.value) : null })}>
            <option value="" />
            {types.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
          </LabeledSelect>
          <LabeledInput label={t('hr.file.qualificationTitle')} required value={f.title} onChange={(e) => set({ title: e.target.value })} containerClassName="field--wide" />
          <LabeledInput label={t('hr.file.institution')} value={f.institution} onChange={(e) => set({ institution: e.target.value })} />
          <LabeledInput label={t('hr.file.year')} type="number" inputMode="numeric" value={f.year} onChange={(e) => set({ year: e.target.value })} />
        </>
      )}
    />
  )
}

function FinancialTab({ file, canEdit }: { file: EmployeeFileDto; canEdit: boolean }) {
  const { t } = useTranslation()
  return (
    <>
      <RowEditor<BankAccountDto, { bankName: string; account: string; isPrimary: boolean }>
        file={file} canEdit={canEdit} rows={file.bankAccounts}
        empty={{ bankName: '', account: '', isPrimary: true }}
        toForm={(r) => ({ bankName: r.bankName, account: '', isPrimary: r.isPrimary })}
        headers={[t('hr.file.bankName'), t('hr.file.account'), t('hr.file.primary')]}
        cells={(r) => [r.bankName,
          <Sensitive key="a" employeeId={file.employee.id} field="bankAccount" recordId={r.id} masked={r.accountMasked} />,
          r.isPrimary ? t('hr.common.yes') : t('hr.common.no')]}
        save={(emp, f, row) => row
          ? hrApi.updateBank(emp, row.id, { bankName: f.bankName, account: f.account.trim() || null, isPrimary: f.isPrimary, rowVersion: row.rowVersion })
          : hrApi.addBank(emp, { bankName: f.bankName, account: f.account.trim(), isPrimary: f.isPrimary })}
        remove={(emp, row) => hrApi.deleteBank(emp, row.id)}
        fields={(f, set, editing) => (
          <>
            <LabeledInput label={t('hr.file.bankName')} required value={f.bankName} onChange={(e) => set({ bankName: e.target.value })} />
            <LabeledInput label={t('hr.file.account')} required={!editing} dir="ltr" autoComplete="off" value={f.account}
              hint={editing ? t('hr.file.sensitiveKeep') : undefined} onChange={(e) => set({ account: e.target.value })} containerClassName="field--wide" />
            <LabeledSelect label={t('hr.file.primary')} value={f.isPrimary ? '1' : '0'} onChange={(e) => set({ isPrimary: e.target.value === '1' })}>
              <option value="1">{t('hr.common.yes')}</option><option value="0">{t('hr.common.no')}</option>
            </LabeledSelect>
          </>
        )}
      />
      <p className="muted">{t('hr.file.salaryLater')}</p>
    </>
  )
}

function JobTab({ file }: { file: EmployeeFileDto }) {
  const { t } = useTranslation()
  const grades = useLookups('grade')
  const appointments = useLookups('appointment_type')
  const e = file.employee
  // Computed from the org structure (unit manager seat, walking up), never stored on the employee.
  const manager = useQuery({ queryKey: ['hr', 'manager', e.id], queryFn: () => hrTime.manager(e.id) }).data
  return (
    <section className="doc-card">
      <div className="form-grid">
        <Field label={t('hr.employees.number')}>{e.employeeNumber}</Field>
        <Field label={t('hr.employees.category')}>{t(`hr.category.${e.category}`)}</Field>
        <Field label={t('hr.employees.hireDate')}>{day(e.hireDate)}</Field>
        <Field label={t('hr.employees.unit')}>{e.orgUnitName ?? '—'}</Field>
        <Field label={t('hr.employees.jobTitle')}>{e.jobTitle}</Field>
        <Field label={t('hr.employees.site')}>{e.siteName ?? '—'}</Field>
        <Field label={t('hrt.org.directManager')}>{manager ? manager.fullName : t('hrt.org.noManagerFound')}</Field>
        <Field label={t('hr.file.grade')}>{lookupName(grades, file.profile.gradeId) ?? '—'}</Field>
        <Field label={t('hr.file.appointmentType')}>{lookupName(appointments, file.profile.appointmentTypeId) ?? '—'}</Field>
      </div>
    </section>
  )
}

function ActionsTab({ file }: { file: EmployeeFileDto }) {
  const { t } = useTranslation()
  return (
    <section className="doc-card">
      <div className="table-scroll">
        <table className="reg-table">
          <thead><tr><th>{t('hr.actions.number')}</th><th>{t('hr.actions.type')}</th><th>{t('hr.actions.effectiveDate')}</th><th>{t('hr.actions.reason')}</th><th>{t('hr.common.status')}</th></tr></thead>
          <tbody>
            {file.actions.length === 0 && <tr><td colSpan={5} className="reg-empty">{t('hr.common.empty')}</td></tr>}
            {file.actions.map((a) => (
              <tr key={a.id}>
                <td dir="ltr">{a.number ?? '—'}</td><td>{a.actionTypeName}</td><td>{day(a.effectiveDate)}</td>
                <td>{a.reason}</td><td><ActionStatusPill status={a.status} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

// ---- generic sub-table editor (family, qualifications, bank accounts) ----

interface RowEditorProps<R extends { id: number }, F> {
  file: EmployeeFileDto
  canEdit: boolean
  rows: R[]
  empty: F
  toForm: (row: R) => F
  headers: string[]
  cells: (row: R) => ReactNode[]
  save: (employeeId: number, form: F, row: R | null) => Promise<unknown>
  remove: (employeeId: number, row: R) => Promise<unknown>
  fields: (form: F, set: (patch: Partial<F>) => void, editing: boolean) => ReactNode
}

function RowEditor<R extends { id: number }, F>(p: RowEditorProps<R, F>) {
  const { t } = useTranslation()
  const toast = useToast()
  const { confirm } = useConfirmDialog()
  const refresh = useRefresh(p.file.employee.id)
  const [editing, setEditing] = useState<R | null>(null)
  const [form, setForm] = useState<F>(p.empty)
  const [busy, setBusy] = useState(false)
  const emp = p.file.employee.id

  function reset() { setEditing(null); setForm(p.empty) }

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    try { await p.save(emp, form, editing); toast.success(t('hr.common.saved')); reset(); await refresh() }
    catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
    finally { setBusy(false) }
  }

  async function del(row: R) {
    if (!(await confirm({ message: t('hr.common.confirmDelete'), destructive: true }))) return
    try { await p.remove(emp, row); toast.success(t('hr.common.deleted')); if (editing?.id === row.id) reset(); await refresh() }
    catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <section className="doc-card">
      <div className="table-scroll">
        <table className="reg-table">
          <thead><tr>{p.headers.map((h) => <th key={h}>{h}</th>)}{p.canEdit && <th aria-label={t('hr.common.actions')} />}</tr></thead>
          <tbody>
            {p.rows.length === 0 && <tr><td colSpan={p.headers.length + 1} className="reg-empty">{t('hr.common.empty')}</td></tr>}
            {p.rows.map((row) => (
              <tr key={row.id} className={editing?.id === row.id ? 'reg-row is-editing' : 'reg-row'}>
                {p.cells(row).map((c, i) => <td key={i}>{c}</td>)}
                {p.canEdit && (
                  <td className="row-actions">
                    <button className="btn btn-ghost btn-sm" title={t('hr.common.edit')} aria-label={t('hr.common.edit')}
                      onClick={() => { setEditing(row); setForm(p.toForm(row)) }}><Pencil size={14} aria-hidden /></button>
                    <button className="btn btn-ghost btn-sm" title={t('hr.common.delete')} aria-label={t('hr.common.delete')}
                      onClick={() => del(row)}><Trash2 size={14} aria-hidden /></button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {p.canEdit && (
        <form className="form-grid" onSubmit={submit} style={{ marginTop: '1rem' }}>
          {p.fields(form, (patch) => setForm((f) => ({ ...f, ...patch })), editing !== null)}
          <div className="form-actions">
            <button className="btn btn-primary" disabled={busy}>{editing ? t('hr.common.save') : t('hr.common.add')}</button>
            {editing && <button type="button" className="btn btn-ghost" onClick={reset}>{t('hr.common.cancel')}</button>}
          </div>
        </form>
      )}
    </section>
  )
}
