import { useEffect, useState, useCallback, useMemo, Fragment } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { AxiosError } from 'axios'
import { useTranslation } from 'react-i18next'
import { Eye, FolderOpen, Trash2, Copy, X } from 'lucide-react'
import { auth } from '../../lib/auth'
import { archive, type PhysicalArchiveItemDto } from '../../lib/archive'
import {
  ContextMenu, ContextMenuContent, ContextMenuItem, ContextMenuLabel,
  ContextMenuSeparator, ContextMenuTrigger,
} from '../../components/ui/context-menu'
import { useTableColumns } from '../../hooks/useTableColumns'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../components/ui/tabs'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../components/ui/card'
import LocationsPage from '../locations/LocationsPage'
import ArchiveByOriginView from './ArchiveByOriginView'
import ArchiveIntakeQueue from './ArchiveIntakeQueue'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { FilterSearchRow, FilterSidebar } from '../../components/ui/filter-sidebar'
import { LabeledCombobox } from '../../components/ui/labeled-input'
import {
  useExpandedRow, ExpandCell, ExpandHeadCell, ExpandedRow, DetailFields, DetailField, DetailActions,
} from '../../components/ui/expandable-row'
import '../../styles/forms.css'

const errOf = (err: unknown, fallback: string) =>
  (err as AxiosError<{ error?: string }>).response?.data?.error ?? fallback

// Location paths from the API are joined with a guillemet; legacy items are a single segment with
// no separator. Trim so the split tolerates the spacing ("A › B") the backend emits.
const SEP = '›'
const segsOf = (name?: string | null) =>
  (name ?? '').split(SEP).map((s) => s.trim()).filter(Boolean)

/** The levels of a location path, in the order the segments appear in it. One search field each. */
const LEVELS = ['مبنى', 'غرفة', 'خزانة', 'رف', 'صندوق'] as const
const NO_LEVELS: string[] = LEVELS.map(() => '')

/**
 * Does one segment of an item's path answer what was typed into that level's field?
 *
 * Text picked from the list is matched exactly — «رف 1» means that shelf, not every shelf whose
 * name contains it. Anything else is matched loosely, so half-typed text narrows the table
 * straight away instead of sitting there doing nothing until it is spelled out in full.
 */
function segMatches(seg: string | undefined, typed: string, options: string[]) {
  const q = typed.trim()
  if (!q) return true
  if (!seg) return false
  return options.includes(q) ? seg === q : seg.toLocaleLowerCase().includes(q.toLocaleLowerCase())
}

export default function ArchivePage() {
  const { t } = useTranslation()
  const { confirm } = useConfirmDialog()
  const navigate = useNavigate()
  const itemCols = useTableColumns('archiveDocuments')
  const { toggle, isExpanded } = useExpandedRow<string>()
  const copyNumber = (n: string) => navigator.clipboard.writeText(n).catch(() => {})
  const [items, setItems] = useState<PhysicalArchiveItemDto[]>([])
  // One entry per level of the hierarchy — مبنى، غرفة، خزانة، رف، صندوق — each independently
  // searchable. '' means that level is not being filtered on.
  const [levels, setLevels] = useState<string[]>(NO_LEVELS)
  const [itemSearch, setItemSearch] = useState('')
  const [showFilters, setShowFilters] = useState(false)
  const [error, setError] = useState('')

  const canArchive = auth.hasPermission('Archive.Archive')

  // Always load every item (no server-side location filter). Two reasons: the place filter runs
  // client-side so it can span both location systems at once, and the backend only returns the
  // boxed-hierarchy documents when no locationId is passed — filtering there would drop exactly
  // the rows this register is meant to show.
  const loadItems = useCallback(async () => {
    try { setItems(await archive.items()) }
    catch { setError(t('archive.loadError')) }
  }, [t])

  useEffect(() => { loadItems() }, [loadItems])

  /* What each level's field offers, narrowed by the levels above it: once a building is chosen the
     room field lists that building's rooms and nothing else, so «غرفة الأرشيف 1» — a name four
     different buildings use — stops being ambiguous. Options come from the rows themselves, so
     every suggestion offered returns something. */
  const levelOptions = useMemo(() => {
    const out: string[][] = []
    for (let i = 0; i < LEVELS.length; i++) {
      const set = new Set<string>()
      for (const it of items) {
        const segs = segsOf(it.locationName)
        if (segs.length <= i) continue
        let ok = true
        for (let j = 0; j < i && ok; j++) {
          const v = levels[j].trim()
          if (v && !segs[j]?.toLocaleLowerCase().includes(v.toLocaleLowerCase())) ok = false
        }
        if (ok) set.add(segs[i])
      }
      out.push([...set].sort((a, b) => a.localeCompare(b, 'ar')))
    }
    return out
  }, [items, levels])

  // An item is shown when every level being filtered on is answered by the matching segment of
  // its own path. Levels left blank say nothing, so filling in only «صندوق» finds that box
  // wherever it stands.
  const matchesPlace = useCallback((it: PhysicalArchiveItemDto) => {
    const segs = segsOf(it.locationName)
    return levels.every((v, i) => segMatches(segs[i], v, levelOptions[i] ?? []))
  }, [levels, levelOptions])

  const activePlace = levels.filter((v) => v.trim()).join(` ${SEP} `)

  /** Set one level. Everything below it is cleared: a shelf chosen under the old room almost
   *  never exists under the new one, and leaving it behind produced an empty table with no
   *  visible reason for it. */
  const setLevel = (i: number, value: string) =>
    setLevels((prev) => prev.map((v, j) => (j === i ? value : j > i ? '' : v)))

  const filteredItems = useMemo(() => {
    const s = itemSearch.trim().toLowerCase()
    return items.filter((it) => {
      if (!matchesPlace(it)) return false
      if (!s) return true
      return [it.documentNumber, it.boxNumber, it.fileNumber, it.documentTitle]
        .some((v) => v?.toLowerCase().includes(s))
    })
  }, [items, itemSearch, matchesPlace])

  // ---- Items ----
  async function deleteItem(it: PhysicalArchiveItemDto) {
    if (!(await confirm({ message: t('archive.confirmDeleteItem'), destructive: true }))) return
    setError('')
    try { await archive.deleteItem(it.id); await loadItems() }
    catch (err) { setError(errOf(err, t('archive.deleteItemError'))) }
  }

  return (
    <div>
      {error && <p className="login__error">{error}</p>}

      <Tabs defaultValue="tree" dir="rtl">
        <TabsList className="p-2">
          <TabsTrigger className="px-7 py-3 text-[17px]" value="tree">{t('archiveTree.tab')}</TabsTrigger>
          {canArchive && <TabsTrigger className="px-7 py-3 text-[17px]" value="intake">وارد الأرشفة</TabsTrigger>}
          <TabsTrigger className="px-7 py-3 text-[17px]" value="items">البنود المؤرشفة</TabsTrigger>
          {auth.hasPermission('Archive.View') && <TabsTrigger className="px-7 py-3 text-[17px]" value="locations">المواقع الفعلية</TabsTrigger>}
        </TabsList>

        {/* Browse the archive by origin: the tree sits permanently beside the documents it scopes. */}
        <TabsContent value="tree"><ArchiveByOriginView /></TabsContent>

        {/* What other departments have sent for filing. Only the people who may assign a shelf see
            it — for everyone else it would be a list they could do nothing with. */}
        {canArchive && <TabsContent value="intake"><ArchiveIntakeQueue /></TabsContent>}

        <TabsContent value="items">

      <Card>
        <CardHeader>
          <CardTitle>{t('archive.items')} ({filteredItems.length})</CardTitle>
          <CardDescription>الوثائق ذات الأصل الورقي المخزّنة في المواقع الفعلية، مع مواقعها وصناديقها.</CardDescription>
        </CardHeader>
        <CardContent>
          <FilterSearchRow
            search={itemSearch}
            onSearch={setItemSearch}
            placeholder={t('archive.searchItems')}
            showFilters={showFilters}
            onToggleFilters={() => setShowFilters((v) => !v)}
            activeCount={levels.filter((v) => v.trim()).length}
          >
            {/* The active place, spelled out beside the search box and removable in one click —
                otherwise a filter set earlier is invisible once the panel is closed, and a short
                table looks like an empty archive. */}
            {activePlace && (
              <button
                type="button"
                onClick={() => setLevels(NO_LEVELS)}
                title={`إزالة المرشح: ${activePlace}`}
                className="flex shrink-0 items-center gap-1.5 rounded-md border border-primary bg-primary/10 px-2.5 py-1.5 text-xs text-primary"
              >
                <span className="max-w-[260px] truncate">{activePlace}</span>
                <X className="h-3.5 w-3.5" />
              </button>
            )}
          </FilterSearchRow>

          <div className="mt-3 flex items-start gap-4">
            <FilterSidebar
              open={showFilters}
              panelLabel={t('common.filters')}
              clearLabel={t('common.clear')}
              hasActive={!!activePlace}
              onClear={() => setLevels(NO_LEVELS)}
              // Wider than the default: place names read as full paths elsewhere on this page,
              // and 260px cut them off at the room.
              width={320}
              // No chip groups — a searchable field per level replaces them. Every place in the
              // register was far too many chips to scan, and a chip list cannot be typed at.
              groups={[]}
              extra={
                <div className="space-y-3">
                  <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">الموقع</p>
                  {LEVELS.map((level, i) => (
                    <LabeledCombobox
                      key={level}
                      label={level}
                      options={levelOptions[i] ?? []}
                      value={levels[i]}
                      onValueChange={(v) => setLevel(i, v)}
                      emptyLabel="لا نتائج مطابقة"
                      // Only the first field needs telling how it works; repeating the hint under
                      // all five turned the panel into a wall of grey text.
                      hint={i === 0 ? 'اكتب للبحث أو اختر من القائمة' : undefined}
                    />
                  ))}
                  {activePlace && (
                    <button type="button" className="text-xs text-primary hover:underline"
                      onClick={() => setLevels(NO_LEVELS)}>
                      إظهار كل المواقع
                    </button>
                  )}
                </div>
              }
            />

            <div className="min-w-0 flex-1 overflow-x-auto">
          <table className="reg-table" style={{ marginTop: '.6rem' }}>
            <thead><tr>
              <ExpandHeadCell />
              {itemCols.columns.map((col) => <th key={col.key}>{col.label}</th>)}
            </tr></thead>
            <tbody>
              {filteredItems.length === 0 && <tr><td colSpan={itemCols.columns.length + 1} className="reg-empty">{t('archive.noItems')}</td></tr>}
              {filteredItems.map((it) => {
                const rowKey = `${it.source}-${it.id}-${it.documentId}`
                const open = isExpanded(rowKey)
                return (
                <Fragment key={rowKey}>
                <ContextMenu>
                  <ContextMenuTrigger asChild>
                    <tr
                      onClick={() => it.documentId && navigate(`/app/documents/${it.documentId}`)}
                      className="reg-row"
                      title="انقر للفتح · انقر بالزر الأيمن للإجراءات"
                    >
                      <ExpandCell expanded={open} onToggle={() => toggle(rowKey)} />
                      {itemCols.columns.map((col) => {
                        switch (col.key) {
                          case 'document': return (
                            <td key={col.key} className="mono">
                              {it.documentId
                                ? <Link to={`/app/documents/${it.documentId}`} title={it.documentTitle ?? ''} onClick={(e) => e.stopPropagation()}>{it.documentNumber ?? `#${it.documentId}`}</Link>
                                : '—'}
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
                    {/* 'Box' rows are the document's own BoxId, not an archive-item row — there is
                        nothing to delete here; unfile it from the document page instead. */}
                    {canArchive && it.source !== 'Box' && (
                      <>
                        <ContextMenuSeparator />
                        <ContextMenuItem onSelect={() => deleteItem(it)}>
                          <Trash2 className="ml-2 h-4 w-4 opacity-60" />{t('common.actions.delete')}
                        </ContextMenuItem>
                      </>
                    )}
                  </ContextMenuContent>
                </ContextMenu>

                <ExpandedRow colSpan={itemCols.columns.length + 1} expanded={open}>
                  <DetailFields>
                    <DetailField label={t('archive.columns.document', { defaultValue: 'الوثيقة' })}>
                      <span className="mono">{it.documentNumber ?? (it.documentId ? `#${it.documentId}` : '—')}</span>
                    </DetailField>
                    <DetailField label="العنوان">{it.documentTitle ?? '—'}</DetailField>
                    <DetailField label="الموقع">{it.locationName}</DetailField>
                    <DetailField label="الصندوق"><span className="mono">{it.boxNumber ?? '—'}</span></DetailField>
                    <DetailField label="رقم الملف"><span className="mono">{it.fileNumber ?? '—'}</span></DetailField>
                    <DetailField label="تاريخ الأرشفة"><span className="mono">{it.archivedAt?.slice(0, 10) ?? '—'}</span></DetailField>
                    <DetailField label="ملاحظات" wide>{it.notes ?? '—'}</DetailField>
                  </DetailFields>
                  <DetailActions>
                    {it.documentId && (
                      <Link to={`/app/documents/${it.documentId}`} className="btn btn-ghost btn-sm">فتح الوثيقة</Link>
                    )}
                    {it.documentNumber && (
                      <button type="button" className="btn btn-ghost btn-sm" onClick={() => copyNumber(it.documentNumber!)}>نسخ الرقم</button>
                    )}
                    {canArchive && it.source !== 'Box' && (
                      <button type="button" className="btn btn-ghost btn-sm text-destructive" onClick={() => deleteItem(it)}>
                        {t('common.actions.delete')}
                      </button>
                    )}
                  </DetailActions>
                </ExpandedRow>
                </Fragment>
              )})}
            </tbody>
          </table>
            </div>
          </div>
        </CardContent>
      </Card>
        </TabsContent>
        {auth.hasPermission('Archive.View') && (
          <TabsContent value="locations"><LocationsPage /></TabsContent>
        )}
      </Tabs>
    </div>
  )
}
