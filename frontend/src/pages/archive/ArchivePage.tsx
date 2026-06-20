import { useEffect, useState, useCallback, useMemo } from 'react'
import { Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { useTranslation } from 'react-i18next'
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
  const { t } = useTranslation()
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
    try { setLocations(await archive.locations()) } catch { setError(t('archive.loadError')) }
  }, [t])
  const loadItems = useCallback(async () => {
    try { setItems(await archive.items(selected === '' ? undefined : Number(selected))) }
    catch { setError(t('archive.loadError')) }
  }, [selected, t])

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
    if (!loc.name) { setError(t('archive.nameRequired')); return }
    const body = { name: loc.name, type: Number(loc.type) as LocationType, code: loc.code || null, parentId: loc.parentId ? Number(loc.parentId) : null, isActive: loc.isActive }
    try {
      if (locEditId) await archive.updateLocation(locEditId, body)
      else await archive.createLocation(body)
      resetLoc(); await loadLocations(); await loadItems()
    } catch (err) { setError(errOf(err, t('archive.saveLocationError'))) }
  }
  async function deleteLocation(l: PhysicalLocationDto) {
    if (!window.confirm(t('archive.confirmDeleteLocation', { name: l.name }))) return
    setError('')
    try { await archive.deleteLocation(l.id); if (locEditId === l.id) resetLoc(); await loadLocations() }
    catch (err) { setError(errOf(err, t('archive.deleteLocationError'))) }
  }

  // ---- Items ----
  function resetItem() { setItem({ documentId: '', physicalLocationId: '', boxNumber: '', fileNumber: '', notes: '' }); setItemEditId(null) }
  function editItem(it: PhysicalArchiveItemDto) {
    setItemEditId(it.id)
    setItem({ documentId: it.documentId ? String(it.documentId) : '', physicalLocationId: String(it.physicalLocationId), boxNumber: it.boxNumber ?? '', fileNumber: it.fileNumber ?? '', notes: it.notes ?? '' })
  }
  async function submitItem(e: React.FormEvent) {
    e.preventDefault(); setError('')
    if (!item.physicalLocationId || (!itemEditId && !item.documentId)) { setError(t('archive.locItemRequired')); return }
    try {
      if (itemEditId) {
        await archive.updateItem(itemEditId, { physicalLocationId: Number(item.physicalLocationId), boxNumber: item.boxNumber || null, fileNumber: item.fileNumber || null, notes: item.notes || null })
      } else {
        await archive.createItem({ documentId: Number(item.documentId), physicalLocationId: Number(item.physicalLocationId), boxNumber: item.boxNumber || null, fileNumber: item.fileNumber || null, notes: item.notes || null })
      }
      resetItem(); await loadItems()
    } catch (err) { setError(errOf(err, t('archive.saveItemError'))) }
  }
  async function deleteItem(it: PhysicalArchiveItemDto) {
    if (!window.confirm(t('archive.confirmDeleteItem'))) return
    setError('')
    try { await archive.deleteItem(it.id); if (itemEditId === it.id) resetItem(); await loadItems() }
    catch (err) { setError(errOf(err, t('archive.deleteItemError'))) }
  }

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">{t('archive.kicker')}</span>
          <h1>{t('archive.title')}</h1>
        </div>
      </header>

      {error && <p className="login__error">{error}</p>}

      <div className="detail-grid">
        {/* Locations */}
        <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
          <h3 className="detail-h3">{t('archive.locations')} ({locations.length})</h3>
          <table className="reg-table">
            <thead><tr>
              <th>{t('archive.columns.name')}</th><th>{t('archive.columns.type')}</th>
              <th>{t('archive.columns.code')}</th><th>{t('archive.columns.status')}</th>{canCreate && <th></th>}
            </tr></thead>
            <tbody>
              {locations.length === 0 && <tr><td colSpan={5} className="reg-empty">{t('archive.noLocations')}</td></tr>}
              {locations.map((l) => (
                <tr key={l.id} className={locEditId === l.id ? 'reg-row is-editing' : ''}>
                  <td>{l.name}</td>
                  <td>{locTypeLabel(l.type)}</td>
                  <td className="mono">{l.code ?? '—'}</td>
                  <td>{l.isActive ? <span className="badge internal">{t('archive.active')}</span> : <span className="badge">{t('archive.inactive')}</span>}</td>
                  {canCreate && (
                    <td className="row-actions">
                      <button className="btn btn-ghost btn-sm" title={t('common.actions.edit')} onClick={() => editLocation(l)}>✏️</button>
                      <button className="btn btn-ghost btn-sm" title={t('common.actions.delete')} onClick={() => deleteLocation(l)}>🗑</button>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>

          {canCreate && (
            <form className="form-grid" onSubmit={submitLocation} style={{ marginTop: '1rem' }}>
              <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{locEditId ? t('archive.editLocationTitle') : t('archive.addLocationTitle')}</h4>
              <label className="field"><span>{t('archive.fields.name')} *</span>
                <input value={loc.name} onChange={(e) => setLoc({ ...loc, name: e.target.value })} /></label>
              <label className="field"><span>{t('archive.fields.type')}</span>
                <select value={loc.type} onChange={(e) => setLoc({ ...loc, type: Number(e.target.value) as LocationType })}>
                  {LOCATION_TYPE_LABELS.map((tl, i) => <option key={i} value={i}>{tl}</option>)}
                </select></label>
              <label className="field"><span>{t('archive.fields.code')}</span>
                <input value={loc.code} onChange={(e) => setLoc({ ...loc, code: e.target.value })} dir="ltr" /></label>
              <label className="field"><span>{t('archive.fields.parent')}</span>
                <select value={loc.parentId} onChange={(e) => setLoc({ ...loc, parentId: e.target.value })}>
                  <option value="">{t('archive.none')}</option>
                  {locations.filter((l) => l.id !== locEditId).map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
                </select></label>
              {locEditId && (
                <label className="field"><span>{t('archive.fields.status')}</span>
                  <select value={loc.isActive ? '1' : '0'} onChange={(e) => setLoc({ ...loc, isActive: e.target.value === '1' })}>
                    <option value="1">{t('archive.active')}</option><option value="0">{t('archive.inactive')}</option>
                  </select></label>
              )}
              <div className="form-actions">
                <button className="btn btn-primary">{locEditId ? t('common.actions.save') : t('archive.addLocationBtn')}</button>
                {locEditId && <button type="button" className="btn btn-ghost" onClick={resetLoc}>{t('common.actions.cancel')}</button>}
              </div>
            </form>
          )}
        </motion.section>

        {/* Archived items */}
        <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.08 }}>
          <h3 className="detail-h3">{t('archive.items')} ({filteredItems.length})</h3>
          <div className="filters" style={{ gap: '.5rem' }}>
            <input className="filters__search" placeholder={t('archive.searchItems')}
              value={itemSearch} onChange={(e) => setItemSearch(e.target.value)} />
            <select className="filters__status" value={selected} onChange={(e) => setSelected(e.target.value === '' ? '' : Number(e.target.value))}>
              <option value="">{t('archive.allLocations')}</option>
              {locations.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
            </select>
          </div>
          <table className="reg-table" style={{ marginTop: '.6rem' }}>
            <thead><tr>
              <th>{t('archive.columns.document')}</th><th>{t('archive.columns.location')}</th>
              <th>{t('archive.columns.box')}</th><th>{t('archive.columns.file')}</th>{canArchive && <th></th>}
            </tr></thead>
            <tbody>
              {filteredItems.length === 0 && <tr><td colSpan={5} className="reg-empty">{t('archive.noItems')}</td></tr>}
              {filteredItems.map((it) => (
                <tr key={it.id} className={itemEditId === it.id ? 'reg-row is-editing' : ''}>
                  <td className="mono">
                    {it.documentId
                      ? <Link to={`/app/documents/${it.documentId}`} title={it.documentTitle ?? ''}>{it.documentNumber ?? `#${it.documentId}`}</Link>
                      : it.incomingMailId ? `${t('archive.incoming')} #${it.incomingMailId}` : '—'}
                  </td>
                  <td>{it.locationName}</td>
                  <td className="mono">{it.boxNumber ?? '—'}</td>
                  <td className="mono">{it.fileNumber ?? '—'}</td>
                  {canArchive && (
                    <td className="row-actions">
                      <button className="btn btn-ghost btn-sm" title={t('common.actions.edit')} onClick={() => editItem(it)}>✏️</button>
                      <button className="btn btn-ghost btn-sm" title={t('common.actions.delete')} onClick={() => deleteItem(it)}>🗑</button>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>

          {canArchive && (
            <form className="form-grid" onSubmit={submitItem} style={{ marginTop: '1rem' }}>
              <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{itemEditId ? t('archive.editItemTitle') : t('archive.addItemTitle')}</h4>
              <label className="field"><span>{t('archive.fields.document')} *</span>
                <input value={item.documentId} disabled={!!itemEditId} onChange={(e) => setItem({ ...item, documentId: e.target.value })} dir="ltr" type="number" /></label>
              <label className="field"><span>{t('archive.fields.location')} *</span>
                <select value={item.physicalLocationId} onChange={(e) => setItem({ ...item, physicalLocationId: e.target.value })}>
                  <option value="">{t('archive.select')}</option>
                  {locations.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
                </select></label>
              <label className="field"><span>{t('archive.fields.box')}</span>
                <input value={item.boxNumber} onChange={(e) => setItem({ ...item, boxNumber: e.target.value })} dir="ltr" /></label>
              <label className="field"><span>{t('archive.fields.file')}</span>
                <input value={item.fileNumber} onChange={(e) => setItem({ ...item, fileNumber: e.target.value })} dir="ltr" /></label>
              <label className="field field--wide"><span>{t('archive.fields.notes')}</span>
                <input value={item.notes} onChange={(e) => setItem({ ...item, notes: e.target.value })} /></label>
              <div className="form-actions">
                <button className="btn btn-seal">{itemEditId ? t('common.actions.save') : t('archive.archiveItem')}</button>
                {itemEditId && <button type="button" className="btn btn-ghost" onClick={resetItem}>{t('common.actions.cancel')}</button>}
              </div>
            </form>
          )}
        </motion.section>
      </div>
    </div>
  )
}
