import { useMemo, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { FolderPlus, Pencil, Plus, Tag } from 'lucide-react'
import { auth } from '../../lib/auth'
import { apiError } from '../../lib/hr'
import { supply, num, ITEM_KINDS, type CategoryDto, type ItemDto, type ItemKind, type SaveCategory, type SaveItem } from '../../lib/supply'
import { LabeledInput, LabeledSelect } from '../../components/ui/labeled-input'
import { useToast } from '../../components/toast'
import { PageHead } from '../hr/hrShared'
import { Check } from '../hr/hrtShared'
import { KindPill } from './supplyShared'
import { useWarehouses } from '../../lib/supplyHooks'

type Filter = { kind: 'all' } | { kind: 'kind'; value: ItemKind } | { kind: 'category'; id: number }

/** دليل الأصناف (screen 12): category tree, stock per warehouse, rows at/below the reorder level in red. */
export default function ItemsPage() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const canEdit = auth.hasPermission('SupplyItems.Edit')
  const warehouses = useWarehouses().filter((w) => w.isActive)
  const categoryData = useQuery({ queryKey: ['supply', 'categories'], queryFn: supply.categories }).data
  const categories = useMemo(() => categoryData ?? [], [categoryData])
  const stock = useQuery({ queryKey: ['supply', 'catalogue'], queryFn: supply.catalogue })
  const [filter, setFilter] = useState<Filter>({ kind: 'all' })
  const [search, setSearch] = useState('')
  const [itemForm, setItemForm] = useState<ItemDto | 'new' | null>(null)
  const [catForm, setCatForm] = useState<CategoryDto | 'new' | null>(null)
  const refresh = () => qc.invalidateQueries({ queryKey: ['supply'] })

  const rows = useMemo(() => {
    const childOf = (id: number) => categories.filter((c) => c.parentId === id).map((c) => c.id)
    return (stock.data ?? []).filter((r) => {
      if (filter.kind === 'kind' && r.item.kind !== filter.value) return false
      if (filter.kind === 'category' && r.item.categoryId !== filter.id && !childOf(filter.id).includes(r.item.categoryId)) return false
      const q = search.trim()
      return !q || r.item.name.includes(q) || r.item.code.toLowerCase().includes(q.toLowerCase())
    })
  }, [stock.data, filter, search, categories])

  return (
    <div>
      <PageHead title={t('sup.item.title')}>
        {canEdit && <button className="btn btn-ghost" onClick={() => setCatForm('new')}><FolderPlus size={15} aria-hidden /> {t('sup.item.newCategory')}</button>}
        {canEdit && <button className="btn btn-primary" onClick={() => setItemForm('new')}><Plus size={15} aria-hidden /> {t('sup.item.new')}</button>}
      </PageHead>
      <p className="muted" style={{ marginTop: '-.5rem' }}>{t('sup.item.subtitle')}</p>

      {catForm && <CategoryForm key={catForm === 'new' ? 'new' : catForm.id} existing={catForm === 'new' ? null : catForm} categories={categories}
        onDone={async () => { setCatForm(null); await refresh() }} onCancel={() => setCatForm(null)} />}
      {itemForm && <ItemForm key={itemForm === 'new' ? 'new' : itemForm.id} existing={itemForm === 'new' ? null : itemForm} categories={categories}
        onDone={async () => { setItemForm(null); await refresh() }} onCancel={() => setItemForm(null)} />}

      <div className="sup-split">
        <nav className="sup-tree" aria-label={t('sup.item.tree')}>
          <button className={filter.kind === 'all' ? 'is-active' : ''} onClick={() => setFilter({ kind: 'all' })}>
            <span><Tag size={14} aria-hidden /> {t('sup.item.all')}</span><span className="sup-count">{stock.data?.length ?? 0}</span>
          </button>
          {ITEM_KINDS.map((k) => (
            <div key={k} className="sup-tree__kind">
              <button className={filter.kind === 'kind' && filter.value === k ? 'is-active' : ''} onClick={() => setFilter({ kind: 'kind', value: k })}>
                <KindPill kind={k} />
              </button>
              {categories.filter((c) => c.kind === k && c.parentId == null).map((c) => (
                <div key={c.id}>
                  <button className={filter.kind === 'category' && filter.id === c.id ? 'is-active' : ''} onClick={() => setFilter({ kind: 'category', id: c.id })}
                    onDoubleClick={() => canEdit && setCatForm(c)}>
                    <span>{c.name}</span><span className="sup-count">{c.itemCount + categories.filter((s) => s.parentId === c.id).reduce((n, s) => n + s.itemCount, 0)}</span>
                  </button>
                  {categories.filter((s) => s.parentId === c.id).map((s) => (
                    <button key={s.id} className={`sup-tree__sub ${filter.kind === 'category' && filter.id === s.id ? 'is-active' : ''}`}
                      onClick={() => setFilter({ kind: 'category', id: s.id })} onDoubleClick={() => canEdit && setCatForm(s)}>
                      <span>{s.name}</span><span className="sup-count">{s.itemCount}</span>
                    </button>
                  ))}
                </div>
              ))}
            </div>
          ))}
        </nav>

        <section className="doc-card">
          <div className="sup-toolbar">
            <LabeledInput label={t('hr.common.search')} value={search} onChange={(e) => setSearch(e.target.value)} containerClassName="grow" />
            <span className="muted">{rows.length} / {stock.data?.length ?? 0}</span>
          </div>
          <div className="table-scroll">
            <table className="reg-table">
              <thead>
                <tr>
                  <th>{t('sup.item.code')}</th><th>{t('sup.item.item')}</th><th>{t('sup.item.kind')}</th><th>{t('sup.item.unit')}</th>
                  {warehouses.map((w) => <th key={w.id} className="num">{w.name}</th>)}
                  <th className="num">{t('sup.item.total')}</th><th className="num">{t('sup.item.reorder')}</th>
                  {canEdit && <th aria-label={t('hr.common.actions')} />}
                </tr>
              </thead>
              <tbody>
                {stock.isLoading && <tr><td colSpan={7 + warehouses.length} className="reg-empty">{t('hr.common.loading')}</td></tr>}
                {rows.length === 0 && !stock.isLoading && <tr><td colSpan={7 + warehouses.length} className="reg-empty">{t('hr.common.empty')}</td></tr>}
                {rows.map((r) => (
                  <tr key={r.item.id} className={r.low ? 'is-low' : ''}>
                    <td dir="ltr" className="mono">{r.item.code}</td>
                    <td><strong>{r.item.name}</strong>{!r.item.isActive && <span className="muted"> · {t('hr.common.inactive')}</span>}</td>
                    <td><KindPill kind={r.item.kind} /></td>
                    <td>{r.item.unit}</td>
                    {warehouses.map((w) => <td key={w.id} className="num">{num(r.byWarehouse[w.id] ?? 0)}</td>)}
                    <td className="num"><strong>{num(r.total)}</strong></td>
                    <td className="num">{r.item.reorderLevel == null ? '—' : num(r.item.reorderLevel)}</td>
                    {canEdit && (
                      <td className="row-actions">
                        <button className="btn btn-ghost btn-sm" title={t('hr.common.edit')} aria-label={t('hr.common.edit')} onClick={() => setItemForm(r.item)}>
                          <Pencil size={14} aria-hidden /></button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </div>
  )
}

function ItemForm({ existing, categories, onDone, onCancel }: {
  existing: ItemDto | null; categories: CategoryDto[]; onDone: () => Promise<void>; onCancel: () => void
}) {
  const { t } = useTranslation()
  const toast = useToast()
  const [f, setF] = useState<SaveItem>(existing
    ? { code: existing.code, name: existing.name, categoryId: existing.categoryId, unit: existing.unit, reorderLevel: existing.reorderLevel,
        minLevel: existing.minLevel, usefulLifeMonths: existing.usefulLifeMonths, warrantyMonths: existing.warrantyMonths, isActive: existing.isActive,
        rowVersion: existing.rowVersion }
    : { code: '', name: '', categoryId: 0, unit: '', reorderLevel: null, minLevel: null, usefulLifeMonths: null, warrantyMonths: null, isActive: true })
  const set = (p: Partial<SaveItem>) => setF((x) => ({ ...x, ...p }))
  const kind = categories.find((c) => c.id === f.categoryId)?.kind
  const optNum = (v: string) => (v === '' ? null : Number(v))

  async function submit(e: FormEvent) {
    e.preventDefault()
    try {
      await supply.saveItem(existing?.id ?? null, f)
      toast.success(t('hr.common.saved'))
      await onDone()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <form className="doc-card form-grid" onSubmit={submit} style={{ marginBottom: '1rem' }}>
      <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{existing ? t('hr.common.edit') : t('sup.item.new')}</h4>
      <LabeledInput label={t('sup.item.code')} required dir="ltr" value={f.code} onChange={(e) => set({ code: e.target.value })} />
      <LabeledInput label={t('sup.item.name')} required value={f.name} onChange={(e) => set({ name: e.target.value })} />
      <LabeledSelect label={t('sup.item.category')} required value={f.categoryId || ''} onChange={(e) => set({ categoryId: Number(e.target.value) })}>
        <option value="" />
        {categories.filter((c) => c.isActive).map((c) => (
          <option key={c.id} value={c.id}>{c.parentId ? `— ${c.name}` : c.name} ({t(`sup.kind.${c.kind}`)})</option>
        ))}
      </LabeledSelect>
      <LabeledInput label={t('sup.item.unit')} required value={f.unit} onChange={(e) => set({ unit: e.target.value })} />
      <LabeledInput label={t('sup.item.reorder')} type="number" min={0} step="any" dir="ltr" value={f.reorderLevel ?? ''} onChange={(e) => set({ reorderLevel: optNum(e.target.value) })} />
      <LabeledInput label={t('sup.item.min')} type="number" min={0} step="any" dir="ltr" value={f.minLevel ?? ''} onChange={(e) => set({ minLevel: optNum(e.target.value) })} />
      {kind === 'FixedAsset' && (
        <>
          <LabeledInput label={t('sup.item.life')} type="number" min={0} dir="ltr" value={f.usefulLifeMonths ?? ''} onChange={(e) => set({ usefulLifeMonths: optNum(e.target.value) })} />
          <LabeledInput label={t('sup.item.warranty')} type="number" min={0} dir="ltr" value={f.warrantyMonths ?? ''} onChange={(e) => set({ warrantyMonths: optNum(e.target.value) })} />
        </>
      )}
      <Check label={t('hr.common.active')} checked={f.isActive} onChange={(v) => set({ isActive: v })} />
      <div className="form-actions">
        <button className="btn btn-primary">{t('hr.common.save')}</button>
        <button type="button" className="btn btn-ghost" onClick={onCancel}>{t('hr.common.cancel')}</button>
      </div>
    </form>
  )
}

function CategoryForm({ existing, categories, onDone, onCancel }: {
  existing: CategoryDto | null; categories: CategoryDto[]; onDone: () => Promise<void>; onCancel: () => void
}) {
  const { t } = useTranslation()
  const toast = useToast()
  const [f, setF] = useState<SaveCategory>(existing
    ? { parentId: existing.parentId, code: existing.code, name: existing.name, kind: existing.kind, isActive: existing.isActive, rowVersion: existing.rowVersion }
    : { parentId: null, code: '', name: '', kind: 'Consumable', isActive: true })
  const set = (p: Partial<SaveCategory>) => setF((x) => ({ ...x, ...p }))

  async function submit(e: FormEvent) {
    e.preventDefault()
    try {
      await supply.saveCategory(existing?.id ?? null, f)
      toast.success(t('hr.common.saved'))
      await onDone()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <form className="doc-card form-grid" onSubmit={submit} style={{ marginBottom: '1rem' }}>
      <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{existing ? t('hr.common.edit') : t('sup.item.newCategory')}</h4>
      <LabeledInput label={t('sup.item.code')} required dir="ltr" value={f.code} onChange={(e) => set({ code: e.target.value })} />
      <LabeledInput label={t('sup.item.name')} required value={f.name} onChange={(e) => set({ name: e.target.value })} />
      <LabeledSelect label={t('sup.item.kind')} value={f.kind} onChange={(e) => set({ kind: e.target.value as ItemKind, parentId: null })}>
        {ITEM_KINDS.map((k) => <option key={k} value={k}>{t(`sup.kind.${k}`)}</option>)}
      </LabeledSelect>
      <LabeledSelect label={t('sup.item.parent')} value={f.parentId ?? ''} onChange={(e) => set({ parentId: e.target.value ? Number(e.target.value) : null })}>
        <option value="">{t('sup.item.mainCategory')}</option>
        {categories.filter((c) => c.parentId == null && c.kind === f.kind && c.id !== existing?.id).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
      </LabeledSelect>
      <Check label={t('hr.common.active')} checked={f.isActive} onChange={(v) => set({ isActive: v })} />
      <div className="form-actions">
        <button className="btn btn-primary">{t('hr.common.save')}</button>
        <button type="button" className="btn btn-ghost" onClick={onCancel}>{t('hr.common.cancel')}</button>
      </div>
    </form>
  )
}
