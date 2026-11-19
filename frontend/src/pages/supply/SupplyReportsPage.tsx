import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { Printer } from 'lucide-react'
import { day } from '../../lib/hrHooks'
import { todayHebron } from '../../lib/hrtUtils'
import { supply, money, num } from '../../lib/supply'
import { LabeledInput, LabeledSelect } from '../../components/ui/labeled-input'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../components/ui/tabs'
import { PageHead } from '../hr/hrShared'
import { KindPill } from './supplyShared'
import { useItems, useWarehouses } from '../../lib/supplyHooks'

const monthStart = () => `${todayHebron().slice(0, 8)}01`

/** التقارير (§7.4): balances, item card, consumption with comparison, maintenance cost, stagnant items. */
export default function SupplyReportsPage() {
  const { t, i18n } = useTranslation()
  return (
    <div>
      <PageHead title={t('sup.rep.title')}>
        <button className="btn btn-ghost" onClick={() => window.print()}><Printer size={15} aria-hidden /> {t('sup.common.print')}</button>
      </PageHead>
      <Tabs defaultValue="balances" dir={i18n.language === 'ar' ? 'rtl' : 'ltr'}>
        <TabsList>
          {['balances', 'card', 'consumption', 'maintenance', 'stagnant'].map((k) => <TabsTrigger key={k} value={k}>{t(`sup.rep.${k}`)}</TabsTrigger>)}
        </TabsList>
        <TabsContent value="balances"><Balances /></TabsContent>
        <TabsContent value="card"><ItemCard /></TabsContent>
        <TabsContent value="consumption"><Consumption /></TabsContent>
        <TabsContent value="maintenance"><Maintenance /></TabsContent>
        <TabsContent value="stagnant"><Stagnant /></TabsContent>
      </Tabs>
    </div>
  )
}

function Balances() {
  const { t } = useTranslation()
  const warehouses = useWarehouses()
  const [wh, setWh] = useState<number | ''>('')
  const { data } = useQuery({ queryKey: ['supply', 'rep', 'balances', wh], queryFn: () => supply.balances(wh || undefined) })
  return (
    <section className="doc-card">
      <div className="sup-toolbar">
        <LabeledSelect label={t('sup.req.warehouse')} value={wh} onChange={(e) => setWh(e.target.value ? Number(e.target.value) : '')}>
          <option value="">{t('hr.common.all')}</option>{warehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
        </LabeledSelect>
      </div>
      <Table head={[t('sup.req.warehouse'), t('sup.item.code'), t('sup.item.item'), t('sup.item.kind'), t('sup.item.balance')]}
        rows={(data ?? []).map((r) => [r.warehouseName, <span key="c" dir="ltr" className="mono">{r.itemCode}</span>, r.itemName, <KindPill key="k" kind={r.kind} />, `${num(r.balance)} ${r.unit}`])} />
    </section>
  )
}

function ItemCard() {
  const { t } = useTranslation()
  const items = useItems(false)
  const warehouses = useWarehouses()
  const [p, setP] = useState({ itemId: 0, warehouseId: 0, from: monthStart(), to: todayHebron() })
  const { data } = useQuery({ queryKey: ['supply', 'rep', 'card', p], queryFn: () => supply.itemCard(p.itemId, p.from, p.to, p.warehouseId || undefined), enabled: p.itemId > 0 })
  return (
    <section className="doc-card">
      <div className="sup-toolbar">
        <LabeledSelect label={t('sup.item.item')} value={p.itemId || ''} onChange={(e) => setP({ ...p, itemId: Number(e.target.value) })}>
          <option value="" />{items.map((i) => <option key={i.id} value={i.id}>{i.code} · {i.name}</option>)}
        </LabeledSelect>
        <LabeledSelect label={t('sup.req.warehouse')} value={p.warehouseId || ''} onChange={(e) => setP({ ...p, warehouseId: Number(e.target.value) })}>
          <option value="">{t('hr.common.all')}</option>{warehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
        </LabeledSelect>
        <LabeledInput label={t('sup.rep.from')} type="date" dir="ltr" value={p.from} onChange={(e) => setP({ ...p, from: e.target.value })} />
        <LabeledInput label={t('sup.rep.to')} type="date" dir="ltr" value={p.to} onChange={(e) => setP({ ...p, to: e.target.value })} />
      </div>
      {data && (
        <>
          <p className="muted">{t('sup.rep.opening')}: <strong>{num(data.opening)}</strong> · {t('sup.rep.closing')}: <strong>{num(data.closing)}</strong> {data.item.unit}</p>
          <Table head={[t('sup.doc.date'), t('sup.doc.number'), t('sup.doc.type'), t('sup.req.warehouse'), t('sup.rep.in'), t('sup.rep.out'), t('sup.item.balance')]}
            rows={data.lines.map((l) => [day(l.date), <span key="n" dir="ltr" className="mono">{l.documentNumber}</span>, t(`sup.docType.${l.documentType}`), l.warehouseName,
              l.in ? num(l.in) : '', l.out ? num(l.out) : '', num(l.balance)])} />
        </>
      )}
    </section>
  )
}

function Range({ v, set }: { v: { from: string; to: string }; set: (v: { from: string; to: string }) => void }) {
  const { t } = useTranslation()
  return (
    <div className="sup-toolbar">
      <LabeledInput label={t('sup.rep.from')} type="date" dir="ltr" value={v.from} onChange={(e) => set({ ...v, from: e.target.value })} />
      <LabeledInput label={t('sup.rep.to')} type="date" dir="ltr" value={v.to} onChange={(e) => set({ ...v, to: e.target.value })} />
    </div>
  )
}

function Consumption() {
  const { t } = useTranslation()
  const [r, setR] = useState({ from: monthStart(), to: todayHebron() })
  const { data } = useQuery({ queryKey: ['supply', 'rep', 'cons', r], queryFn: () => supply.consumption(r.from, r.to) })
  return (
    <section className="doc-card">
      <Range v={r} set={setR} />
      <Table head={[t('sup.req.unit'), t('sup.item.item'), t('sup.rep.period'), t('sup.rep.previous'), t('sup.rep.change')]}
        rows={(data ?? []).map((x) => [x.orgUnitName, x.itemName, `${num(x.quantity)} ${x.unit}`, num(x.previousQuantity),
          x.previousQuantity ? `${Math.round(((x.quantity - x.previousQuantity) / x.previousQuantity) * 100)}%` : '—'])} />
    </section>
  )
}

function Maintenance() {
  const { t } = useTranslation()
  const [r, setR] = useState({ from: `${todayHebron().slice(0, 4)}-01-01`, to: todayHebron() })
  const { data } = useQuery({ queryKey: ['supply', 'rep', 'wo', r], queryFn: () => supply.maintenanceReport(r.from, r.to) })
  return (
    <section className="doc-card">
      <Range v={r} set={setR} />
      <Table head={[t('sup.asset.number'), t('sup.item.item'), t('sup.rep.orders'), t('sup.wo.cost')]}
        rows={(data ?? []).map((x) => [x.assetNumber ?? t('sup.rep.places'), x.itemName ?? '—', x.orders, money(x.cost)])} />
    </section>
  )
}

function Stagnant() {
  const { t } = useTranslation()
  const { data } = useQuery({ queryKey: ['supply', 'rep', 'stagnant'], queryFn: supply.stagnant })
  return (
    <section className="doc-card">
      <Table head={[t('sup.item.item'), t('sup.item.balance'), t('sup.rep.lastMovement')]}
        rows={(data ?? []).map((x) => [x.itemName, `${num(x.balance)} ${x.unit}`, day(x.lastMovement)])} />
    </section>
  )
}

function Table({ head, rows }: { head: string[]; rows: React.ReactNode[][] }) {
  const { t } = useTranslation()
  return (
    <div className="table-scroll">
      <table className="reg-table">
        <thead><tr>{head.map((h) => <th key={h}>{h}</th>)}</tr></thead>
        <tbody>
          {rows.length === 0 && <tr><td colSpan={head.length} className="reg-empty">{t('hr.common.empty')}</td></tr>}
          {rows.map((r, i) => <tr key={i}>{r.map((c, j) => <td key={j}>{c}</td>)}</tr>)}
        </tbody>
      </table>
    </div>
  )
}
