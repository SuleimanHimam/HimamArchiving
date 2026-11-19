import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { auth } from '../../lib/auth'
import { apiError } from '../../lib/hr'
import { useOrgUnits, useSites } from '../../lib/hrHooks'
import { workflow } from '../../lib/workflow'
import { supply, num, DOC_TYPES, WAREHOUSE_KINDS, type SaveQuota, type SaveSupplySettings, type SaveWarehouse, type WarehouseDto } from '../../lib/supply'
import { LabeledInput, LabeledSelect } from '../../components/ui/labeled-input'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../components/ui/tabs'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { PageHead } from '../hr/hrShared'
import { Check, EmployeeSelect } from '../hr/hrtShared'
import { useItems, useWarehouses } from '../../lib/supplyHooks'

/** إعدادات اللوازم: warehouses and keepers, monthly quotas, approval paths and alert thresholds — all data, nothing in code. */
export default function SupplySettingsPage() {
  const { t, i18n } = useTranslation()
  return (
    <div>
      <PageHead title={t('sup.set.title')} />
      <Tabs defaultValue="warehouses" dir={i18n.language === 'ar' ? 'rtl' : 'ltr'}>
        <TabsList>
          <TabsTrigger value="warehouses">{t('sup.set.warehouses')}</TabsTrigger>
          <TabsTrigger value="paths">{t('sup.set.paths')}</TabsTrigger>
          <TabsTrigger value="quotas">{t('sup.set.quotas')}</TabsTrigger>
        </TabsList>
        <TabsContent value="warehouses"><Warehouses /></TabsContent>
        <TabsContent value="paths"><Paths /></TabsContent>
        <TabsContent value="quotas"><Quotas /></TabsContent>
      </Tabs>
    </div>
  )
}

function Warehouses() {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const sites = useSites()
  const rows = useWarehouses()
  const canEdit = auth.hasPermission('SupplyItems.Edit')
  const [form, setForm] = useState<WarehouseDto | 'new' | null>(null)
  const blank: SaveWarehouse = { code: '', name: '', kind: 'Main', siteId: null, keeperEmployeeId: null, isActive: true }
  const [f, setF] = useState<SaveWarehouse>(blank)
  const edit = (w: WarehouseDto | 'new') => {
    setForm(w)
    setF(w === 'new' ? blank : { code: w.code, name: w.name, kind: w.kind, siteId: w.siteId, keeperEmployeeId: w.keeperEmployeeId, isActive: w.isActive, rowVersion: w.rowVersion })
  }
  async function save(e: FormEvent) {
    e.preventDefault()
    try {
      await supply.saveWarehouse(form === 'new' || !form ? null : form.id, f)
      toast.success(t('hr.common.saved'))
      setForm(null)
      await qc.invalidateQueries({ queryKey: ['supply'] })
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }
  return (
    <section className="doc-card">
      {canEdit && !form && <div className="row-actions" style={{ marginBottom: '.6rem' }}><button className="btn btn-primary" onClick={() => edit('new')}><Plus size={15} aria-hidden /> {t('sup.set.newWarehouse')}</button></div>}
      {form && (
        <form className="form-grid" onSubmit={save} style={{ marginBottom: '1rem' }}>
          <LabeledInput label={t('sup.item.code')} required dir="ltr" value={f.code} onChange={(e) => setF({ ...f, code: e.target.value })} />
          <LabeledInput label={t('sup.item.name')} required value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} />
          <LabeledSelect label={t('sup.item.kind')} value={f.kind} onChange={(e) => setF({ ...f, kind: e.target.value })}>
            {WAREHOUSE_KINDS.map((k) => <option key={k} value={k}>{t(`sup.whKind.${k}`)}</option>)}
          </LabeledSelect>
          <LabeledSelect label={t('sup.count.site')} value={f.siteId ?? ''} onChange={(e) => setF({ ...f, siteId: Number(e.target.value) || null })}>
            <option value="" />{sites.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </LabeledSelect>
          <EmployeeSelect label={t('sup.set.keeper')} value={f.keeperEmployeeId} onChange={(v) => setF({ ...f, keeperEmployeeId: v })} />
          <Check label={t('hr.common.active')} checked={f.isActive} onChange={(v) => setF({ ...f, isActive: v })} />
          <div className="form-actions">
            <button className="btn btn-primary">{t('hr.common.save')}</button>
            <button type="button" className="btn btn-ghost" onClick={() => setForm(null)}>{t('hr.common.cancel')}</button>
          </div>
        </form>
      )}
      <div className="table-scroll">
        <table className="reg-table">
          <thead><tr><th>{t('sup.item.code')}</th><th>{t('sup.item.name')}</th><th>{t('sup.item.kind')}</th><th>{t('sup.count.site')}</th><th>{t('sup.set.keeper')}</th><th>{t('hr.common.status')}</th>{canEdit && <th aria-label={t('hr.common.actions')} />}</tr></thead>
          <tbody>
            {rows.length === 0 && <tr><td colSpan={7} className="reg-empty">{t('hr.common.empty')}</td></tr>}
            {rows.map((w) => (
              <tr key={w.id}>
                <td dir="ltr" className="mono">{w.code}</td><td>{w.name}</td><td>{t(`sup.whKind.${w.kind}`)}</td><td>{w.siteName ?? '—'}</td>
                <td>{w.keeperName ?? <span className="status-pill s-onhold">{t('sup.set.noKeeper')}</span>}</td>
                <td>{w.isActive ? t('hr.common.active') : t('hr.common.inactive')}</td>
                {canEdit && <td className="row-actions"><button className="btn btn-ghost btn-sm" aria-label={t('hr.common.edit')} title={t('hr.common.edit')} onClick={() => edit(w)}><Pencil size={14} aria-hidden /></button></td>}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function Paths() {
  const { t } = useTranslation()
  const toast = useToast()
  const canEdit = auth.hasPermission('SupplyItems.Edit')
  const settings = useQuery({ queryKey: ['supply', 'settings'], queryFn: supply.settings })
  const workflows = useQuery({ queryKey: ['workflow', 'definitions'], queryFn: workflow.definitions, enabled: canEdit }).data ?? []
  const [f, setF] = useState<SaveSupplySettings | null>(null)
  const v: SaveSupplySettings | null = f ?? (settings.data ? { ...settings.data } : null)
  if (!v) return <p className="muted">{t('hr.common.loading')}</p>
  const set = (p: Partial<SaveSupplySettings>) => setF({ ...v, ...p })
  const optInt = (s: string) => (s === '' ? null : Number(s))

  async function save(e: FormEvent) {
    e.preventDefault()
    try {
      await supply.saveSettings(v!)
      toast.success(t('hr.common.saved'))
      setF(null)
      await settings.refetch()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <form className="doc-card form-grid" onSubmit={save}>
      <p className="muted field--wide" style={{ margin: 0 }}>{t('sup.set.pathsHelp')}</p>
      {[...DOC_TYPES, 'REQ'].map((k) => (
        <LabeledSelect key={k} label={k === 'REQ' ? t('sup.set.reqPath') : t(`sup.docType.${k}`)} disabled={!canEdit} value={v.workflows[k] ?? ''}
          onChange={(e) => set({ workflows: { ...v.workflows, [k]: e.target.value ? Number(e.target.value) : null } })}>
          <option value="">{t('sup.set.noPath')}</option>
          {workflows.filter((w) => w.isActive && w.triggerModule === 'ApprovalRequest').map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
        </LabeledSelect>
      ))}
      <h4 className="detail-h3" style={{ gridColumn: '1 / -1', marginBottom: 0 }}>{t('sup.set.thresholds')}</h4>
      <LabeledInput label={t('sup.set.replacementPct')} type="number" min={0} dir="ltr" disabled={!canEdit} value={v.replacementCostPercent ?? ''} onChange={(e) => set({ replacementCostPercent: optInt(e.target.value) })} />
      <LabeledInput label={t('sup.set.warrantyDays')} type="number" min={0} dir="ltr" disabled={!canEdit} value={v.warrantyAlertDays ?? ''} onChange={(e) => set({ warrantyAlertDays: optInt(e.target.value) })} />
      <LabeledInput label={t('sup.set.suspendedDays')} type="number" min={0} dir="ltr" disabled={!canEdit} value={v.suspendedRequestDays ?? ''} onChange={(e) => set({ suspendedRequestDays: optInt(e.target.value) })} />
      <LabeledInput label={t('sup.set.stagnantDays')} type="number" min={0} dir="ltr" disabled={!canEdit} value={v.stagnantDays ?? ''} onChange={(e) => set({ stagnantDays: optInt(e.target.value) })} />
      <LabeledInput label={t('sup.set.countOverdueDays')} type="number" min={0} dir="ltr" disabled={!canEdit} value={v.countOverdueDays ?? ''} onChange={(e) => set({ countOverdueDays: optInt(e.target.value) })} />
      {canEdit && <div className="form-actions"><button className="btn btn-primary">{t('hr.common.save')}</button></div>}
    </form>
  )
}

function Quotas() {
  const { t } = useTranslation()
  const toast = useToast()
  const { confirm } = useConfirmDialog()
  const canEdit = auth.hasPermission('SupplyItems.Edit')
  const units = useOrgUnits()
  const items = useItems(true).filter((i) => i.kind === 'Consumable')
  const warehouses = useWarehouses().filter((w) => w.isActive && w.kind !== 'Maintenance')
  const { data, refetch } = useQuery({ queryKey: ['supply', 'quotas'], queryFn: supply.quotas })
  const [f, setF] = useState<SaveQuota>({ orgUnitId: 0, itemId: 0, warehouseId: 0, quantity: 1, isActive: true })

  async function add(e: FormEvent) {
    e.preventDefault()
    try {
      await supply.saveQuota(null, { ...f, warehouseId: f.warehouseId || warehouses[0]?.id || 0 })
      toast.success(t('hr.common.saved'))
      await refetch()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }
  async function remove(id: number) {
    if (!await confirm({ message: t('sup.set.confirmRemoveQuota'), destructive: true })) return
    try { await supply.deleteQuota(id); await refetch() } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <section className="doc-card">
      <p className="muted" style={{ marginTop: 0 }}>{t('sup.set.quotasHelp')}</p>
      {canEdit && (
        <form className="form-grid" onSubmit={add} style={{ marginBottom: '1rem' }}>
          <LabeledSelect label={t('sup.req.unit')} required value={f.orgUnitId || ''} onChange={(e) => setF({ ...f, orgUnitId: Number(e.target.value) })}>
            <option value="" />{units.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
          </LabeledSelect>
          <LabeledSelect label={t('sup.item.item')} required value={f.itemId || ''} onChange={(e) => setF({ ...f, itemId: Number(e.target.value) })}>
            <option value="" />{items.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}
          </LabeledSelect>
          <LabeledSelect label={t('sup.req.warehouse')} value={f.warehouseId || warehouses[0]?.id || ''} onChange={(e) => setF({ ...f, warehouseId: Number(e.target.value) })}>
            {warehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
          </LabeledSelect>
          <LabeledInput label={t('sup.set.monthlyQty')} type="number" min={0} step="any" dir="ltr" required value={f.quantity} onChange={(e) => setF({ ...f, quantity: Number(e.target.value) })} />
          <div className="form-actions"><button className="btn btn-primary"><Plus size={15} aria-hidden /> {t('sup.common.add')}</button></div>
        </form>
      )}
      <div className="table-scroll">
        <table className="reg-table">
          <thead><tr><th>{t('sup.req.unit')}</th><th>{t('sup.item.item')}</th><th>{t('sup.req.warehouse')}</th><th className="num">{t('sup.set.monthlyQty')}</th>{canEdit && <th aria-label={t('hr.common.actions')} />}</tr></thead>
          <tbody>
            {data?.length === 0 && <tr><td colSpan={5} className="reg-empty">{t('hr.common.empty')}</td></tr>}
            {data?.map((q) => (
              <tr key={q.id}>
                <td>{q.orgUnitName}</td><td>{q.itemName}</td><td>{q.warehouseName}</td><td className="num">{num(q.quantity)} {q.unit}</td>
                {canEdit && <td className="row-actions"><button className="btn btn-ghost btn-sm" aria-label={t('sup.common.remove')} title={t('sup.common.remove')} onClick={() => remove(q.id)}><Trash2 size={14} aria-hidden /></button></td>}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}
