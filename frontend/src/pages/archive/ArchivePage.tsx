import { useEffect, useState, useCallback, useMemo } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { useTranslation } from 'react-i18next'
import { Eye, FolderOpen, Trash2, Copy } from 'lucide-react'
import { auth } from '../../lib/auth'
import { archive, type PhysicalLocationDto, type PhysicalArchiveItemDto } from '../../lib/archive'
import {
  ContextMenu, ContextMenuContent, ContextMenuItem, ContextMenuLabel,
  ContextMenuSeparator, ContextMenuTrigger,
} from '../../components/ui/context-menu'
import { useTableColumns } from '../../hooks/useTableColumns'
import '../incoming/incoming.css'

const errOf = (err: unknown, fallback: string) =>
  (err as AxiosError<{ error?: string }>).response?.data?.error ?? fallback

export default function ArchivePage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const itemCols = useTableColumns('archiveDocuments')
  const copyNumber = (n: string) => navigator.clipboard.writeText(n).catch(() => {})
  const [locations, setLocations] = useState<PhysicalLocationDto[]>([])
  const [items, setItems] = useState<PhysicalArchiveItemDto[]>([])
  const [selected, setSelected] = useState<number | ''>('')
  const [itemSearch, setItemSearch] = useState('')
  const [error, setError] = useState('')

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

  // ---- Items ----
  async function deleteItem(it: PhysicalArchiveItemDto) {
    if (!window.confirm(t('archive.confirmDeleteItem'))) return
    setError('')
    try { await archive.deleteItem(it.id); await loadItems() }
    catch (err) { setError(errOf(err, t('archive.deleteItemError'))) }
  }

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">{t('archive.kicker')}</span>
          <h1>{t('archive.title')}</h1>
        </div>
        {auth.hasPermission('Documents.Create') && (
          <Link to="/app/documents/new" className="btn btn-primary">{t('documents.newButton')}</Link>
        )}
      </header>

      {error && <p className="login__error">{error}</p>}

      <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
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
              {itemCols.columns.map((col) => <th key={col.key}>{col.label}</th>)}
            </tr></thead>
            <tbody>
              {filteredItems.length === 0 && <tr><td colSpan={itemCols.columns.length} className="reg-empty">{t('archive.noItems')}</td></tr>}
              {filteredItems.map((it) => (
                <ContextMenu key={it.id}>
                  <ContextMenuTrigger asChild>
                    <tr
                      onClick={() => it.documentId && navigate(`/app/documents/${it.documentId}`)}
                      className="reg-row"
                      title="انقر للفتح · انقر بالزر الأيمن للإجراءات"
                    >
                      {itemCols.columns.map((col) => {
                        switch (col.key) {
                          case 'document': return (
                            <td key={col.key} className="mono">
                              {it.documentId
                                ? <Link to={`/app/documents/${it.documentId}`} title={it.documentTitle ?? ''} onClick={(e) => e.stopPropagation()}>{it.documentNumber ?? `#${it.documentId}`}</Link>
                                : it.incomingMailId ? `${t('archive.incoming')} #${it.incomingMailId}` : '—'}
                            </td>
                          )
                          case 'location': return <td key={col.key}>{it.locationName}</td>
                          case 'box': return <td key={col.key} className="mono">{it.boxNumber ?? '—'}</td>
                          case 'file': return <td key={col.key} className="mono">{it.fileNumber ?? '—'}</td>
                          case 'title': return <td key={col.key}>{it.documentTitle ?? '—'}</td>
                          case 'archivedAt': return <td key={col.key} className="mono">{it.archivedAt?.slice(0, 10) ?? '—'}</td>
                          case 'notes': return <td key={col.key}>{it.notes ?? '—'}</td>
                          default: return null
                        }
                      })}
                    </tr>
                  </ContextMenuTrigger>

                  <ContextMenuContent className="w-56">
                    <ContextMenuLabel className="truncate text-xs text-muted-foreground max-w-[200px]">
                      {it.documentNumber ?? it.locationName}
                    </ContextMenuLabel>
                    <ContextMenuSeparator />
                    {it.documentId && (
                      <>
                        <ContextMenuItem onSelect={() => navigate(`/app/documents/${it.documentId}`)}>
                          <Eye className="ml-2 h-4 w-4 opacity-60" />فتح الوثيقة
                        </ContextMenuItem>
                        <ContextMenuItem onSelect={() => window.open(`/app/documents/${it.documentId}`, '_blank')}>
                          <FolderOpen className="ml-2 h-4 w-4 opacity-60" />فتح في نافذة جديدة
                        </ContextMenuItem>
                        {it.documentNumber && (
                          <ContextMenuItem onSelect={() => copyNumber(it.documentNumber!)}>
                            <Copy className="ml-2 h-4 w-4 opacity-60" />نسخ رقم الوثيقة
                          </ContextMenuItem>
                        )}
                      </>
                    )}
                    {canArchive && (
                      <>
                        <ContextMenuSeparator />
                        <ContextMenuItem onSelect={() => deleteItem(it)}>
                          <Trash2 className="ml-2 h-4 w-4 opacity-60" />{t('common.actions.delete')}
                        </ContextMenuItem>
                      </>
                    )}
                  </ContextMenuContent>
                </ContextMenu>
              ))}
            </tbody>
          </table>
      </motion.section>
    </div>
  )
}
