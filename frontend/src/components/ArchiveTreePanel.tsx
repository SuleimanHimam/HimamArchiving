import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Building2, Calendar, FileText, Inbox, Landmark, Search, Send } from 'lucide-react'
import {
  AppTreeView, createTreeCollection, type TreeNode,
} from './ui/tree-view'
import { archiveTree, type ArchiveTreeMode, type ArchiveTreeNodeDto } from '../lib/archiveTree'

export interface ArchiveTreeSelection {
  /** Node selector to hand back to the documents endpoint; null = the whole archive. */
  nodeId: string | null
  /** Human-readable path of the selection, for the table's header. */
  path: string
}

interface Props {
  mode: ArchiveTreeMode
  onModeChange: (mode: ArchiveTreeMode) => void
  selection: ArchiveTreeSelection
  onSelect: (selection: ArchiveTreeSelection) => void
}

/** The API node carries a `kind` and `count` the shared TreeNode does not model, so they ride
 *  along on the node and are read back out through this lookup rather than widening TreeNode. */
type Meta = { kind: string; count: number; path: string }

export default function ArchiveTreePanel({ mode, onModeChange, selection, onSelect }: Props) {
  const { t } = useTranslation()
  const [nodes, setNodes] = useState<ArchiveTreeNodeDto[]>([])
  const [manualExpanded, setManualExpanded] = useState<string[]>([])
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    archiveTree.tree(mode)
      .then((data) => { if (!cancelled) { setNodes(data); setError('') } })
      .catch(() => { if (!cancelled) setError(t('archiveTree.loadError')) })
      .finally(() => { if (!cancelled) setLoading(false) })
    return () => { cancelled = true }
  }, [mode, t])

  // Filter the tree to nodes matching the search, keeping any ancestor that has a surviving
  // descendant — otherwise a deep match would be unreachable.
  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase()
    if (!q) return nodes
    const prune = (list: ArchiveTreeNodeDto[]): ArchiveTreeNodeDto[] =>
      list.flatMap((n) => {
        const kids = prune(n.children ?? [])
        const hit = n.name.toLowerCase().includes(q)
        return hit || kids.length ? [{ ...n, children: kids }] : []
      })
    return prune(nodes)
  }, [nodes, search])

  // Flatten once so selection can resolve a node's kind/count and its full path label.
  const meta = useMemo(() => {
    const map = new Map<string, Meta>()
    const walk = (list: ArchiveTreeNodeDto[], parentPath: string) => {
      for (const n of list) {
        const path = parentPath ? `${parentPath} › ${n.name}` : n.name
        map.set(n.id, { kind: n.kind, count: n.count, path })
        walk(n.children ?? [], path)
      }
    }
    walk(nodes, '')
    return map
  }, [nodes])

  const collection = useMemo(() => {
    // An empty `children` array makes the shared tree render an expandable-but-empty branch;
    // leaves must have no `children` key at all.
    const toTreeNode = (n: ArchiveTreeNodeDto): TreeNode => {
      const kids = (n.children ?? []).map(toTreeNode)
      return { id: n.id, name: n.name, kind: n.kind, ...(kids.length ? { children: kids } : {}) }
    }
    return createTreeCollection<TreeNode>({
      nodeToValue: (n) => n.id,
      nodeToString: (n) => n.name,
      rootNode: { id: 'ROOT', name: '', children: filtered.map(toTreeNode) },
    })
  }, [filtered])

  // While searching, every surviving branch is expanded so matches are visible without manual
  // drilling. Derived in render rather than pushed into state by an effect — the expansion is a
  // pure function of the query and the pruned tree.
  const expanded = useMemo(() => {
    if (!search.trim()) return manualExpanded
    const ids: string[] = []
    const walk = (list: ArchiveTreeNodeDto[]) => list.forEach((n) => {
      if (n.children?.length) { ids.push(n.id); walk(n.children) }
    })
    walk(filtered)
    return ids
  }, [search, filtered, manualExpanded])

  const iconFor = (node: TreeNode) => {
    switch (node.kind) {
      case 'Department': return <Building2 className="h-4 w-4 shrink-0 text-teal-600" />
      case 'Year': return <Calendar className="h-4 w-4 shrink-0 text-amber-600" />
      case 'DocumentType': return <FileText className="h-4 w-4 shrink-0 text-slate-500" />
      case 'ExternalOrg': return <Landmark className="h-4 w-4 shrink-0 text-purple-600" />
      case 'Direction': return node.id.endsWith('Incoming')
        ? <Inbox className="h-4 w-4 shrink-0 text-blue-600" />
        : <Send className="h-4 w-4 shrink-0 text-emerald-600" />
      default: return null
    }
  }

  const total = nodes.reduce((sum, n) => sum + n.count, 0)

  return (
    <aside className="flex h-full min-h-0 w-full flex-col rounded-xl border border-slate-200 bg-white dark:border-slate-700/60 dark:bg-slate-900">
      <header className="border-b border-slate-200 px-3 py-2.5 dark:border-slate-700/60">
        <h2 className="text-sm font-bold text-slate-900 dark:text-slate-100">{t('archiveTree.title')}</h2>
      </header>

      {/* Axis switcher — which origin the tree branches on */}
      <div className="flex gap-1 p-2">
        {(['Department', 'External'] as ArchiveTreeMode[]).map((m) => (
          <button
            key={m}
            type="button"
            onClick={() => onModeChange(m)}
            aria-pressed={mode === m}
            // archive-tree__mode carries no styling of its own — it is the hook the touch
            // stylesheet needs to give this pair a finger-sized target.
            className={`archive-tree__mode flex-1 rounded-lg px-2.5 py-1.5 text-xs font-semibold transition-colors ${
              mode === m
                ? 'bg-teal-600 text-white'
                : 'bg-slate-100 text-slate-600 hover:bg-slate-200 dark:bg-slate-800 dark:text-slate-300 dark:hover:bg-slate-700'
            }`}
          >
            {t(m === 'Department' ? 'archiveTree.byDepartment' : 'archiveTree.byExternal')}
          </button>
        ))}
      </div>

      <div className="relative px-2 pb-2">
        <Search className="pointer-events-none absolute inset-inline-start-0 top-1/2 ms-4 h-3.5 w-3.5 -translate-y-1/2 text-slate-400" />
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('archiveTree.searchPlaceholder')}
          aria-label={t('archiveTree.searchPlaceholder')}
          className="h-9 w-full rounded-lg border border-slate-200 bg-white ps-8 pe-2 text-sm text-slate-700 outline-none focus:border-teal-500 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-200"
        />
      </div>

      {/* "All" row — clears the node filter back to the whole archive */}
      <button
        type="button"
        onClick={() => onSelect({ nodeId: null, path: '' })}
        className={`mx-2 mb-1 flex items-center gap-2 rounded-lg px-2.5 py-1.5 text-sm transition-colors ${
          selection.nodeId === null
            ? 'bg-blue-50 font-semibold text-blue-700 ring-1 ring-blue-200 dark:bg-blue-900/30 dark:text-blue-300'
            : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800/60'
        }`}
      >
        <FileText className="h-4 w-4 shrink-0" />
        <span className="flex-1 text-start">{t('archiveTree.allDocuments')}</span>
        <span className="rounded bg-slate-100 px-1.5 py-0.5 font-mono text-[0.7rem] text-slate-600 dark:bg-slate-800 dark:text-slate-300">
          {total}
        </span>
      </button>

      <div className="min-h-0 flex-1 overflow-auto px-2 pb-3">
        {loading && <p className="px-2 py-4 text-sm text-slate-500">{t('common.loading')}</p>}
        {!loading && error && <p className="px-2 py-4 text-sm text-red-600">{error}</p>}
        {!loading && !error && filtered.length === 0 && (
          <p className="px-2 py-4 text-sm text-slate-500">{t('archiveTree.empty')}</p>
        )}
        {!loading && !error && filtered.length > 0 && (
          <AppTreeView
            collection={collection}
            selectedIds={selection.nodeId ? [selection.nodeId] : []}
            expandedIds={expanded}
            onExpandedChange={setManualExpanded}
            onSelect={(id) => onSelect({ nodeId: id, path: meta.get(id)?.path ?? '' })}
            icon={iconFor}
            badge={(node) => (
              <span className="rounded bg-slate-100 px-1.5 py-0.5 font-mono text-[0.7rem] text-slate-600 dark:bg-slate-800 dark:text-slate-300">
                {meta.get(node.id)?.count ?? 0}
              </span>
            )}
          />
        )}
      </div>
    </aside>
  )
}
