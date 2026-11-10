import { useEffect, useRef, useState } from 'react'
import { X, Check } from 'lucide-react'

export type WmType = 'none' | 'text' | 'image'

export interface WmDraft {
  type: WmType
  text: string
  image: string | null
  color: string
  font: string
  size: number
  semiTransparent: boolean          // ↔ opacity (شبه شفاف)
  layout: 'horizontal' | 'diagonal' // ↔ rotate
}

const FONTS = ['Segoe UI', 'Tahoma', 'Arial', 'Times New Roman', 'Amiri', 'Reem Kufi', 'IBM Plex Sans Arabic']
const SIZES = [24, 36, 48, 60, 72, 84, 96, 120, 144, 160]

/** Word-style "Printed Watermark" dialog. Edits a local draft; تطبيق commits without
 *  closing, موافق commits and closes, إلغاء الأمر discards. */
export default function WatermarkDialog({ open, initial, onCommit, onClose }: {
  open: boolean
  initial: WmDraft
  onCommit: (v: WmDraft) => void
  onClose: () => void
}) {
  const [d, setD] = useState<WmDraft>(initial)
  const imgInput = useRef<HTMLInputElement>(null)
  useEffect(() => { if (open) setD(initial) }, [open]) // eslint-disable-line react-hooks/exhaustive-deps

  if (!open) return null
  const set = (patch: Partial<WmDraft>) => setD((p) => ({ ...p, ...patch }))
  const textOn = d.type === 'text'
  const anyOn = d.type !== 'none'
  const imgOn = d.type === 'image'

  const onImageFile = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0]; e.target.value = ''
    if (!f) return
    if (f.size > 1_500_000) { return }
    const r = new FileReader(); r.onload = () => set({ image: r.result as string, type: 'image' }); r.readAsDataURL(f)
  }

  return (
    <div className="wm-backdrop" onMouseDown={onClose}>
      <div className="wm-dialog" dir="rtl" onMouseDown={(e) => e.stopPropagation()} role="dialog" aria-modal="true" aria-label="إعدادات العلامة المائية">
        <div className="wm-dialog__head">
          <h3>إعدادات العلامة المائية</h3>
          <button type="button" className="wm-x" onClick={onClose} aria-label="إغلاق"><X size={18} /></button>
        </div>

        <div className="wm-dialog__body">
          <label className="wm-radio">
            <input type="radio" name="wm-type" checked={d.type === 'none'} onChange={() => set({ type: 'none' })} />
            <span>بدون علامة مائية</span>
          </label>

          <label className="wm-radio">
            <input type="radio" name="wm-type" checked={d.type === 'image'} onChange={() => set({ type: 'image' })} />
            <span>صورة العلامة المائية</span>
          </label>
          <div className="wm-inset">
            <button type="button" className="wm-imgbtn" disabled={!imgOn} onClick={() => imgInput.current?.click()}>
              {d.image ? 'تغيير الصورة…' : 'إدراج صورة…'}
            </button>
            {d.image && imgOn && <img className="wm-thumb" src={d.image} alt="" />}
            <input ref={imgInput} type="file" accept="image/*" style={{ display: 'none' }} onChange={onImageFile} />
          </div>

          <label className="wm-radio">
            <input type="radio" name="wm-type" checked={d.type === 'text'} onChange={() => set({ type: 'text' })} />
            <span>نص العلامة المائية</span>
          </label>

          <div className="wm-grid">
            <label className={`wm-f${textOn ? '' : ' is-off'}`}><span>النص:</span>
              <input type="text" value={d.text} disabled={!textOn} onChange={(e) => set({ text: e.target.value })} placeholder="سري" /></label>
            <label className={`wm-f${textOn ? '' : ' is-off'}`}><span>اللون:</span>
              <input type="color" value={d.color} disabled={!textOn} onChange={(e) => set({ color: e.target.value })} /></label>

            <label className={`wm-f${textOn ? '' : ' is-off'}`}><span>الخط:</span>
              <select value={d.font} disabled={!textOn} onChange={(e) => set({ font: e.target.value })}>
                {FONTS.map((f) => <option key={f} value={f} style={{ fontFamily: f }}>{f}</option>)}
              </select></label>
            <label className={`wm-f${anyOn ? '' : ' is-off'}`}><span>الحجم:</span>
              <select value={d.size} disabled={!anyOn} onChange={(e) => set({ size: Number(e.target.value) })}>
                {SIZES.map((s) => <option key={s} value={s}>{s}</option>)}
              </select></label>

            <div className={`wm-f${anyOn ? '' : ' is-off'}`}><span>تخطيط:</span>
              <div className="wm-layout">
                <label className="wm-radio wm-radio--sm">
                  <input type="radio" name="wm-layout" checked={d.layout === 'diagonal'} disabled={!anyOn} onChange={() => set({ layout: 'diagonal' })} />
                  <span>قطري</span></label>
                <label className="wm-radio wm-radio--sm">
                  <input type="radio" name="wm-layout" checked={d.layout === 'horizontal'} disabled={!anyOn} onChange={() => set({ layout: 'horizontal' })} />
                  <span>أفقي</span></label>
              </div>
            </div>
            <label className={`wm-f wm-f--check${anyOn ? '' : ' is-off'}`}><span>الشفافية:</span>
              <label className="wm-check">
                <input type="checkbox" checked={d.semiTransparent} disabled={!anyOn} onChange={(e) => set({ semiTransparent: e.target.checked })} />
                <span>شبه شفاف</span>
              </label></label>
          </div>
        </div>

        <div className="wm-dialog__foot">
          <button type="button" className="btn btn-ghost" onClick={() => onCommit(d)}>تطبيق</button>
          <div className="wm-foot-spacer" />
          <button type="button" className="btn btn-primary" onClick={() => { onCommit(d); onClose() }}><Check size={15} /> موافق</button>
          <button type="button" className="btn btn-ghost" onClick={onClose}><X size={15} /> إلغاء الأمر</button>
        </div>
      </div>
    </div>
  )
}
