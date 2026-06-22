import { useEffect, useState, useCallback, type FormEvent, type ReactNode } from 'react'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { auth } from '../../lib/auth'
import {
  locations,
  type Building, type Room, type Cabinet, type Shelf, type Box,
} from '../../lib/locations'
import { useToast } from '../../components/toast'
import BoxLabel from '../../components/BoxLabel'
import '../incoming/incoming.css'

const errOf = (e: unknown, f: string) => (e as AxiosError<{ error?: string }>).response?.data?.error ?? f

interface FieldDef { key: string; label: string; type?: 'text' | 'number'; required?: boolean; dir?: 'ltr' }
type Row = { id: number; isActive?: boolean }

/** Generic add/edit/delete/select panel for one hierarchy level. */
function CrudLevel<T extends Row>({
  title, items, fields, badge, onCreate, onUpdate, onDelete, onSelect, selectedId, canEdit, canCreate, canDelete, extra,
}: {
  title: string
  items: T[]
  fields: FieldDef[]
  badge: (it: T) => string
  onCreate: (rec: Record<string, string>) => Promise<void>
  onUpdate: (id: number, rec: Record<string, string>) => Promise<void>
  onDelete: (it: T) => Promise<void>
  onSelect?: (it: T) => void
  selectedId?: number | null
  canEdit: boolean; canCreate: boolean; canDelete: boolean
  extra?: (it: T) => ReactNode
}) {
  const empty = Object.fromEntries(fields.map((f) => [f.key, ''])) as Record<string, string>
  const [form, setForm] = useState<Record<string, string>>(empty)
  const [editId, setEditId] = useState<number | null>(null)
  const [busy, setBusy] = useState(false)

  function edit(it: T) { setEditId(it.id); setForm(Object.fromEntries(fields.map((f) => [f.key, String((it as Record<string, unknown>)[f.key] ?? '')])) as Record<string, string>) }
  function reset() { setEditId(null); setForm(empty) }

  async function submit(e: FormEvent) {
    e.preventDefault()
    const req = fields.find((f) => f.required && !form[f.key]?.trim())
    if (req) return
    setBusy(true)
    try { if (editId) await onUpdate(editId, form); else await onCreate(form); reset() }
    finally { setBusy(false) }
  }

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} style={{ marginBottom: '1rem' }}>
      <h3 className="detail-h3">{title} ({items.length})</h3>
      <div className="table-scroll">
        <table className="reg-table">
          <tbody>
            {items.length === 0 && <tr><td className="reg-empty">لا يوجد</td></tr>}
            {items.map((it) => (
              <tr key={it.id} className={`reg-row${selectedId === it.id ? ' is-editing' : ''}`}>
                <td onClick={() => onSelect?.(it)} style={{ cursor: onSelect ? 'pointer' : 'default' }}>{badge(it)}{it.isActive === false && <span className="badge" style={{ marginInlineStart: 6 }}>معطّل</span>}</td>
                <td className="row-actions" style={{ width: 1, whiteSpace: 'nowrap' }}>
                  {extra?.(it)}
                  {canEdit && <button className="btn btn-ghost btn-sm" title="تعديل" onClick={() => edit(it)}>✏️</button>}
                  {canDelete && <button className="btn btn-ghost btn-sm" title="حذف" onClick={() => onDelete(it)}>🗑</button>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {(canCreate || editId) && (
        <form className="form-grid" onSubmit={submit} style={{ marginTop: '.8rem' }}>
          {fields.map((f) => (
            <label className="field" key={f.key}><span>{f.label}{f.required ? ' *' : ''}</span>
              <input value={form[f.key] ?? ''} type={f.type ?? 'text'} dir={f.dir} onChange={(e) => setForm((s) => ({ ...s, [f.key]: e.target.value }))} /></label>
          ))}
          <div className="form-actions">
            <button className="btn btn-primary" disabled={busy}>{editId ? 'حفظ' : '+ إضافة'}</button>
            {editId && <button type="button" className="btn btn-ghost" onClick={reset}>إلغاء</button>}
          </div>
        </form>
      )}
    </motion.section>
  )
}

export default function LocationsPage() {
  const toast = useToast()
  const canCreate = auth.hasPermission('Archive.Create')
  const canEdit = auth.hasPermission('Archive.Edit')
  const canDelete = auth.hasPermission('Archive.Delete')

  const [buildings, setBuildings] = useState<Building[]>([])
  const [rooms, setRooms] = useState<Room[]>([])
  const [cabinets, setCabinets] = useState<Cabinet[]>([])
  const [shelves, setShelves] = useState<Shelf[]>([])
  const [boxes, setBoxes] = useState<Box[]>([])

  const [bSel, setBSel] = useState<Building | null>(null)
  const [rSel, setRSel] = useState<Room | null>(null)
  const [cSel, setCSel] = useState<Cabinet | null>(null)
  const [sSel, setSSel] = useState<Shelf | null>(null)
  const [labelBox, setLabelBox] = useState<Box | null>(null)

  const loadBuildings = useCallback(async () => { try { setBuildings(await locations.buildings()) } catch { toast.error('تعذّر التحميل') } }, [toast])
  useEffect(() => { loadBuildings() }, [loadBuildings])
  useEffect(() => { if (bSel) locations.rooms(bSel.id).then(setRooms); else setRooms([]); setRSel(null) }, [bSel])
  useEffect(() => { if (rSel) locations.cabinets(rSel.id).then(setCabinets); else setCabinets([]); setCSel(null) }, [rSel])
  useEffect(() => { if (cSel) locations.shelves(cSel.id).then(setShelves); else setShelves([]); setSSel(null) }, [cSel])
  useEffect(() => { if (sSel) locations.boxes({ shelfId: sSel.id }).then(setBoxes); else setBoxes([]) }, [sSel])

  const reloadRooms = () => bSel && locations.rooms(bSel.id).then(setRooms)
  const reloadCabinets = () => rSel && locations.cabinets(rSel.id).then(setCabinets)
  const reloadShelves = () => cSel && locations.shelves(cSel.id).then(setShelves)
  const reloadBoxes = () => sSel && locations.boxes({ shelfId: sSel.id }).then(setBoxes)
  const del = (fn: () => Promise<unknown>, reload: () => void) => fn().then(() => { reload(); toast.success('تم الحذف') }).catch((e) => toast.error(errOf(e, 'تعذّر الحذف')))

  return (
    <div>
      <header className="page__head">
        <div><span className="kicker">الأرشيف المادي</span><h1>إدارة المواقع الفعلية</h1></div>
        <div className="page__headactions">
          {canEdit && <button className="btn btn-ghost" title="نقل المواقع من النظام القديم" onClick={() =>
            locations.migrateLegacy().then((r) => { toast.success(r.message); loadBuildings() }).catch((e) => toast.error(errOf(e, 'تعذّر الترحيل')))}>⇪ ترحيل القديمة</button>}
          <button className="btn btn-ghost" onClick={loadBuildings}>↻ تحديث</button>
        </div>
      </header>

      <p className="muted" style={{ marginBottom: '.8rem' }}>
        {[bSel && `🏢 ${bSel.nameAr}`, rSel && `🚪 ${rSel.nameAr}`, cSel && `🗄 ${cSel.nameAr}`, sSel && `📚 رف ${sSel.shelfNumber}`].filter(Boolean).join('  ›  ') || 'اختر مبنى للبدء'}
      </p>

      <CrudLevel<Building> title="المباني" items={buildings} selectedId={bSel?.id} canEdit={canEdit} canCreate={canCreate} canDelete={canDelete}
        fields={[{ key: 'nameAr', label: 'الاسم', required: true }, { key: 'code', label: 'الرمز', dir: 'ltr' }]}
        badge={(b) => `🏢 ${b.nameAr}${b.code ? ` · ${b.code}` : ''} · ${b.roomCount} غرفة`}
        onSelect={setBSel}
        onCreate={async (r) => { await locations.createBuilding(r); await loadBuildings() }}
        onUpdate={async (id, r) => { await locations.updateBuilding(id, r); await loadBuildings() }}
        onDelete={(b) => del(() => locations.deleteBuilding(b.id), loadBuildings)} />

      {bSel && (
        <CrudLevel<Room> title={`غرف: ${bSel.nameAr}`} items={rooms} selectedId={rSel?.id} canEdit={canEdit} canCreate={canCreate} canDelete={canDelete}
          fields={[{ key: 'nameAr', label: 'الاسم', required: true }, { key: 'roomNumber', label: 'الرقم', dir: 'ltr' }]}
          badge={(r) => `🚪 ${r.nameAr}${r.roomNumber ? ` · ${r.roomNumber}` : ''} · ${r.cabinetCount} خزانة`}
          onSelect={setRSel}
          onCreate={async (r) => { await locations.createRoom({ ...r, buildingId: bSel.id } as Partial<Room>); reloadRooms() }}
          onUpdate={async (id, r) => { await locations.updateRoom(id, { ...r, buildingId: bSel.id } as Partial<Room>); reloadRooms() }}
          onDelete={(r) => del(() => locations.deleteRoom(r.id), reloadRooms)} />
      )}

      {rSel && (
        <CrudLevel<Cabinet> title={`خزائن: ${rSel.nameAr}`} items={cabinets} selectedId={cSel?.id} canEdit={canEdit} canCreate={canCreate} canDelete={canDelete}
          fields={[{ key: 'nameAr', label: 'الاسم', required: true }, { key: 'cabinetCode', label: 'الرمز', dir: 'ltr' }]}
          badge={(c) => `🗄 ${c.nameAr}${c.cabinetCode ? ` · ${c.cabinetCode}` : ''} · ${c.shelvesActual} رف`}
          onSelect={setCSel}
          onCreate={async (r) => { await locations.createCabinet({ ...r, roomId: rSel.id } as Partial<Cabinet>); reloadCabinets() }}
          onUpdate={async (id, r) => { await locations.updateCabinet(id, { ...r, roomId: rSel.id } as Partial<Cabinet>); reloadCabinets() }}
          onDelete={(c) => del(() => locations.deleteCabinet(c.id), reloadCabinets)} />
      )}

      {cSel && (
        <CrudLevel<Shelf> title={`رفوف: ${cSel.nameAr}`} items={shelves} selectedId={sSel?.id} canEdit={canEdit} canCreate={canCreate} canDelete={canDelete}
          fields={[{ key: 'shelfNumber', label: 'رقم الرف', required: true, dir: 'ltr' }, { key: 'capacity', label: 'السعة', type: 'number', dir: 'ltr' }]}
          badge={(s) => `📚 رف ${s.shelfNumber} · ${s.boxCount} صندوق`}
          onSelect={setSSel}
          onCreate={async (r) => { await locations.createShelf({ ...r, cabinetId: cSel.id, capacity: r.capacity ? Number(r.capacity) : null } as Partial<Shelf>); reloadShelves() }}
          onUpdate={async (id, r) => { await locations.updateShelf(id, { ...r, cabinetId: cSel.id, capacity: r.capacity ? Number(r.capacity) : null } as Partial<Shelf>); reloadShelves() }}
          onDelete={(s) => del(() => locations.deleteShelf(s.id), reloadShelves)} />
      )}

      {sSel && (
        <CrudLevel<Box> title={`صناديق: رف ${sSel.shelfNumber}`} items={boxes} canEdit={canEdit} canCreate={canCreate} canDelete={canDelete}
          fields={[{ key: 'boxCode', label: 'رمز الصندوق', required: true, dir: 'ltr' }, { key: 'capacity', label: 'السعة', type: 'number', dir: 'ltr' }]}
          badge={(b) => `📦 ${b.boxCode} · ${b.currentCount}${b.capacity ? `/${b.capacity}` : ''}${b.isFull ? ' · ممتلئ' : ''}`}
          extra={(b) => <button className="btn btn-ghost btn-sm" title="بطاقة الصندوق" onClick={() => setLabelBox(b)}>🏷</button>}
          onCreate={async (r) => { await locations.createBox({ ...r, shelfId: sSel.id, capacity: r.capacity ? Number(r.capacity) : null } as Partial<Box>); reloadBoxes() }}
          onUpdate={async (id, r) => { await locations.updateBox(id, { ...r, shelfId: sSel.id, capacity: r.capacity ? Number(r.capacity) : null } as Partial<Box>); reloadBoxes() }}
          onDelete={(b) => del(() => locations.deleteBox(b.id), reloadBoxes)} />
      )}

      {labelBox && <BoxLabel box={labelBox} onClose={() => setLabelBox(null)} />}
    </div>
  )
}
