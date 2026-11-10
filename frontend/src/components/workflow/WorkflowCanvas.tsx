import * as React from 'react'
import {
  Zap, UserCheck, Users, Building2, GitBranch, Split, CheckCircle2, Plus, Mail, X, GripVertical,
  Copy, Trash2, Flag, Palette, Shapes, ChevronsLeft, ChevronsRight,
} from 'lucide-react'
import {
  ContextMenu, ContextMenuContent, ContextMenuItem, ContextMenuLabel, ContextMenuSeparator,
  ContextMenuSub, ContextMenuSubContent, ContextMenuSubTrigger, ContextMenuTrigger,
  ContextMenuRadioGroup, ContextMenuRadioItem, ContextMenuCheckboxItem,
} from '../ui/context-menu'
import { ASSIGNEE_TYPE, ASSIGNMENT_TYPE, STAGE_TYPE } from '../../lib/workflow'

/* ─────────────────────────────────────────────────────────────────────────────
   The route drawn as a graph.

   Layout is computed from the definition rather than stored: a stage's place in
   the picture is a fact about the route, so there are no coordinates to drag,
   save, or let drift out of step with the stages themselves. Parallel stages
   share a column and fan out; everything else is one column per step.

   The route runs right to left, like the language it is written in: the trigger
   sits where an Arabic reader starts and each stage follows leftwards. Columns
   are still numbered from the trigger — only the mapping from column to x is
   mirrored, so the layout maths stays readable and one function decides the
   direction.
   ───────────────────────────────────────────────────────────────────────────── */

/** The palette an administrator can paint a stage with.
 *
 *  Keys, not CSS: the stage stores "violet" and this table decides what violet
 *  looks like, so the picture can be restyled or rebranded without rewriting
 *  routes — and a colour that no longer exists falls back to the default tile
 *  instead of rendering as nothing. */
export const STAGE_COLORS = {
  slate:  { label: 'رمادي',   bg: '#23233a', fg: '#e8e9f5', accent: '#6b7280' },
  violet: { label: 'بنفسجي',  bg: '#2e2150', fg: '#c9a6ff', accent: '#8b5cf6' },
  blue:   { label: 'أزرق',    bg: '#15294c', fg: '#8fbcff', accent: '#3b82f6' },
  teal:   { label: 'فيروزي',  bg: '#0f3230', fg: '#7fded0', accent: '#14b8a6' },
  green:  { label: 'أخضر',    bg: '#16351f', fg: '#7ee2a8', accent: '#22c55e' },
  amber:  { label: 'كهرماني', bg: '#38290e', fg: '#f4c76b', accent: '#d97706' },
  orange: { label: 'برتقالي', bg: '#3a2413', fg: '#ffab76', accent: '#ea580c' },
  rose:   { label: 'وردي',    bg: '#3a1622', fg: '#ff9ab0', accent: '#e11d48' },
} as const

export type StageColor = keyof typeof STAGE_COLORS

export const DEFAULT_STAGE_COLOR: StageColor = 'slate'

export function stageColor(key?: string | null) {
  return STAGE_COLORS[(key ?? '') as StageColor] ?? STAGE_COLORS[DEFAULT_STAGE_COLOR]
}

/** What the canvas needs to know about a stage. Deliberately narrower than the
 *  editor's draft: the picture should not depend on fields it never draws. */
export interface CanvasStage {
  key: string
  name: string
  assignmentType?: number
  assigneeType: number
  assigneeName?: string | null
  parallelGroupId?: number | null
  minimumApprovals?: number | null
  conditionExpression?: string | null
  isFinal: boolean
  /** Palette key chosen by the administrator; undefined draws the default tile. */
  color?: string | null
  type?: number
}

/** What the right-click menu can do to a stage.
 *
 *  Grouped rather than spread across the props list because they belong to one
 *  surface: give the canvas the whole object and it draws the menu, give it
 *  nothing and right-click falls back to the browser's own. */
export interface CanvasMenuActions {
  insertBefore?: (key: string) => void
  duplicate?: (key: string) => void
  move?: (key: string, delta: number) => void
  setColor?: (key: string, color: StageColor) => void
  setType?: (key: string, type: number) => void
  setFinal?: (key: string, isFinal: boolean) => void
}

interface Props {
  stages: CanvasStage[]
  /** The تصنيف البريد that fires the route; shown on the trigger node. */
  triggerLabel: string
  selectedKey: string | null
  onSelect: (key: string | null) => void
  /** Insert a new stage after `afterKey`, or at the start when null. */
  onInsertAfter: (afterKey: string | null) => void
  /** Drop `fromKey` into `toKey`'s place. Omit to make the canvas read-only for ordering. */
  onReorder?: (fromKey: string, toKey: string) => void
  /** Remove a stage from the route. Omit to hide the control. */
  onDelete?: (key: string) => void
  /** The right-click menu's actions. Omit to leave the canvas without one. */
  menu?: CanvasMenuActions
}

const COL_W = 176
const ROW_H = 128
const NODE = 56

type Placed = { stage: CanvasStage | null; id: string; col: number; row: number; rows: number }

/** Groups consecutive stages that share a parallel group into one column. */
function layout(stages: CanvasStage[]): { placed: Placed[]; cols: number; maxRows: number } {
  const columns: CanvasStage[][] = []
  for (const s of stages) {
    const prev = columns[columns.length - 1]
    const sameGroup = prev && s.parallelGroupId != null
      && prev[0].parallelGroupId === s.parallelGroupId
    if (sameGroup) prev.push(s)
    else columns.push([s])
  }

  const placed: Placed[] = [{ stage: null, id: 'trigger', col: 0, row: 0, rows: 1 }]
  columns.forEach((group, i) => {
    group.forEach((s, r) => {
      placed.push({ stage: s, id: s.key, col: i + 1, row: r, rows: group.length })
    })
  })
  placed.push({ stage: null, id: 'result', col: columns.length + 1, row: 0, rows: 1 })

  const maxRows = columns.reduce((m, g) => Math.max(m, g.length), 1)
  return { placed, cols: columns.length + 2, maxRows }
}

const y = (row: number, rows: number, mid: number) => mid + (row - (rows - 1) / 2) * ROW_H

/** The kind of step this is, in the two words that fit under a tile. */
function stageKind(s: CanvasStage): { label: string; icon: React.ReactNode } {
  if (s.conditionExpression) return { label: 'شرطية', icon: <Split size={22} /> }
  if (s.parallelGroupId != null) return { label: 'متوازية', icon: <GitBranch size={22} /> }
  if ((s.assignmentType ?? 0) !== ASSIGNMENT_TYPE.Static) return { label: 'ديناميكية', icon: <Users size={22} /> }
  if (s.assigneeType === ASSIGNEE_TYPE.OrgUnit) return { label: 'إدارة', icon: <Building2 size={22} /> }
  return { label: 'تسلسلية', icon: <UserCheck size={22} /> }
}

export default function WorkflowCanvas({
  stages, triggerLabel, selectedKey, onSelect, onInsertAfter, onReorder, onDelete, menu,
}: Props) {
  const { placed, cols, maxRows } = React.useMemo(() => layout(stages), [stages])
  const [dragKey, setDragKey] = React.useState<string | null>(null)
  const [overKey, setOverKey] = React.useState<string | null>(null)

  const mid = (Math.max(maxRows, 1) * ROW_H) / 2 + 20
  const width = cols * COL_W + 80
  const height = Math.max(maxRows * ROW_H + 90, 260)

  // Right to left: column 0 (the trigger) sits at the right edge and the route
  // walks leftwards. Every coordinate below goes through here.
  const x = React.useCallback((col: number) => width - 40 - NODE - col * COL_W, [width])

  const at = React.useCallback((id: string) => {
    const p = placed.find((n) => n.id === id)!
    return { cx: x(p.col) + NODE / 2, cy: y(p.row, p.rows, mid) + NODE / 2 }
  }, [placed, mid, x])

  // Edges: trigger → first column, then column to column, last column → result.
  const byCol = React.useMemo(() => {
    const map = new Map<number, Placed[]>()
    for (const p of placed) map.set(p.col, [...(map.get(p.col) ?? []), p])
    return map
  }, [placed])

  const edges: { from: string; to: string; label?: string }[] = []
  for (let c = 0; c < cols - 1; c++) {
    const left = byCol.get(c) ?? []
    const right = byCol.get(c + 1) ?? []
    for (const a of left) {
      for (const b of right) {
        const quorum = b.stage?.minimumApprovals
        edges.push({
          from: a.id,
          to: b.id,
          // Only the first edge of a fan-out carries the quorum, or the label
          // repeats once per branch and the picture gets noisy.
          label: quorum && b.row === 0 ? `${quorum} من ${b.rows}` : undefined,
        })
      }
    }
  }

  const dropOn = (toKey: string) => {
    if (dragKey && onReorder && dragKey !== toKey) onReorder(dragKey, toKey)
    setDragKey(null)
    setOverKey(null)
  }

  const last = stages[stages.length - 1]?.key ?? null

  /** The menu for one stage. Only the actions that were handed in are drawn — a
   *  read-only canvas gets a shorter menu rather than dead entries. */
  const stageMenu = (s: CanvasStage, index: number) => (
    <ContextMenuContent className="w-60 wfc__menu">
      <ContextMenuLabel className="truncate">{s.name || 'مرحلة بلا اسم'}</ContextMenuLabel>
      <ContextMenuSeparator />

      <ContextMenuItem onSelect={() => onInsertAfter(s.key)}>
        <Plus size={14} aria-hidden /> إضافة مرحلة بعدها
      </ContextMenuItem>
      {menu?.insertBefore && (
        <ContextMenuItem onSelect={() => menu.insertBefore!(s.key)}>
          <Plus size={14} aria-hidden /> إضافة مرحلة قبلها
        </ContextMenuItem>
      )}
      {menu?.duplicate && (
        <ContextMenuItem onSelect={() => menu.duplicate!(s.key)}>
          <Copy size={14} aria-hidden /> تكرار المرحلة
        </ContextMenuItem>
      )}

      {menu?.move && (
        <>
          <ContextMenuSeparator />
          {/* Earlier is towards the trigger, which on this canvas is to the right. */}
          <ContextMenuItem disabled={index === 0} onSelect={() => menu.move!(s.key, -1)}>
            <ChevronsRight size={14} aria-hidden /> تقديمها في الترتيب
          </ContextMenuItem>
          <ContextMenuItem disabled={index === stages.length - 1} onSelect={() => menu.move!(s.key, 1)}>
            <ChevronsLeft size={14} aria-hidden /> تأخيرها في الترتيب
          </ContextMenuItem>
        </>
      )}

      {(menu?.setColor || menu?.setType) && <ContextMenuSeparator />}

      {menu?.setColor && (
        <ContextMenuSub>
          <ContextMenuSubTrigger>
            <Palette size={14} aria-hidden /> اللون
          </ContextMenuSubTrigger>
          <ContextMenuSubContent className="wfc__menu">
            <ContextMenuRadioGroup
              value={(s.color ?? DEFAULT_STAGE_COLOR)}
              onValueChange={(v) => menu.setColor!(s.key, v as StageColor)}
            >
              {(Object.keys(STAGE_COLORS) as StageColor[]).map((k) => (
                <ContextMenuRadioItem key={k} value={k}>
                  <span className="wfc__menudot" style={{ background: STAGE_COLORS[k].accent }} aria-hidden />
                  {STAGE_COLORS[k].label}
                </ContextMenuRadioItem>
              ))}
            </ContextMenuRadioGroup>
          </ContextMenuSubContent>
        </ContextMenuSub>
      )}

      {menu?.setType && (
        <ContextMenuSub>
          <ContextMenuSubTrigger>
            <Shapes size={14} aria-hidden /> نوع المرحلة
          </ContextMenuSubTrigger>
          <ContextMenuSubContent className="wfc__menu">
            <ContextMenuRadioGroup
              value={String(s.type ?? STAGE_TYPE.Sequential)}
              onValueChange={(v) => menu.setType!(s.key, Number(v))}
            >
              <ContextMenuRadioItem value={String(STAGE_TYPE.Sequential)}>تسلسلية</ContextMenuRadioItem>
              <ContextMenuRadioItem value={String(STAGE_TYPE.Parallel)}>متوازية</ContextMenuRadioItem>
              <ContextMenuRadioItem value={String(STAGE_TYPE.Conditional)}>شرطية</ContextMenuRadioItem>
            </ContextMenuRadioGroup>
          </ContextMenuSubContent>
        </ContextMenuSub>
      )}

      {menu?.setFinal && (
        <ContextMenuCheckboxItem
          checked={s.isFinal}
          onCheckedChange={(v) => menu.setFinal!(s.key, !!v)}
        >
          <Flag size={14} aria-hidden /> مرحلة نهائية
        </ContextMenuCheckboxItem>
      )}

      {onDelete && (
        <>
          <ContextMenuSeparator />
          <ContextMenuItem className="wfc__menu--danger" onSelect={() => onDelete(s.key)}>
            <Trash2 size={14} aria-hidden /> حذف المرحلة
          </ContextMenuItem>
        </>
      )}
    </ContextMenuContent>
  )

  return (
    <div className="wfc" role="group" aria-label="مخطّط سير العمل">
      <div className="wfc__scroll">
        {/* Right-clicking the empty grid is about the route as a whole, so it gets its own
            short menu; the node menus below stop the event so the two never both open. */}
        <ContextMenu dir="rtl">
          <ContextMenuTrigger asChild>
        <div className="wfc__stage" style={{ width, height }}>
          <svg className="wfc__wires" width={width} height={height} aria-hidden="true">
            <defs>
              {/* Points the way the route runs — leftwards. */}
              <marker id="wfc-arrow" viewBox="0 0 8 8" refX="7" refY="4"
                markerWidth="7" markerHeight="7" orient="auto">
                <path d="M 8 0 L 0 4 L 8 8 z" className="wfc__arrow" />
              </marker>
            </defs>
            {edges.map((e, i) => {
              const a = at(e.from)
              const b = at(e.to)
              const dx = Math.max(40, (a.cx - b.cx) / 2)
              const d = `M ${a.cx - NODE / 2} ${a.cy} C ${a.cx - dx} ${a.cy}, ${b.cx + dx} ${b.cy}, ${b.cx + NODE / 2 + 8} ${b.cy}`
              return (
                <g key={i}>
                  <path d={d} className="wfc__wire" markerEnd="url(#wfc-arrow)" />
                  {e.label && (
                    <foreignObject x={(a.cx + b.cx) / 2 - 34} y={(a.cy + b.cy) / 2 - 22} width="68" height="20">
                      <span className="wfc__wirelabel">{e.label}</span>
                    </foreignObject>
                  )}
                </g>
              )
            })}
          </svg>

          {placed.map((p) => {
            const left = x(p.col)
            const top = y(p.row, p.rows, mid)

            if (p.id === 'trigger') {
              return (
                <ContextMenu key={p.id} dir="rtl">
                  <ContextMenuTrigger asChild>
                    <div className="wfc__node wfc__node--trigger" style={{ left, top }}
                      onContextMenu={(e) => e.stopPropagation()}>
                      <span className="wfc__tile wfc__tile--trigger"><Zap size={22} /></span>
                      <span className="wfc__label">
                        بريد داخلي جديد
                        <em>{triggerLabel || 'اختر التصنيف'}</em>
                      </span>
                      {/* The route starts here, so this is where "add the first stage" belongs. */}
                      <button type="button" className="wfc__plus" title="إضافة مرحلة في البداية"
                        onClick={() => onInsertAfter(null)}>
                        <Plus size={12} />
                      </button>
                    </div>
                  </ContextMenuTrigger>
                  <ContextMenuContent className="w-56 wfc__menu">
                    <ContextMenuLabel>المُشغِّل</ContextMenuLabel>
                    <ContextMenuSeparator />
                    <ContextMenuItem onSelect={() => onInsertAfter(null)}>
                      <Plus size={14} aria-hidden /> إضافة مرحلة في البداية
                    </ContextMenuItem>
                  </ContextMenuContent>
                </ContextMenu>
              )
            }

            if (p.id === 'result') {
              return (
                <div key={p.id} className="wfc__node" style={{ left, top }}>
                  <span className="wfc__tile wfc__tile--result"><CheckCircle2 size={22} /></span>
                  <span className="wfc__label">
                    النتيجة لمُقدّم الطلب
                    <em>تلقائيًا</em>
                  </span>
                </div>
              )
            }

            const s = p.stage!
            const kind = stageKind(s)
            const paint = stageColor(s.color)
            const selected = selectedKey === s.key
            const index = stages.findIndex((st) => st.key === s.key)
            return (
              <ContextMenu key={p.id} dir="rtl">
                <ContextMenuTrigger asChild>
                  <div
                    className={`wfc__node ${dragKey === s.key ? 'is-dragging' : ''} ${overKey === s.key ? 'is-dropzone' : ''}`}
                    style={{ left, top }}
                    // Opening a stage's menu also selects it: the settings panel below should be
                    // showing whatever the menu is about to change.
                    onContextMenu={(e) => { e.stopPropagation(); onSelect(s.key) }}
                    onDragOver={(e) => { if (dragKey && dragKey !== s.key) { e.preventDefault(); setOverKey(s.key) } }}
                    onDragLeave={() => setOverKey((k) => (k === s.key ? null : k))}
                    onDrop={(e) => { e.preventDefault(); dropOn(s.key) }}
                  >
                    <button
                      type="button"
                      className={`wfc__tile ${selected ? 'is-selected' : ''}`}
                      style={{ background: paint.bg, color: paint.fg }}
                      aria-pressed={selected}
                      draggable={!!onReorder}
                      onDragStart={() => { setDragKey(s.key); onSelect(s.key) }}
                      onDragEnd={() => { setDragKey(null); setOverKey(null) }}
                      onClick={() => onSelect(selected ? null : s.key)}
                      onKeyDown={(e) => {
                        if ((e.key === 'Delete' || e.key === 'Backspace') && onDelete) {
                          e.preventDefault()
                          onDelete(s.key)
                        }
                      }}
                      title={`${s.name || 'مرحلة بلا اسم'} — ${kind.label}`}
                    >
                      {kind.icon}
                      {s.isFinal && <span className="wfc__final" aria-hidden />}
                      {onReorder && <span className="wfc__grip" aria-hidden><GripVertical size={11} /></span>}
                    </button>
                    <span className="wfc__label">
                      {s.name || 'مرحلة بلا اسم'}
                      <em>{s.assigneeName || assigneeHint(s)}</em>
                      <b className="wfc__kind" style={{ color: paint.accent, borderColor: paint.accent }}>{kind.label}</b>
                    </span>
                    {/* Insert after this stage — the connector is where you think about
                        what comes next, so the control lives on it, on the outgoing side. */}
                    <button type="button" className="wfc__plus" title="إضافة مرحلة بعدها"
                      onClick={() => onInsertAfter(s.key)}>
                      <Plus size={12} />
                    </button>
                    {onDelete && (
                      <button type="button" className="wfc__del" title="حذف المرحلة"
                        onClick={() => onDelete(s.key)}>
                        <X size={11} />
                      </button>
                    )}
                  </div>
                </ContextMenuTrigger>
                {stageMenu(s, index)}
              </ContextMenu>
            )
          })}

          {stages.length === 0 && (
            <button type="button" className="wfc__seed" onClick={() => onInsertAfter(null)}>
              <Mail size={16} aria-hidden /> أضف أول مرحلة
            </button>
          )}
        </div>
          </ContextMenuTrigger>
          <ContextMenuContent className="w-56 wfc__menu">
            <ContextMenuLabel>المسار</ContextMenuLabel>
            <ContextMenuSeparator />
            <ContextMenuItem onSelect={() => onInsertAfter(last)}>
              <Plus size={14} aria-hidden /> إضافة مرحلة في النهاية
            </ContextMenuItem>
            <ContextMenuItem onSelect={() => onInsertAfter(null)}>
              <Plus size={14} aria-hidden /> إضافة مرحلة في البداية
            </ContextMenuItem>
            {selectedKey && (
              <ContextMenuItem onSelect={() => onSelect(null)}>
                <X size={14} aria-hidden /> إلغاء تحديد المرحلة
              </ContextMenuItem>
            )}
          </ContextMenuContent>
        </ContextMenu>
      </div>

      {/* Plain text, no inline icon: an LTR glyph inside an Arabic sentence drags the
          bidi reordering around and the line reads back to front. */}
      <p className="wfc__hint">
        انقر أي مرحلة لتحرير إعداداتها، واسحبها لتغيير ترتيبها، وانقر بالزر الأيمن لقائمة الإجراءات.
      </p>
    </div>
  )
}

function assigneeHint(s: CanvasStage): string {
  if ((s.assignmentType ?? 0) === ASSIGNMENT_TYPE.DepartmentHead) return 'مدير إدارة مُقدّم الطلب'
  if ((s.assignmentType ?? 0) === ASSIGNMENT_TYPE.OrgHierarchy) return 'المستوى الأعلى'
  switch (s.assigneeType) {
    case ASSIGNEE_TYPE.OrgUnit: return 'إدارة'
    case ASSIGNEE_TYPE.User: return 'شخص محدَّد'
    case ASSIGNEE_TYPE.DirectManager: return 'المدير المباشر'
    default: return 'وظيفة'
  }
}
