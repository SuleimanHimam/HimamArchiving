import { useEffect, useMemo, useRef, useState } from 'react'
import { locations, type Building, type Room, type Cabinet, type Shelf, type Box, type Breadcrumb } from '../lib/locations'

/** Building → Room → Cabinet → Shelf → Box picker. Every level is independently selectable: choosing a
 * shelf (or box) at any level fills in the building/room/cabinet it belongs to. Emits the chosen box id. */
export default function LocationPicker({ value, onChange }: { value: number | null; onChange: (boxId: number | null) => void }) {
  const [buildings, setBuildings] = useState<Building[]>([])
  const [rooms, setRooms] = useState<Room[]>([])
  const [cabinets, setCabinets] = useState<Cabinet[]>([])
  const [shelves, setShelves] = useState<Shelf[]>([])
  const [boxes, setBoxes] = useState<Box[]>([])
  const [sel, setSel] = useState({ b: '', r: '', c: '', s: '' })
  const [bc, setBc] = useState<Breadcrumb | null>(null)
  const filledFor = useRef<number | null>(null)

  // Load the whole hierarchy once so any level can be chosen on its own.
  useEffect(() => {
    Promise.all([locations.buildings(), locations.rooms(), locations.cabinets(), locations.shelves(), locations.boxes({})])
      .then(([b, r, c, s, bx]) => {
        setBuildings(b); setRooms(r); setCabinets(c); setShelves(s); setBoxes(bx)
        if (b.length === 1) setSel((p) => (p.b ? p : { ...p, b: String(b[0].id) }))
      }).catch(() => {})
  }, [])

  // Walk parent ids through the loaded lists to fill in the levels above a chosen node.
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

  // Pre-fill all parents when a box is already chosen (editing a filed document, change-location modal).
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

  // Each level lists items under the chosen parent, or every item when no parent is chosen yet.
  const roomOpts  = useMemo(() => (sel.b ? rooms.filter((r) => String(r.buildingId) === sel.b) : rooms), [rooms, sel.b])
  const cabOpts   = useMemo(() => (sel.r ? cabinets.filter((c) => String(c.roomId) === sel.r) : cabinets), [cabinets, sel.r])
  const shelfOpts = useMemo(() => (sel.c ? shelves.filter((s) => String(s.cabinetId) === sel.c) : shelves), [shelves, sel.c])
  const boxOpts   = useMemo(() => (sel.s ? boxes.filter((b) => String(b.shelfId) === sel.s) : boxes), [boxes, sel.s])

  return (
    <>
      <label className="field"><span>المبنى</span>
        <select value={sel.b} onChange={(e) => { setSel({ b: e.target.value, r: '', c: '', s: '' }); onChange(null) }}>
          <option value="">—</option>
          {buildings.map((b) => <option key={b.id} value={b.id}>{b.nameAr}</option>)}
        </select></label>

      <label className="field"><span>الغرفة</span>
        <select value={sel.r} onChange={(e) => { const v = e.target.value; setSel(v ? chainFromRoom(Number(v)) : { b: sel.b, r: '', c: '', s: '' }); onChange(null) }}>
          <option value="">—</option>
          {roomOpts.map((r) => <option key={r.id} value={r.id}>{r.nameAr}{!sel.b ? ` · ${r.buildingName}` : ''}</option>)}
        </select></label>

      <label className="field"><span>الخزانة</span>
        <select value={sel.c} onChange={(e) => { const v = e.target.value; setSel(v ? chainFromCabinet(Number(v)) : { b: sel.b, r: sel.r, c: '', s: '' }); onChange(null) }}>
          <option value="">—</option>
          {cabOpts.map((c) => <option key={c.id} value={c.id}>{c.nameAr}{!sel.r ? ` · ${c.roomName}` : ''}</option>)}
        </select></label>

      <label className="field"><span>الرف</span>
        <select value={sel.s} onChange={(e) => { const v = e.target.value; setSel(v ? chainFromShelf(Number(v)) : { b: sel.b, r: sel.r, c: sel.c, s: '' }); onChange(null) }}>
          <option value="">—</option>
          {shelfOpts.map((s) => <option key={s.id} value={s.id}>رف {s.shelfNumber}{!sel.c ? ` · ${s.cabinetName}` : ''}</option>)}
        </select></label>

      <label className="field"><span>الصندوق</span>
        <select value={value ?? ''} onChange={(e) => onChange(e.target.value ? Number(e.target.value) : null)}>
          <option value="">—</option>
          {boxOpts.map((b) => <option key={b.id} value={b.id} disabled={b.isFull}>{b.boxCode}{b.isFull ? ' (ممتلئ)' : ''}</option>)}
        </select></label>

      {bc && (
        <div className="field field--wide">
          <span className="muted" style={{ fontSize: '.78rem' }}>الموقع: {bc.path}</span>
          <span className="mono muted" style={{ fontSize: '.78rem' }}>{bc.locationCode}</span>
        </div>
      )}
    </>
  )
}
