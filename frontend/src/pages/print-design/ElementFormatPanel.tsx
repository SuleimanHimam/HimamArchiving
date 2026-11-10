import { useEffect, useState } from 'react'
import { X, RotateCcw } from 'lucide-react'

import type { ElementFormat } from './elementFormat'
import { formatToCss } from './elementFormat'

const FONTS = ['Segoe UI', 'Tahoma', 'Arial', 'Times New Roman', 'Amiri', 'Reem Kufi', 'IBM Plex Sans Arabic']
const BORDER_STYLES = ['none', 'solid', 'dashed', 'dotted', 'double']
const ALIGNS: { v: string; label: string }[] = [
  { v: 'right', label: 'يمين' }, { v: 'center', label: 'توسيط' },
  { v: 'left', label: 'يسار' }, { v: 'justify', label: 'ضبط' },
]

/**
 * «تنسيق العنصر» — every appearance setting for the selected element in one panel.
 *
 * The ribbon covers the common ones (font, size, colours) for text *and* elements; this is for
 * what only an element has: its frame, its fill, its padding, its exact size. Changes apply as
 * they are made — there is no OK button, because the canvas behind it is the preview.
 */
export default function ElementFormatPanel({ label, initial, at, isPicture, onApply, onClose }: {
  label: string
  initial: ElementFormat
  /** Viewport position to anchor to (the selection badge). */
  at: { left: number; top: number }
  isPicture: boolean
  onApply: (css: Partial<CSSStyleDeclaration>) => void
  onClose: () => void
}) {
  const [f, setF] = useState<ElementFormat>(initial)

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  /* The change is written to the element and to the panel's own state. `onApply` stays outside
     the state updater: an updater runs during render, and calling into the designer from there
     is a setState-while-rendering — React warns, and the order of the two updates stops being
     predictable. */
  const set = (patch: Partial<ElementFormat>) => {
    const next = { ...f, ...patch }
    onApply(formatToCss(next, f))
    setF(next)
  }

  // Keep the panel on screen when the element sits near an edge.
  const left = Math.max(8, Math.min(at.left - 150, window.innerWidth - 320))
  const top = Math.max(8, Math.min(at.top + 26, window.innerHeight - 520))

  return (
    <div className="rd-fmt" style={{ left, top }} dir="rtl"
      onMouseDown={(e) => e.stopPropagation()} role="dialog" aria-label={`تنسيق ${label}`}>
      <div className="rd-fmt__head">
        <span>تنسيق: {label}</span>
        <button type="button" className="rd-fmt__x" onClick={onClose} aria-label="إغلاق"><X size={15} /></button>
      </div>

      <div className="rd-fmt__body">
        <div className="rd-fmt__sec">الخط</div>
        <label className="rd-fmt__row"><span>النوع</span>
          <select value={f.fontFamily} onChange={(e) => set({ fontFamily: e.target.value })}>
            {[f.fontFamily, ...FONTS.filter((x) => x !== f.fontFamily)].map((x) => (
              <option key={x} value={x} style={{ fontFamily: x }}>{x}</option>
            ))}
          </select></label>
        <label className="rd-fmt__row"><span>الحجم</span>
          <span className="rd-fmt__num">
            <input type="number" min={6} max={96} value={f.fontSizePt}
              onChange={(e) => set({ fontSizePt: Number(e.target.value) })} /> نقطة
          </span></label>
        <div className="rd-fmt__row"><span>النمط</span>
          <span className="rd-fmt__seg">
            <button type="button" className={f.bold ? 'is-on' : ''} onClick={() => set({ bold: !f.bold })} style={{ fontWeight: 700 }}>B</button>
            <button type="button" className={f.italic ? 'is-on' : ''} onClick={() => set({ italic: !f.italic })} style={{ fontStyle: 'italic' }}>I</button>
            <button type="button" className={f.underline ? 'is-on' : ''} onClick={() => set({ underline: !f.underline })} style={{ textDecoration: 'underline' }}>U</button>
          </span></div>
        <label className="rd-fmt__row"><span>تباعد الأسطر</span>
          <select value={f.lineHeight} onChange={(e) => set({ lineHeight: e.target.value })}>
            <option value="">تلقائي</option>
            {['1', '1.15', '1.5', '2', '2.5', '3'].map((x) => <option key={x} value={x}>{x}</option>)}
          </select></label>
        <div className="rd-fmt__row"><span>المحاذاة</span>
          <span className="rd-fmt__seg">
            {ALIGNS.map((a) => (
              <button key={a.v} type="button" className={f.align === a.v ? 'is-on' : ''} onClick={() => set({ align: a.v })}>{a.label}</button>
            ))}
          </span></div>

        <div className="rd-fmt__sec">الألوان</div>
        <label className="rd-fmt__row"><span>لون النص</span>
          <input type="color" value={f.color} onChange={(e) => set({ color: e.target.value })} /></label>
        <div className="rd-fmt__row"><span>التعبئة</span>
          <span className="rd-fmt__fill">
            <input type="color" value={f.fill ?? '#ffffff'} onChange={(e) => set({ fill: e.target.value })} />
            <button type="button" className={f.fill === null ? 'is-on' : ''} onClick={() => set({ fill: null })}>بلا</button>
          </span></div>
        <label className="rd-fmt__row"><span>الشفافية</span>
          <span className="rd-fmt__num">
            <input type="range" min={10} max={100} value={f.opacity} onChange={(e) => set({ opacity: Number(e.target.value) })} />
            {f.opacity}%
          </span></label>

        <div className="rd-fmt__sec">الإطار والمسافات</div>
        <label className="rd-fmt__row"><span>نمط الحد</span>
          <select value={f.borderStyle} onChange={(e) => set({ borderStyle: e.target.value })}>
            <option value="none">بلا</option>
            {BORDER_STYLES.filter((x) => x !== 'none').map((x) => <option key={x} value={x}>{x}</option>)}
          </select></label>
        <label className="rd-fmt__row"><span>سماكة الحد</span>
          <span className="rd-fmt__num">
            <input type="number" min={0} max={20} value={f.borderWidth} onChange={(e) => set({ borderWidth: Number(e.target.value) })} /> بكسل
          </span></label>
        <label className="rd-fmt__row"><span>لون الحد</span>
          <input type="color" value={f.borderColor} onChange={(e) => set({ borderColor: e.target.value })} /></label>
        <label className="rd-fmt__row"><span>استدارة الزوايا</span>
          <span className="rd-fmt__num">
            <input type="number" min={0} max={60} value={f.radius} onChange={(e) => set({ radius: Number(e.target.value) })} /> بكسل
          </span></label>
        <label className="rd-fmt__row"><span>الحشو الداخلي</span>
          <span className="rd-fmt__num">
            <input type="number" min={0} max={60} value={f.padding} onChange={(e) => set({ padding: Number(e.target.value) })} /> بكسل
          </span></label>

        {isPicture && (<>
          <div className="rd-fmt__sec">الأبعاد</div>
          <label className="rd-fmt__row"><span>العرض</span>
            <input type="text" value={f.width} placeholder="مثال: 140px أو 100%" onChange={(e) => set({ width: e.target.value })} /></label>
          <label className="rd-fmt__row"><span>الارتفاع</span>
            <input type="text" value={f.height} placeholder="تلقائي" onChange={(e) => set({ height: e.target.value })} /></label>
        </>)}
      </div>

      <div className="rd-fmt__foot">
        <button type="button" className="rd-fmt__reset" onClick={() => { onApply({
          font: '', fontFamily: '', fontSize: '', fontWeight: '', fontStyle: '', color: '',
          backgroundColor: '', textDecorationLine: '', textAlign: '', lineHeight: '',
          border: '', borderRadius: '', padding: '', opacity: '',
        } as Partial<CSSStyleDeclaration>); onClose() }}>
          <RotateCcw size={13} /> إعادة تعيين
        </button>
      </div>
    </div>
  )
}
