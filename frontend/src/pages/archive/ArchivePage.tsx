import { useEffect, useState, useCallback } from 'react'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { useTranslation } from 'react-i18next'
import { auth } from '../../lib/auth'
import {
  archive, type PhysicalLocationDto, type PhysicalArchiveItemDto, type LocationType,
  LOCATION_TYPE_LABELS,
} from '../../lib/archive'
import '../incoming/incoming.css'

export default function ArchivePage() {
  const { t } = useTranslation()
  const [locations, setLocations] = useState<PhysicalLocationDto[]>([])
  const [items, setItems] = useState<PhysicalArchiveItemDto[]>([])
  const [selected, setSelected] = useState<number | ''>('')
  const [error, setError] = useState('')

  const [loc, setLoc] = useState({ name: '', type: 0 as LocationType, code: '', parentId: '' })
  const [item, setItem] = useState({ documentId: '', physicalLocationId: '', boxNumber: '', fileNumber: '', notes: '' })

  const canCreate = auth.hasPermission('Archive.Create')
  const canArchive = auth.hasPermission('Archive.Archive')

  const loadLocations = useCallback(async () => {
    try { setLocations(await archive.locations()) }
    catch { setError(t('archive.loadError')) }
  }, [t])

  const loadItems = useCallback(async () => {
    try { setItems(await archive.items(selected === '' ? undefined : Number(selected))) }
    catch { setError(t('archive.loadError')) }
  }, [selected, t])

  useEffect(() => { loadLocations() }, [loadLocations])
  useEffect(() => { loadItems() }, [loadItems])

  async function addLocation(e: React.FormEvent) {
    e.preventDefault(); setError('')
    if (!loc.name) { setError(t('archive.fields.name') + ' ' + t('common.required')); return }
    try {
      await archive.createLocation({
        name: loc.name, type: Number(loc.type) as LocationType,
        code: loc.code || null, parentId: loc.parentId ? Number(loc.parentId) : null,
      })
      setLoc({ name: '', type: 0, code: '', parentId: '' })
      await loadLocations()
    } catch (err) { setError((err as AxiosError<{ error?: string }>).response?.data?.error ?? t('archive.loadError')) }
  }

  async function addItem(e: React.FormEvent) {
    e.preventDefault(); setError('')
    if (!item.physicalLocationId || !item.documentId) { setError(t('common.required')); return }
    try {
      await archive.createItem({
        documentId: Number(item.documentId),
        physicalLocationId: Number(item.physicalLocationId),
        boxNumber: item.boxNumber || null, fileNumber: item.fileNumber || null, notes: item.notes || null,
      })
      setItem({ documentId: '', physicalLocationId: '', boxNumber: '', fileNumber: '', notes: '' })
      await loadItems()
    } catch (err) { setError((err as AxiosError<{ error?: string }>).response?.data?.error ?? t('archive.loadError')) }
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
        <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
          <h3 className="detail-h3">{t('archive.title')} ({locations.length})</h3>
          <table className="reg-table">
            <thead><tr>
              <th>{t('archive.fields.name')}</th>
              <th>{t('archive.fields.type')}</th>
              <th>{t('archive.fields.code')}</th>
            </tr></thead>
            <tbody>
              {locations.length === 0 && <tr><td colSpan={3} className="reg-empty">{t('archive.empty')}</td></tr>}
              {locations.map((l) => (
                <tr key={l.id}>
                  <td>{l.name}</td>
                  <td>{LOCATION_TYPE_LABELS[['Building','Room','Cabinet','Shelf','Box'].indexOf(l.type)] ?? l.type}</td>
                  <td className="mono">{l.code ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>

          {canCreate && (
            <form className="form-grid" onSubmit={addLocation} style={{ marginTop: '1rem' }}>
              <label className="field"><span>{t('archive.fields.name')} *</span>
                <input value={loc.name} onChange={(e) => setLoc({ ...loc, name: e.target.value })} /></label>
              <label className="field"><span>{t('archive.fields.type')}</span>
                <select value={loc.type} onChange={(e) => setLoc({ ...loc, type: Number(e.target.value) as LocationType })}>
                  {LOCATION_TYPE_LABELS.map((lbl, i) => <option key={i} value={i}>{lbl}</option>)}
                </select></label>
              <label className="field"><span>{t('archive.fields.code')}</span>
                <input value={loc.code} onChange={(e) => setLoc({ ...loc, code: e.target.value })} dir="ltr" /></label>
              <label className="field"><span>—</span>
                <select value={loc.parentId} onChange={(e) => setLoc({ ...loc, parentId: e.target.value })}>
                  <option value="">—</option>
                  {locations.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
                </select></label>
              <div className="form-actions"><button className="btn btn-primary">+ {t('archive.addLocation')}</button></div>
            </form>
          )}
        </motion.section>

        <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.08 }}>
          <h3 className="detail-h3">{t('nav.archive')}</h3>
          <select className="filters__status" value={selected} onChange={(e) => setSelected(e.target.value === '' ? '' : Number(e.target.value))}>
            <option value="">—</option>
            {locations.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
          </select>
          <table className="reg-table" style={{ marginTop: '.6rem' }}>
            <thead><tr>
              <th>{t('documents.title')}</th>
              <th>{t('archive.title')}</th>
              <th>{t('archive.fields.code')}</th>
              <th>—</th>
            </tr></thead>
            <tbody>
              {items.length === 0 && <tr><td colSpan={4} className="reg-empty">{t('archive.empty')}</td></tr>}
              {items.map((it) => (
                <tr key={it.id}>
                  <td className="mono">{it.documentId ? `#${it.documentId}` : it.incomingMailId ? `#${it.incomingMailId}` : '—'}</td>
                  <td>{it.locationName}</td>
                  <td className="mono">{it.boxNumber ?? '—'}</td>
                  <td className="mono">{it.fileNumber ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>

          {canArchive && (
            <form className="form-grid" onSubmit={addItem} style={{ marginTop: '1rem' }}>
              <label className="field"><span>{t('documents.title')} *</span>
                <input value={item.documentId} onChange={(e) => setItem({ ...item, documentId: e.target.value })} dir="ltr" type="number" /></label>
              <label className="field"><span>{t('archive.title')} *</span>
                <select value={item.physicalLocationId} onChange={(e) => setItem({ ...item, physicalLocationId: e.target.value })}>
                  <option value="">—</option>
                  {locations.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
                </select></label>
              <label className="field"><span>{t('archive.fields.code')}</span>
                <input value={item.boxNumber} onChange={(e) => setItem({ ...item, boxNumber: e.target.value })} dir="ltr" /></label>
              <label className="field"><span>—</span>
                <input value={item.fileNumber} onChange={(e) => setItem({ ...item, fileNumber: e.target.value })} dir="ltr" /></label>
              <div className="form-actions"><button className="btn btn-seal">{t('nav.archive')}</button></div>
            </form>
          )}
        </motion.section>
      </div>
    </div>
  )
}
