import { useEffect, useState, useCallback, useMemo } from 'react'
import { Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { auth } from '../../lib/auth'
import {
  archive, type PhysicalLocationDto, type PhysicalArchiveItemDto, type LocationType,
  LOCATION_TYPE_LABELS,
} from '../../lib/archive'
import '../incoming/incoming.css'

const TYPE_NAMES = ['Building', 'Room', 'Cabinet', 'Shelf', 'Box']
const locTypeLabel = (type: string) => LOCATION_TYPE_LABELS[TYPE_NAMES.indexOf(type)] ?? type
const errOf = (err: unknown, fallback: string) =>
  (err as AxiosError<{ error?: string }>).response?.data?.error ?? fallback

export default function ArchivePage() {
  const [locations, setLocations] = useState<PhysicalLocationDto[]>([])
  const [items, setItems] = useState<PhysicalArchiveItemDto[]>([])
  const [selected, setSelected] = useState<number | ''>('')
  const [itemSearch, setItemSearch] = useState('')
  const [error, setError] = useState('')

  const [loc, setLoc] = useState({ name: '', type: 0 as LocationType, code: '', parentId: '', isActive: true })
  const [locEditId, setLocEditId] = useState<number | null>(null)
  const [item, setItem] = useState({ documentId: '', physicalLocationId: '', boxNumber: '', fileNumber: '', notes: '' })
  const [itemEditId, setItemEditId] = useState<number | null>(null)

  const canCreate = auth.hasPermission('Archive.Create')
  const canArchive = auth.hasPermission('Archive.Archive')

  const loadLocations = useCallback(async () => {
    try { setLocations(await archive.locations()) } catch { setError('تعذّر تحميل المواقع') }
  }, [])
  const loadItems = useCallback(async () => {
    try { setItems(await archive.items(selected === '' ? undefined : Number(selected))) }
    catch { setError('تعذّر تحميل البنود المؤرشفة') }
  }, [selected])

  useEffect(() => { loadLocations() }, [loadLocations])
  useEffect(() => { loadItems() }, [loadItems])

  const filteredItems = useMemo(() => {
    const s = itemSearch.trim().toLowerCase()
    if (!s) return items
    return items.filter((it) => [it.documentNumber, it.boxNumber, it.fileNumber, it.documentTitle]
      .some((v) => v?.toLowerCase().includes(s)))
  }, [items, itemSearch])

  // ---- Locations ----
  function resetLoc() { setLoc({ name: '', type: 0, code: '', parentId: '', isActive: true }); setLocEditId(null) }
  function editLocation(l: PhysicalLocationDto) {
    setLocEditId(l.id)
    setLoc({ name: l.name, type: Math.max(0, TYPE_NAMES.indexOf(l.type)) as LocationType, code: l.code ?? '', parentId: l.parentId ? String(l.parentId) : '', isActive: l.isActive })
  }
  async function submitLocation(e: React.FormEvent) {
    e.preventDefault(); setError('')
    if (!loc.name) { setError('اسم الموقع مطلوب'); return }
    const body = { name: loc.name, type: Number(loc.type) as LocationType, code: loc.code || null, parentId: loc.parentId ? Number(loc.parentId) : null, isActive: loc.isActive }
    try {
      if (locEditId) await archive.updateLocation(locEditId, body)
      else await archive.createLocation(body)
      resetLoc(); await loadLocations(); await loadItems()
    } catch (err) { setError(errOf(err, 'تعذّر حفظ الموقع')) }
  }
  async function deleteLocation(l: PhysicalLocationDto) {
    if (!window.confirm(`حذف الموقع «${l.name}»؟`)) return
    setError('')
    try { await archive.deleteLocation(l.id); if (locEditId === l.id) resetLoc(); await loadLocations() }
    catch (err) { setError(errOf(err, 'تعذّر حذف الموقع')) }
  }

  // ---- Items ----
  function resetItem() { setItem({ documentId: '', physicalLocationId: '', boxNumber: '', fileNumber: '', notes: '' }); setItemEditId(null) }
  function editItem(it: PhysicalArchiveItemDto) {
    setItemEditId(it.id)
    setItem({ documentId: it.documentId ? String(it.documentId) : '', physicalLocationId: String(it.physicalLocationId), boxNumber: it.boxNumber ?? '', fileNumber: it.fileNumber ?? '', notes: it.notes ?? '' })
  }
  async function submitItem(e: React.FormEvent) {
    e.preventDefault(); setError('')
    if (!item.physicalLocationId || (!itemEditId && !item.documentId)) { setError('الموقع ورقم الوثيقة مطلوبان'); return }
    try {
      if (itemEditId) {
        await archive.updateItem(itemEditId, { physicalLocationId: Number(item.physicalLocationId), boxNumber: item.boxNumber || null, fileNumber: item.fileNumber || null, notes: item.notes || null })
      } else {
        await archive.createItem({ documentId: Number(item.documentId), physicalLocationId: Number(item.physicalLocationId), boxNumber: item.boxNumber || null, fileNumber: item.fileNumber || null, notes: item.notes || null })
      }
      resetItem(); await loadItems()
    } catch (err) { setError(errOf(err, 'تعذّر حفظ البند')) }
  }
  async function deleteItem(it: PhysicalArchiveItemDto) {
    if (!window.confirm('حذف هذا البند من الأرشيف الورقي؟')) return
    setError('')
    try { await archive.deleteItem(it.id); if (itemEditId === it.id) resetItem(); await loadItems() }
    catch (err) { setError(errOf(err, 'تعذّر حذف البند')) }
  }

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">ARCHIVE · الأرشيف الفيزيائي</span>
          <h1>الأرشيف الورقي</h1>
        </div>
      </header>

      {error && <p className="login__error">{error}</p>}

      <div className="detail-grid">
        {/* Locations */}
        <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
          <h3 className="detail-h3">المواقع ({locations.length})</h3>
          <table className="reg-table">
            <thead><tr><th>الاسم</th><th>النوع</th><th>الرمز</th><th>الحالة</th>{canCreate && <th></th>}</tr></thead>
            <tbody>
              {locations.length === 0 && <tr><td colSpan={5} className="reg-empty">لا توجد مواقع</td></tr>}
              {locations.map((l) => (
                <tr key={l.id} className={locEditId === l.id ? 'reg-row is-editing' : ''}>
                  <td>{l.name}</td>
                  <td>{locTypeLabel(l.type)}</td>
                  <td className="mono">{l.code ?? '—'}</td>
                  <td>{l.isActive ? <span className="badge internal">مفعّل</span> : <span className="badge">معطّل</span>}</td>
                  {canCreate && (
                    <td className="row-actions">
                      <button className="btn btn-ghost btn-sm" title="تعديل" onClick={() => editLocation(l)}>✏️</button>
                      <button className="btn btn-ghost btn-sm" title="حذف" onClick={() => deleteLocation(l)}>🗑</button>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>

          {canCreate && (
            <form className="form-grid" onSubmit={submitLocation} style={{ marginTop: '1rem' }}>
              <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{locEditId ? 'تعديل موقع' : 'إضافة موقع'}</h4>
              <label className="field"><span>اسم الموقع *</span>
                <input value={loc.name} onChange={(e) => setLoc({ ...loc, name: e.target.value })} /></label>
              <label className="field"><span>النوع</span>
                <select value={loc.type} onChange={(e) => setLoc({ ...loc, type: Number(e.target.value) as LocationType })}>
                  {LOCATION_TYPE_LABELS.map((t, i) => <option key={i} value={i}>{t}</option>)}
                </select></label>
              <label className="field"><span>الرمز</span>
                <input value={loc.code} onChange={(e) => setLoc({ ...loc, code: e.target.value })} dir="ltr" /></label>
              <label className="field"><span>الموقع الأب</span>
                <select value={loc.parentId} onChange={(e) => setLoc({ ...loc, parentId: e.target.value })}>
                  <option value="">— بدون —</option>
                  {locations.filter((l) => l.id !== locEditId).map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
                </select></label>
              {locEditId && (
                <label className="field"><span>الحالة</span>
                  <select value={loc.isActive ? '1' : '0'} onChange={(e) => setLoc({ ...loc, isActive: e.target.value === '1' })}>
                    <option value="1">مفعّل</option><option value="0">معطّل</option>
                  </select></label>
              )}
              <div className="form-actions">
                <button className="btn btn-primary">{locEditId ? 'حفظ' : '+ إضافة موقع'}</button>
                {locEditId && <button type="button" className="btn btn-ghost" onClick={resetLoc}>إلغاء</button>}
              </div>
            </form>
          )}
        </motion.section>

        {/* Archived items */}
        <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.08 }}>
          <h3 className="detail-h3">البنود المؤرشفة ({filteredItems.length})</h3>
          <div className="filters" style={{ gap: '.5rem' }}>
            <input className="filters__search" placeholder="بحث برقم الوثيقة، الصندوق، أو الملف…"
              value={itemSearch} onChange={(e) => setItemSearch(e.target.value)} />
            <select className="filters__status" value={selected} onChange={(e) => setSelected(e.target.value === '' ? '' : Number(e.target.value))}>
              <option value="">كل المواقع</option>
              {locations.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
            </select>
          </div>
          <table className="reg-table" style={{ marginTop: '.6rem' }}>
            <thead><tr><th>الوثيقة</th><th>الموقع</th><th>صندوق</th><th>ملف</th>{canArchive && <th></th>}</tr></thead>
            <tbody>
              {filteredItems.length === 0 && <tr><td colSpan={5} className="reg-empty">لا توجد بنود</td></tr>}
              {filteredItems.map((it) => (
                <tr key={it.id} className={itemEditId === it.id ? 'reg-row is-editing' : ''}>
                  <td className="mono">
                    {it.documentId
                      ? <Link to={`/app/documents/${it.documentId}`} title={it.documentTitle ?? ''}>{it.documentNumber ?? `وثيقة #${it.documentId}`}</Link>
                      : it.incomingMailId ? `وارد #${it.incomingMailId}` : '—'}
                  </td>
                  <td>{it.locationName}</td>
                  <td className="mono">{it.boxNumber ?? '—'}</td>
                  <td className="mono">{it.fileNumber ?? '—'}</td>
                  {canArchive && (
                    <td className="row-actions">
                      <button className="btn btn-ghost btn-sm" title="تعديل/نقل" onClick={() => editItem(it)}>✏️</button>
                      <button className="btn btn-ghost btn-sm" title="حذف" onClick={() => deleteItem(it)}>🗑</button>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>

          {canArchive && (
            <form className="form-grid" onSubmit={submitItem} style={{ marginTop: '1rem' }}>
              <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{itemEditId ? 'تعديل / نقل بند' : 'أرشفة بند'}</h4>
              <label className="field"><span>رقم الوثيقة *</span>
                <input value={item.documentId} disabled={!!itemEditId} onChange={(e) => setItem({ ...item, documentId: e.target.value })} dir="ltr" type="number" /></label>
              <label className="field"><span>الموقع *</span>
                <select value={item.physicalLocationId} onChange={(e) => setItem({ ...item, physicalLocationId: e.target.value })}>
                  <option value="">— اختر —</option>
                  {locations.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
                </select></label>
              <label className="field"><span>رقم الصندوق</span>
                <input value={item.boxNumber} onChange={(e) => setItem({ ...item, boxNumber: e.target.value })} dir="ltr" /></label>
              <label className="field"><span>رقم الملف</span>
                <input value={item.fileNumber} onChange={(e) => setItem({ ...item, fileNumber: e.target.value })} dir="ltr" /></label>
              <label className="field field--wide"><span>ملاحظات</span>
                <input value={item.notes} onChange={(e) => setItem({ ...item, notes: e.target.value })} /></label>
              <div className="form-actions">
                <button className="btn btn-seal">{itemEditId ? 'حفظ' : 'أرشفة بند'}</button>
                {itemEditId && <button type="button" className="btn btn-ghost" onClick={resetItem}>إلغاء</button>}
              </div>
            </form>
          )}
        </motion.section>
      </div>
    </div>
  )
}
