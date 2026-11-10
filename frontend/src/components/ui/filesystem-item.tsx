import type React from 'react'
import { useState } from 'react'
import { ChevronLeft, ChevronRight, Folder, File } from 'lucide-react'
import { AnimatePresence, motion } from 'motion/react'
import { ContextMenu, ContextMenuContent, ContextMenuTrigger } from '@/components/ui/context-menu'

export type FilesystemNode = {
  name: string
  /** Present (even when empty) marks a folder; absent marks a leaf file. */
  nodes?: FilesystemNode[]
  /** Optional identity, so a caller can select and act on a row. */
  id?: string | number
  /** Start expanded — used to reveal the branch the caller is already inside. */
  defaultOpen?: boolean
}

interface FilesystemItemProps {
  node: FilesystemNode
  animated?: boolean
  /** Highlight the row whose `id` matches. */
  selectedId?: string | number | null
  /** Click the row's name (not its caret, which only expands). */
  onSelect?: (node: FilesystemNode) => void
  /** Right-click menu body for this row. Return nothing to leave the row plain. */
  renderMenu?: (node: FilesystemNode) => React.ReactNode
  /**
   * Draw this row's icon instead of the built-in folder/file glyph — so a tree
   * over real records can show whatever badge those records carry. Returning
   * nothing falls back to the default.
   */
  renderIcon?: (node: FilesystemNode) => React.ReactNode
}

/**
 * One row of a collapsible file tree, recursing into its own children.
 *
 * Upstream builds the caret and the child list as components declared inside
 * the render body; here they are plain values. A component created during
 * render is a new type on every pass, so React unmounts and remounts the whole
 * subtree — which would restart the open/close animation mid-flight.
 */
export function FilesystemItem({
  node, animated = false, selectedId, onSelect, renderMenu, renderIcon,
}: FilesystemItemProps) {
  const [isOpen, setIsOpen] = useState(node.defaultOpen ?? false)
  const rtl = typeof document !== 'undefined' && document.dir === 'rtl'

  // The caret points along the reading direction and turns toward the open
  // branch, so it is mirrored for RTL rather than merely rotated.
  const glyph = (
    <>
      <ChevronRight className="size-4 text-muted-foreground rtl:hidden" />
      <ChevronLeft className="hidden size-4 text-muted-foreground rtl:block" />
    </>
  )
  const chevron = animated ? (
    <motion.span
      animate={{ rotate: isOpen ? (rtl ? -90 : 90) : 0 }}
      transition={{ type: 'spring', bounce: 0, duration: 0.4 }}
      className="flex"
    >
      {glyph}
    </motion.span>
  ) : (
    <span className={`flex ${isOpen ? 'rotate-90 rtl:-rotate-90' : ''}`}>{glyph}</span>
  )

  const children = node.nodes?.map((child) => (
    <FilesystemItem node={child} key={child.id ?? child.name} animated={animated}
      selectedId={selectedId} onSelect={onSelect} renderMenu={renderMenu} renderIcon={renderIcon} />
  ))
  const childrenList = animated ? (
    <AnimatePresence>
      {isOpen && (
        <motion.ul
          initial={{ height: 0 }}
          animate={{ height: 'auto' }}
          exit={{ height: 0 }}
          transition={{ type: 'spring', bounce: 0, duration: 0.4 }}
          className="flex flex-col justify-end overflow-hidden ps-6"
        >
          {children}
        </motion.ul>
      )}
    </AnimatePresence>
  ) : isOpen ? (
    <ul className="ps-6">{children}</ul>
  ) : null

  // `list-none` sits on the <li>, not the parent <ul>: this project runs Tailwind
  // without preflight, so a bare list still gets the UA's bullets, and putting the
  // reset here keeps the component correct whatever <ul> a caller wraps it in.
  const custom = renderIcon?.(node)
  // A row with no caret has to make up that 22px itself, or its icon sits where
  // the carets are and the column of glyphs goes ragged.
  const unindented = !node.nodes || node.nodes.length === 0
  const glyphFor = custom ? (
    <span className={`flex shrink-0 ${unindented ? 'ms-[22px]' : ''}`}>{custom}</span>
  ) : (node.nodes ? (
    <Folder
      className={`size-6 shrink-0 fill-sky-500 text-sky-500 ${node.nodes.length === 0 ? 'ms-[22px]' : ''}`}
    />
  ) : (
    /* Theme token, not text-gray-900: a near-black file glyph disappears
       against this app's dark theme. */
    <File className="ms-[22px] size-6 shrink-0 text-foreground" />
  ))

  const isSelected = node.id !== undefined && node.id === selectedId
  // The label is a button only when it does something; a plain <span> otherwise,
  // so a decorative tree does not hand a screen reader a row of dead controls.
  const label = onSelect ? (
    <button
      type="button"
      onClick={() => onSelect(node)}
      className="-my-0.5 flex min-w-0 appearance-none items-center gap-1.5 truncate rounded border-0 bg-transparent px-1 py-0.5 text-start font-[inherit] text-[inherit] hover:bg-accent"
      aria-current={isSelected ? 'true' : undefined}
    >
      {glyphFor}
      <span className="truncate">{node.name}</span>
    </button>
  ) : (
    <>
      {glyphFor}
      {node.name}
    </>
  )

  const menu = renderMenu?.(node)
  const row = (
    <span
      className={`flex items-center gap-1.5 py-1 ${isSelected ? 'font-semibold' : ''}`}
      // Marks the row you are actually inside. Exposed as an attribute rather
      // than a fixed colour so each host can highlight it in its own palette.
      data-selected={isSelected ? 'true' : undefined}
      // A row menu has to beat any menu the surrounding pane put on itself,
      // and the outer one only yields if the event stops here.
      onContextMenu={menu ? (e) => e.stopPropagation() : undefined}
    >
      {/* Same reason: without preflight a <button> keeps the UA's grey box. */}
      {node.nodes && node.nodes.length > 0 && (
        <button
          type="button"
          onClick={() => setIsOpen(!isOpen)}
          className="-m-1 appearance-none border-0 bg-transparent p-1"
        >
          {chevron}
        </button>
      )}

      {label}
    </span>
  )

  return (
    <li className="list-none">
      {menu ? (
        <ContextMenu>
          <ContextMenuTrigger asChild>{row}</ContextMenuTrigger>
          <ContextMenuContent className="w-56">{menu}</ContextMenuContent>
        </ContextMenu>
      ) : row}

      {childrenList}
    </li>
  )
}
