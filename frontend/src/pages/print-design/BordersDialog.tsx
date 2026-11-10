import { useState } from 'react'
import { X, Check } from 'lucide-react'
import {
  BORDER_SETTINGS, BORDER_STYLES, BORDER_WIDTHS,
  type BordersDraft, type BordersTab, type BorderSetting, type Edge,
} from './borders'

const ALL_ON: Record<Edge, boolean> = { top: true, right: true, bottom: true, left: true }
const ALL_OFF: Record<Edge, boolean> = { top: false, right: false, bottom: false, left: false }

/**
 * Word's "Borders and Shading". One draft drives all three tabs: الحدود applies to the selected
 * paragraphs, حدود الصفحة to the page itself, and تظليل fills the paragraph behind the text.
 */
export default function BordersDialog({ tab, initial, onApply, onClose }: {
  tab: BordersTab
  initial: BordersDraft
  onApply: (v: BordersDraft, tab: BordersTab) => void
  onClose: () => void
}) {
  /* Mounted only while open (the designer renders it conditionally), so the draft starts from
     the props and needs no effect to resynchronise. */
  const [d, setD] = useState<BordersDraft>(initial)
  const [t, setT] = useState<BordersTab>(tab)
  const set = (patch: Partial<BordersDraft>) => setD((p) => ({ ...p, ...patch }))

  // The Setting column is a shortcut for the edge toggles; touching an edge makes it «مخصّص».
  const pickSetting = (k: BorderSetting) =>
    set({ setting: k, edges: k === 'none' ? { ...ALL_OFF } : k === 'custom' ? d.edges : { ...ALL_ON } })
  const toggleEdge = (e: Edge) => {
    const edges = { ...d.edges, [e]: !d.edges[e] }
    const on = Object.values(edges).some(Boolean)
    set({ edges, setting: !on ? 'none' : d.setting === 'none' ? 'box' : 'custom' })
  }

  const shading = t === 'shading'
  const preview = (edge: Edge) => (d.edges[edge] && d.setting !== 'none'
    ? `${Math.max(1, d.width)}px ${d.style} ${d.color}` : '1px dashed #d1d5db')

  return (
    <div className="wm-backdrop" onMouseDown={onClose}>
      <div className="wm-dialog bd-dialog" dir="rtl" onMouseDown={(e) => e.stopPropagation()} role="dialog" aria-modal="true" aria-label="الحدود والتظليل">
        <div className="wm-dialog__head">
          <h3>الحدود والتظليل</h3>
          <button type="button" className="wm-x" onClick={onClose} aria-label="إغلاق"><X size={18} /></button>
        </div>

        <div className="bd-tabs" role="tablist">
          {([['borders', 'الحدود'], ['page', 'حدود الصفحة'], ['shading', 'تظليل']] as [BordersTab, string][]).map(([k, label]) => (
            <button key={k} type="button" role="tab" aria-selected={t === k}
              className={`bd-tab${t === k ? ' is-active' : ''}`} onClick={() => setT(k)}>{label}</button>
          ))}
        </div>

        <div className="wm-dialog__body bd-body">
          {shading ? (
            <div className="bd-shading">
              <div className="bd-col__cap">التعبئة:</div>
              <div className="bd-fillrow">
                <input type="color" value={d.fill ?? '#ffffff'} onChange={(e) => set({ fill: e.target.value })} aria-label="لون التعبئة" />
                <button type="button" className={`bd-nofill${d.fill === null ? ' is-active' : ''}`} onClick={() => set({ fill: null })}>بلا لون</button>
              </div>
              <div className="bd-preview">
                <div className="bd-preview__cap">معاينة</div>
                <div className="bd-page" style={{ background: d.fill ?? '#fff' }}>
                  {Array.from({ length: 7 }, (_, i) => <span key={i} className="bd-line" />)}
                </div>
              </div>
            </div>
          ) : (
            <div className="bd-grid">
              <div className="bd-col">
                <div className="bd-col__cap">الإعداد:</div>
                {BORDER_SETTINGS.map((s) => (
                  <button key={s.k} type="button" className={`bd-set${d.setting === s.k ? ' is-active' : ''}`} onClick={() => pickSetting(s.k)}>
                    <span className={`bd-set__ic bd-set__ic--${s.k}`} aria-hidden />
                    <span>{s.label}</span>
                  </button>
                ))}
              </div>

              <div className="bd-col">
                <div className="bd-col__cap">النمط:</div>
                <div className="bd-styles" role="listbox" aria-label="النمط">
                  {BORDER_STYLES.map((s) => (
                    <button key={s.k} type="button" role="option" aria-selected={d.style === s.k}
                      className={`bd-style${d.style === s.k ? ' is-active' : ''}`} onClick={() => set({ style: s.k })}>
                      <span className="bd-style__line" style={{ borderTopStyle: s.k, borderTopWidth: s.k === 'double' ? 3 : 2 }} />
                      <span className="bd-style__lb">{s.label}</span>
                    </button>
                  ))}
                </div>
                <label className="wm-f"><span>اللون:</span>
                  <input type="color" value={d.color} onChange={(e) => set({ color: e.target.value })} /></label>
                <label className="wm-f"><span>العرض:</span>
                  <select value={d.width} onChange={(e) => set({ width: Number(e.target.value) })}>
                    {BORDER_WIDTHS.map((w) => <option key={w} value={w}>{w} نقطة</option>)}
                  </select></label>
              </div>

              {/* Word's diagram: the buttons around it switch each edge on and off. */}
              <div className="bd-preview">
                <div className="bd-preview__cap">معاينة — انقر الأزرار لتطبيق الحدود</div>
                <div className="bd-diagram">
                  <button type="button" className={`bd-edge bd-edge--top${d.edges.top ? ' is-on' : ''}`} onClick={() => toggleEdge('top')} aria-label="الحد العلوي" />
                  <button type="button" className={`bd-edge bd-edge--right${d.edges.right ? ' is-on' : ''}`} onClick={() => toggleEdge('right')} aria-label="الحد الأيمن" />
                  <button type="button" className={`bd-edge bd-edge--bottom${d.edges.bottom ? ' is-on' : ''}`} onClick={() => toggleEdge('bottom')} aria-label="الحد السفلي" />
                  <button type="button" className={`bd-edge bd-edge--left${d.edges.left ? ' is-on' : ''}`} onClick={() => toggleEdge('left')} aria-label="الحد الأيسر" />
                  <div className="bd-page" style={{
                    borderTop: preview('top'), borderRight: preview('right'),
                    borderBottom: preview('bottom'), borderLeft: preview('left'),
                    boxShadow: d.setting === 'shadow' ? `3px 3px 0 0 ${d.color}` : undefined,
                  }}>
                    {Array.from({ length: 7 }, (_, i) => <span key={i} className="bd-line" />)}
                  </div>
                </div>
                <div className="bd-applyto">تطبيق على: <strong>{t === 'page' ? 'الصفحة بأكملها' : 'الفقرة المحدّدة'}</strong></div>
              </div>
            </div>
          )}
        </div>

        <div className="wm-dialog__foot">
          <button type="button" className="btn btn-ghost" onClick={() => onApply(d, t)}>تطبيق</button>
          <div className="wm-foot-spacer" />
          <button type="button" className="btn btn-primary" onClick={() => { onApply(d, t); onClose() }}><Check size={15} /> موافق</button>
          <button type="button" className="btn btn-ghost" onClick={onClose}><X size={15} /> إلغاء الأمر</button>
        </div>
      </div>
    </div>
  )
}
