import { useState, useMemo } from 'react'
import { motion } from 'motion/react'
import { useTranslation } from 'react-i18next'
import { TABLE_DEFS, TABLE_LABELS, tableDefs, getTablePrefs, saveTablePref, defaultLabel as colDefaultLabel, type ColPref } from '../lib/tableColumns'
import { useToast } from './toast'
import '../pages/documents/documents.css'

interface Row { key: string; defaultLabel: string; label: string; hidden: boolean }

export default function TableColumnsSettings() {
  const { t } = useTranslation()
  const toast = useToast()
  const tableKeys = Object.keys(TABLE_DEFS)
  const [table, setTable] = useState(tableKeys[0])
  const [busy, setBusy] = useState(false)

  // Build editable rows for the selected table from defaults + saved prefs.
  const initialRows = useMemo<Row[]>(() => {
    const defs = tableDefs(table)
    const saved = getTablePrefs()[table] ?? []
    const savedMap = new Map(saved.map((p) => [p.key, p]))
    const order = saved.length
      ? [...saved.map((p) => p.key).filter((k) => defs.some((d) => d.key === k)),
         ...defs.map((d) => d.key).filter((k) => !savedMap.has(k))]
      : defs.map((d) => d.key)
    return order.map((k) => {
      const d = defs.find((x) => x.key === k)!
      const o = savedMap.get(k)
      return { key: k, defaultLabel: colDefaultLabel(d, t), label: o?.label ?? '', hidden: o ? !!o.hidden : !!d.defaultHidden }
    })
  }, [table, t])

  const [rows, setRows] = useState<Row[]>(initialRows)
  // Reset local edits when the selected table (and thus initialRows) changes.
  const [tableSig, setTableSig] = useState(table)
  if (tableSig !== table) { setTableSig(table); setRows(initialRows) }

  function patch(i: number, p: Partial<Row>) {
    setRows((rs) => rs.map((r, idx) => (idx === i ? { ...r, ...p } : r)))
  }
  function move(i: number, dir: -1 | 1) {
    setRows((rs) => {
      const j = i + dir
      if (j < 0 || j >= rs.length) return rs
      const copy = [...rs]; [copy[i], copy[j]] = [copy[j], copy[i]]; return copy
    })
  }

  async function save() {
    setBusy(true)
    try {
      const prefs: ColPref[] = rows.map((r) => ({
        key: r.key,
        ...(r.label.trim() ? { label: r.label.trim() } : {}),
        ...(r.hidden ? { hidden: true } : {}),
      }))
      await saveTablePref(table, prefs)
      toast.success('تم حفظ أعمدة الجدول')
    } catch { toast.error('تعذّر حفظ الإعدادات') }
    finally { setBusy(false) }
  }

  async function reset() {
    setBusy(true)
    try {
      await saveTablePref(table, [])
      setRows(tableDefs(table).map((d) => ({ key: d.key, defaultLabel: colDefaultLabel(d, t), label: '', hidden: !!d.defaultHidden })))
      toast.success('تمت إعادة الأعمدة الافتراضية')
    } catch { toast.error('تعذّر إعادة التعيين') }
    finally { setBusy(false) }
  }

  const visibleCount = rows.filter((r) => !r.hidden).length

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
      <h3 className="detail-h3">أعمدة الجداول</h3>
      <p className="muted">خصّص أعمدة كل جدول: غيّر الاسم، أظهر/أخفِ العمود، وأعد ترتيبها. الإعدادات خاصة بك وحدك.</p>

      <label className="field" style={{ maxWidth: 360 }}>
        <span>الجدول</span>
        <select value={table} onChange={(e) => setTable(e.target.value)}>
          {tableKeys.map((k) => <option key={k} value={k}>{TABLE_LABELS[k] ?? k}</option>)}
        </select>
      </label>

      <div className="table-scroll" style={{ marginTop: '1rem' }}>
        <table className="reg-table">
          <thead>
            <tr><th>الترتيب</th><th>ظاهر</th><th>الاسم المعروض</th><th>الاسم الافتراضي</th></tr>
          </thead>
          <tbody>
            {rows.map((r, i) => (
              <tr key={r.key} style={r.hidden ? { opacity: 0.5 } : undefined}>
                <td className="row-actions">
                  <button className="btn btn-ghost btn-sm" title="لأعلى" disabled={i === 0} onClick={() => move(i, -1)}>▲</button>
                  <button className="btn btn-ghost btn-sm" title="لأسفل" disabled={i === rows.length - 1} onClick={() => move(i, 1)}>▼</button>
                </td>
                <td>
                  <input type="checkbox" checked={!r.hidden} onChange={(e) => patch(i, { hidden: !e.target.checked })}
                    disabled={!r.hidden && visibleCount <= 1} title={!r.hidden && visibleCount <= 1 ? 'يجب إبقاء عمود واحد على الأقل' : ''} />
                </td>
                <td><input value={r.label} placeholder={r.defaultLabel} onChange={(e) => patch(i, { label: e.target.value })} /></td>
                <td className="muted">{r.defaultLabel}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="form-actions" style={{ marginTop: '1rem' }}>
        <button className="btn btn-primary" disabled={busy} onClick={save}>حفظ</button>
        <button className="btn btn-ghost" disabled={busy} onClick={reset}>↺ إعادة الافتراضي</button>
      </div>
    </motion.section>
  )
}
