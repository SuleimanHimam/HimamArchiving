import { useEffect, useId, useMemo, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { useAnchoredPopup } from './ui/anchored-popup'
import { locations, type Building, type Room, type Cabinet, type Shelf, type Box, type Breadcrumb } from '../lib/locations'

type Opt = { id: number; label: string }

// Floating-label look shared with the LabeledInput family (bordered shadcn field).
const FL_LABEL =
  'origin-start absolute block px-1 cursor-text text-sm text-muted-foreground/70 transition-all duration-200 ease-out top-1/2 -translate-y-1/2 ' +
  'group-focus-within:top-0 group-focus-within:cursor-default group-focus-within:text-xs group-focus-within:font-medium group-focus-within:text-foreground group-focus-within:pointer-events-none ' +
  'has-[+input:not(:placeholder-shown)]:top-0 has-[+input:not(:placeholder-shown)]:cursor-default has-[+input:not(:placeholder-shown)]:text-xs has-[+input:not(:placeholder-shown)]:font-medium has-[+input:not(:placeholder-shown)]:text-foreground has-[+input:not(:placeholder-shown)]:pointer-events-none'
const FL_INPUT =
  'flex h-9 w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-foreground shadow-sm shadow-black/5 transition-shadow placeholder:text-muted-foreground/70 focus-visible:border-ring focus-visible:outline-none focus-visible:ring-[3px] focus-visible:ring-ring/20 disabled:cursor-not-allowed disabled:opacity-50'

/** A searchable select with a floating label. Clicking opens the FULL option list (a native
 *  <datalist> only shows options matching the text already in the field, so a pre-filled level
 *  would show just its one value); typing filters it. Keyboard: ↑/↓ move, Enter picks, Esc closes. */
function Combobox({ label, value, options, onPick }: {
  label: string
  value: string
  options: Opt[]
  onPick: (id: number | null) => void
}) {
  const id = useId()
  const current = options.find((o) => String(o.id) === value)?.label ?? ''
  const [text, setText] = useState(current)
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const [query, setQuery] = useState(false)   // true once the user types → filter; false → show all
  const wrapRef = useRef<HTMLDivElement>(null)
  const fieldRef = useRef<HTMLInputElement>(null)
  /* The list is portalled to <body> and positioned against the input: this picker is used inside
     the "مكان الحفظ الفعلي" dialog, whose body scrolls, and an absolutely-positioned list was
     clipped at the dialog's edge — only the first row or two of a level was reachable. */
  const popup = useAnchoredPopup(fieldRef, open, 240)
  useEffect(() => { setText(current) }, [current])

  // Show every option on open; narrow to matches only after the user starts typing.
  const shown = useMemo(() => {
    if (!query || text.trim() === '') return options
    const q = text.trim().toLowerCase()
    return options.filter((o) => o.label.toLowerCase().includes(q))
  }, [options, text, query])

  // Close when focus/click leaves the whole widget; snap the text back to the committed value.
  useEffect(() => {
    if (!open) return
    const onDown = (e: MouseEvent) => {
      const t = e.target as Node
      // The list now lives outside this subtree, so it needs its own containment check.
      if (wrapRef.current?.contains(t) || document.getElementById(`${id}-list`)?.contains(t)) return
      setOpen(false); setQuery(false); setText(current)
    }
    document.addEventListener('mousedown', onDown)
    return () => document.removeEventListener('mousedown', onDown)
  }, [open, current, id])

  const commit = (o: Opt) => { setText(o.label); onPick(o.id); setOpen(false); setQuery(false) }

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') { e.preventDefault(); if (!open) setOpen(true); setActive((i) => Math.min(i + 1, shown.length - 1)) }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActive((i) => Math.max(i - 1, 0)) }
    else if (e.key === 'Enter') { if (open && shown[active]) { e.preventDefault(); commit(shown[active]) } }
    else if (e.key === 'Escape') { setOpen(false); setQuery(false); setText(current) }
  }

  return (
    <div className="space-y-1" ref={wrapRef}>
      <div className="group relative">
        <label htmlFor={id} className={FL_LABEL}>
          <span className="inline-flex bg-background px-2">{label}</span>
        </label>
        <input
          id={id}
          ref={fieldRef}
          role="combobox"
          aria-expanded={open}
          aria-controls={`${id}-list`}
          value={text}
          placeholder=" "
          autoComplete="off"
          className={FL_INPUT}
          onFocus={() => { setOpen(true); setActive(0) }}
          onClick={() => { setOpen(true) }}
          onKeyDown={onKeyDown}
          onChange={(e) => {
            const v = e.target.value
            setText(v); setQuery(true); setOpen(true); setActive(0)
            if (v.trim() === '') { onPick(null); return }
            const m = options.find((o) => o.label === v)
            if (m) onPick(m.id)          // commit on an exact match (typed or picked); ignore partial typing
          }}
        />
        {open && shown.length > 0 && popup && createPortal(
          <ul
            id={`${id}-list`}
            role="listbox"
            style={popup}
            className="z-[200] m-0 list-none overflow-auto rounded-lg border border-input bg-background py-1 ps-0 shadow-lg shadow-black/20"
          >
            {shown.map((o, i) => (
              <li
                key={o.id}
                role="option"
                aria-selected={String(o.id) === value}
                className={
                  'cursor-pointer px-3 py-2 text-sm text-foreground ' +
                  (i === active ? 'bg-accent ' : '') +
                  (String(o.id) === value ? 'font-medium' : '')
                }
                onMouseEnter={() => setActive(i)}
                // onMouseDown (not onClick) so it fires before the input's blur closes the list.
                onMouseDown={(e) => { e.preventDefault(); commit(o) }}
              >
                {o.label}
              </li>
            ))}
          </ul>,
          document.body,
        )}
      </div>
    </div>
  )
}

/** Building → Room → Cabinet → Shelf → Box picker. Every level is a searchable combobox (type to find or
 * pick); choosing a shelf/box at any level fills in the building/room/cabinet it belongs to. Emits the box id. */
export default function LocationPicker({ value, onChange }: { value: number | null; onChange: (boxId: number | null) => void }) {
  const [buildings, setBuildings] = useState<Building[]>([])
  const [rooms, setRooms] = useState<Room[]>([])
  const [cabinets, setCabinets] = useState<Cabinet[]>([])
  const [shelves, setShelves] = useState<Shelf[]>([])
  const [boxes, setBoxes] = useState<Box[]>([])
  const [sel, setSel] = useState({ b: '', r: '', c: '', s: '' })
  const [bc, setBc] = useState<Breadcrumb | null>(null)
  const filledFor = useRef<number | null>(null)

  useEffect(() => {
    Promise.all([locations.buildings(), locations.rooms(), locations.cabinets(), locations.shelves(), locations.boxes({})])
      .then(([b, r, c, s, bx]) => {
        setBuildings(b); setRooms(r); setCabinets(c); setShelves(s); setBoxes(bx)
        if (b.length === 1) setSel((p) => (p.b ? p : { ...p, b: String(b[0].id) }))
      }).catch(() => {})
  }, [])

  const chainFromShelf = (shelfId: number) => {
    const sh = shelves.find((x) => x.id === shelfId)
    const cab = sh ? cabinets.find((x) => x.id === sh.cabinetId) : undefined
    const room = cab ? rooms.find((x) => x.id === cab.roomId) : undefined
    return { b: room ? String(room.buildingId) : '', r: cab ? String(cab.roomId) : '', c: sh ? String(sh.cabinetId) : '', s: String(shelfId) }
  }
  const chainFromCabinet = (cabId: number) => {
    const cab = cabinets.find((x) => x.id === cabId)
    const room = cab ? rooms.find((x) => x.id === cab.roomId) : undefined
    return { b: room ? String(room.buildingId) : '', r: cab ? String(cab.roomId) : '', c: String(cabId), s: '' }
  }
  const chainFromRoom = (roomId: number) => {
    const room = rooms.find((x) => x.id === roomId)
    return { b: room ? String(room.buildingId) : '', r: String(roomId), c: '', s: '' }
  }

  useEffect(() => {
    if (!value) { filledFor.current = null; return }
    if (filledFor.current === value || boxes.length === 0) return
    const box = boxes.find((x) => x.id === value)
    if (!box) return
    filledFor.current = value
    if (box.shelfId) setSel(chainFromShelf(box.shelfId))
    else if (box.roomId) setSel(chainFromRoom(box.roomId))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value, boxes])

  useEffect(() => { if (value) locations.breadcrumb(value).then(setBc).catch(() => setBc(null)); else setBc(null) }, [value])

  // Options per level — listed under the chosen parent, or everything when no parent is chosen yet.
  const buildingOpts = useMemo<Opt[]>(() => buildings.map((b) => ({ id: b.id, label: b.nameAr })), [buildings])
  const roomOpts = useMemo<Opt[]>(() => (sel.b ? rooms.filter((r) => String(r.buildingId) === sel.b) : rooms)
    .map((r) => ({ id: r.id, label: r.nameAr + (!sel.b ? ` · ${r.buildingName}` : '') })), [rooms, sel.b])
  const cabOpts = useMemo<Opt[]>(() => (sel.r ? cabinets.filter((c) => String(c.roomId) === sel.r) : cabinets)
    .map((c) => ({ id: c.id, label: c.nameAr + (!sel.r ? ` · ${c.roomName}` : '') })), [cabinets, sel.r])
  const shelfOpts = useMemo<Opt[]>(() => (sel.c ? shelves.filter((s) => String(s.cabinetId) === sel.c) : shelves)
    .map((s) => ({ id: s.id, label: `رف ${s.shelfNumber}` + (!sel.c ? ` · ${s.cabinetName}` : '') })), [shelves, sel.c])
  const boxOpts = useMemo<Opt[]>(() => (sel.s ? boxes.filter((b) => String(b.shelfId) === sel.s) : boxes)
    .map((b) => ({ id: b.id, label: b.boxCode + (b.isFull ? ' (ممتلئ)' : '') })), [boxes, sel.s])

  return (
    <>
      <Combobox label="المبنى" value={sel.b} options={buildingOpts}
        onPick={(id) => { setSel({ b: id ? String(id) : '', r: '', c: '', s: '' }); onChange(null) }} />

      <Combobox label="الغرفة" value={sel.r} options={roomOpts}
        onPick={(id) => { setSel(id ? chainFromRoom(id) : { b: sel.b, r: '', c: '', s: '' }); onChange(null) }} />

      <Combobox label="الخزانة" value={sel.c} options={cabOpts}
        onPick={(id) => { setSel(id ? chainFromCabinet(id) : { b: sel.b, r: sel.r, c: '', s: '' }); onChange(null) }} />

      <Combobox label="الرف" value={sel.s} options={shelfOpts}
        onPick={(id) => { setSel(id ? chainFromShelf(id) : { b: sel.b, r: sel.r, c: sel.c, s: '' }); onChange(null) }} />

      <Combobox label="الصندوق" value={value != null ? String(value) : ''} options={boxOpts}
        onPick={(id) => {
          const box = id != null ? boxes.find((b) => b.id === id) : null
          onChange(box && !box.isFull ? box.id : null)   // don't file into a full box
        }} />

      {bc && (
        <div className="field field--wide">
          <span className="muted" style={{ fontSize: '.78rem' }}>الموقع: {bc.path}</span>
          <span className="mono muted" style={{ fontSize: '.78rem' }}>{bc.locationCode}</span>
        </div>
      )}
    </>
  )
}
