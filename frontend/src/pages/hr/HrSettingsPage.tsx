import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil, Trash2 } from 'lucide-react'
import { auth } from '../../lib/auth'
import { workflow } from '../../lib/workflow'
import {
  ACTION_EFFECTS, LOOKUP_CATEGORIES, apiError, hrApi, type ActionTypeDto, type LookupCategory, type LookupDto,
  type SaveActionType, type SaveLookup,
} from '../../lib/hr'
import { LabeledInput, LabeledSelect } from '../../components/ui/labeled-input'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../components/ui/tabs'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { PageHead } from './hrShared'
import { LeaveTypesSettings, WorkCalendarSettings } from './HrTimeSettings'

/**
 * إعدادات الموارد البشرية: every value the Authority has not fixed in writing is a list here
 * (grades, appointment types, clearance parties, …) or an action type with its approval path.
 */
export default function HrSettingsPage() {
  const { t, i18n } = useTranslation()
  return (
    <div>
      <PageHead title={t('hr.settings.title')} />
      <Tabs defaultValue="types" dir={i18n.language === 'ar' ? 'rtl' : 'ltr'}>
        <TabsList>
          <TabsTrigger value="types">{t('hr.settings.actionTypes')}</TabsTrigger>
          <TabsTrigger value="lists">{t('hr.settings.lists')}</TabsTrigger>
          <TabsTrigger value="leaveTypes">{t('hrt.leaveSet.tab')}</TabsTrigger>
          <TabsTrigger value="calendar">{t('hrt.cal.tab')}</TabsTrigger>
        </TabsList>
        <TabsContent value="types"><ActionTypes /></TabsContent>
        <TabsContent value="lists"><Lists /></TabsContent>
        <TabsContent value="leaveTypes"><LeaveTypesSettings /></TabsContent>
        <TabsContent value="calendar"><WorkCalendarSettings /></TabsContent>
      </Tabs>
    </div>
  )
}

function ActionTypes() {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const canEdit = auth.hasPermission('HrSettings.Edit')
  const types = useQuery({ queryKey: ['hr', 'actionTypes', 'all'], queryFn: () => hrApi.actionTypes(false) })
  const workflows = useQuery({ queryKey: ['workflow', 'definitions'], queryFn: workflow.definitions, enabled: canEdit }).data ?? []
  const empty: SaveActionType = { code: '', name: '', effect: 'Penalty', workflowDefinitionId: null, isActive: true }
  const [editing, setEditing] = useState<ActionTypeDto | null>(null)
  const [f, setF] = useState<SaveActionType>(empty)
  const [busy, setBusy] = useState(false)

  function reset() { setEditing(null); setF(empty) }

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    try {
      if (editing) await hrApi.updateActionType(editing.id, { ...f, rowVersion: editing.rowVersion })
      else await hrApi.createActionType(f)
      toast.success(t('hr.common.saved'))
      reset()
      await qc.invalidateQueries({ queryKey: ['hr', 'actionTypes'] })
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
    finally { setBusy(false) }
  }

  return (
    <section className="doc-card">
      <p className="muted">{t('hr.settings.workflowHint')}</p>
      <div className="table-scroll">
        <table className="reg-table">
          <thead>
            <tr>
              <th>{t('hr.settings.code')}</th><th>{t('hr.settings.name')}</th><th>{t('hr.settings.effect')}</th>
              <th>{t('hr.settings.workflow')}</th><th>{t('hr.common.status')}</th>{canEdit && <th aria-label={t('hr.common.actions')} />}
            </tr>
          </thead>
          <tbody>
            {types.isLoading && <tr><td colSpan={6} className="reg-empty">{t('hr.common.loading')}</td></tr>}
            {types.data?.map((x) => (
              <tr key={x.id} className={editing?.id === x.id ? 'reg-row is-editing' : 'reg-row'}>
                <td dir="ltr">{x.code}</td>
                <td>{x.name}</td>
                <td>{t(`hr.effect.${x.effect}`)}</td>
                <td>{x.workflowDefinitionName ?? <span className="status-pill s-onhold">{t('hr.actions.noPath')}</span>}</td>
                <td>{x.isActive ? t('hr.common.active') : t('hr.common.inactive')}</td>
                {canEdit && (
                  <td className="row-actions">
                    <button className="btn btn-ghost btn-sm" title={t('hr.common.edit')} aria-label={t('hr.common.edit')}
                      onClick={() => { setEditing(x); setF({ code: x.code, name: x.name, effect: x.effect, workflowDefinitionId: x.workflowDefinitionId, isActive: x.isActive }) }}>
                      <Pencil size={14} aria-hidden />
                    </button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {canEdit && (
        <form className="form-grid" onSubmit={submit} style={{ marginTop: '1rem' }}>
          <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{editing ? t('hr.common.edit') : t('hr.settings.addType')}</h4>
          <LabeledInput label={t('hr.settings.code')} required dir="ltr" value={f.code} onChange={(e) => setF((x) => ({ ...x, code: e.target.value }))} />
          <LabeledInput label={t('hr.settings.name')} required value={f.name} onChange={(e) => setF((x) => ({ ...x, name: e.target.value }))} containerClassName="field--wide" />
          <LabeledSelect label={t('hr.settings.effect')} value={f.effect} onChange={(e) => setF((x) => ({ ...x, effect: e.target.value as SaveActionType['effect'] }))}>
            {ACTION_EFFECTS.map((k) => <option key={k} value={k}>{t(`hr.effect.${k}`)}</option>)}
          </LabeledSelect>
          <LabeledSelect label={t('hr.settings.workflow')} value={f.workflowDefinitionId ?? ''}
            onChange={(e) => setF((x) => ({ ...x, workflowDefinitionId: e.target.value ? Number(e.target.value) : null }))}>
            <option value="">{t('hr.actions.noPath')}</option>
            {workflows.filter((w) => w.isActive).map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
          </LabeledSelect>
          <LabeledSelect label={t('hr.common.status')} value={f.isActive ? '1' : '0'} onChange={(e) => setF((x) => ({ ...x, isActive: e.target.value === '1' }))}>
            <option value="1">{t('hr.common.active')}</option><option value="0">{t('hr.common.inactive')}</option>
          </LabeledSelect>
          <div className="form-actions">
            <button className="btn btn-primary" disabled={busy}>{editing ? t('hr.common.save') : t('hr.common.add')}</button>
            {editing && <button type="button" className="btn btn-ghost" onClick={reset}>{t('hr.common.cancel')}</button>}
          </div>
        </form>
      )}
    </section>
  )
}

function Lists() {
  const { t } = useTranslation()
  const toast = useToast()
  const { confirm } = useConfirmDialog()
  const qc = useQueryClient()
  const canEdit = auth.hasPermission('Lookups.Edit')
  const [category, setCategory] = useState<LookupCategory>('grade')
  const rows = useQuery({ queryKey: ['hr', 'lookups', 'admin', category], queryFn: () => hrApi.lookups(category) })
  const empty = (): SaveLookup => ({ category, code: '', name: '', nameEn: null, sortOrder: 0, isActive: true })
  const [editing, setEditing] = useState<LookupDto | null>(null)
  const [f, setF] = useState<SaveLookup>(empty)
  const [busy, setBusy] = useState(false)

  function reset(next: LookupCategory = category) {
    setEditing(null)
    setF({ category: next, code: '', name: '', nameEn: null, sortOrder: 0, isActive: true })
  }
  const refresh = () => qc.invalidateQueries({ queryKey: ['hr', 'lookups'] })

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    try {
      const body = { ...f, category, nameEn: f.nameEn || null }
      if (editing) await hrApi.updateLookup(editing.id, { ...body, rowVersion: editing.rowVersion })
      else await hrApi.createLookup(body)
      toast.success(t('hr.common.saved'))
      reset()
      await refresh()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
    finally { setBusy(false) }
  }

  async function remove(l: LookupDto) {
    if (!(await confirm({ message: t('hr.common.confirmDelete'), destructive: true }))) return
    try { await hrApi.deleteLookup(l.id); toast.success(t('hr.common.deleted')); if (editing?.id === l.id) reset(); await refresh() }
    catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <section className="doc-card">
      <div className="form-grid">
        <LabeledSelect label={t('hr.settings.lists')} value={category}
          onChange={(e) => { const c = e.target.value as LookupCategory; setCategory(c); reset(c) }}>
          {LOOKUP_CATEGORIES.map((c) => <option key={c} value={c}>{t(`hr.settings.category.${c}`)}</option>)}
        </LabeledSelect>
      </div>
      <div className="table-scroll">
        <table className="reg-table">
          <thead>
            <tr>
              <th>{t('hr.settings.code')}</th><th>{t('hr.settings.name')}</th><th>{t('hr.settings.nameEn')}</th>
              <th>{t('hr.settings.order')}</th><th>{t('hr.common.status')}</th>{canEdit && <th aria-label={t('hr.common.actions')} />}
            </tr>
          </thead>
          <tbody>
            {rows.isLoading && <tr><td colSpan={6} className="reg-empty">{t('hr.common.loading')}</td></tr>}
            {rows.data?.length === 0 && <tr><td colSpan={6} className="reg-empty">{t('hr.common.empty')}</td></tr>}
            {rows.data?.map((l) => (
              <tr key={l.id} className={editing?.id === l.id ? 'reg-row is-editing' : 'reg-row'}>
                <td dir="ltr">{l.code}</td><td>{l.name}</td><td dir="ltr">{l.nameEn ?? '—'}</td><td>{l.sortOrder}</td>
                <td>{l.isActive ? t('hr.common.active') : t('hr.common.inactive')}</td>
                {canEdit && (
                  <td className="row-actions">
                    <button className="btn btn-ghost btn-sm" title={t('hr.common.edit')} aria-label={t('hr.common.edit')}
                      onClick={() => { setEditing(l); setF({ category: l.category, code: l.code, name: l.name, nameEn: l.nameEn, sortOrder: l.sortOrder, isActive: l.isActive }) }}>
                      <Pencil size={14} aria-hidden />
                    </button>
                    <button className="btn btn-ghost btn-sm" title={t('hr.common.delete')} aria-label={t('hr.common.delete')} onClick={() => remove(l)}>
                      <Trash2 size={14} aria-hidden />
                    </button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {canEdit && (
        <form className="form-grid" onSubmit={submit} style={{ marginTop: '1rem' }}>
          <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{editing ? t('hr.common.edit') : t('hr.settings.addValue')}</h4>
          <LabeledInput label={t('hr.settings.code')} required dir="ltr" value={f.code} onChange={(e) => setF((x) => ({ ...x, code: e.target.value }))} />
          <LabeledInput label={t('hr.settings.name')} required value={f.name} onChange={(e) => setF((x) => ({ ...x, name: e.target.value }))} />
          <LabeledInput label={t('hr.settings.nameEn')} dir="ltr" value={f.nameEn ?? ''} onChange={(e) => setF((x) => ({ ...x, nameEn: e.target.value }))} />
          <LabeledInput label={t('hr.settings.order')} type="number" value={f.sortOrder} onChange={(e) => setF((x) => ({ ...x, sortOrder: Number(e.target.value) || 0 }))} />
          <LabeledSelect label={t('hr.common.status')} value={f.isActive ? '1' : '0'} onChange={(e) => setF((x) => ({ ...x, isActive: e.target.value === '1' }))}>
            <option value="1">{t('hr.common.active')}</option><option value="0">{t('hr.common.inactive')}</option>
          </LabeledSelect>
          <div className="form-actions">
            <button className="btn btn-primary" disabled={busy}>{editing ? t('hr.common.save') : t('hr.common.add')}</button>
            {editing && <button type="button" className="btn btn-ghost" onClick={() => reset()}>{t('hr.common.cancel')}</button>}
          </div>
        </form>
      )}
    </section>
  )
}
