import { useEffect, useState, useCallback } from 'react'
import { motion } from 'motion/react'
import { notesApi, type Note } from '../../lib/userFeatures'
import { useToast } from '../../components/toast'
import '../incoming/incoming.css'

export default function NotepadPage() {
  const toast = useToast()
  const [notes, setNotes] = useState<Note[]>([])
  const [sel, setSel] = useState<Note | null>(null)
  const [title, setTitle] = useState('')
  const [content, setContent] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try { setNotes(await notesApi.list()) } catch { toast.error('تعذّر تحميل الملاحظات') }
  }, [toast])
  useEffect(() => { load() }, [load])

  function select(n: Note) { setSel(n); setTitle(n.title); setContent(n.content ?? '') }
  function newNote() { setSel(null); setTitle(''); setContent('') }

  async function save() {
    if (!title.trim() && !content.trim()) { toast.error('أدخل عنوانًا أو محتوى'); return }
    setBusy(true)
    try {
      const body = { title: title.trim() || 'ملاحظة', content }
      const saved = sel ? await notesApi.update(sel.id, body) : await notesApi.create(body)
      setSel(saved); await load(); toast.success('تم حفظ الملاحظة')
    } catch { toast.error('تعذّر الحفظ') } finally { setBusy(false) }
  }

  async function remove(n: Note) {
    if (!window.confirm(`حذف الملاحظة «${n.title}»؟`)) return
    try { await notesApi.remove(n.id); if (sel?.id === n.id) newNote(); await load() }
    catch { toast.error('تعذّر الحذف') }
  }

  return (
    <div>
      <header className="page__head">
        <div><span className="kicker">NOTEPAD · المفكّرة</span><h1>المفكّرة</h1></div>
        <button className="btn btn-primary" onClick={newNote}>+ ملاحظة جديدة</button>
      </header>

      <div className="detail-grid">
        <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
          <h3 className="detail-h3">ملاحظاتي ({notes.length})</h3>
          <ul style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: '.4rem' }}>
            {notes.length === 0 && <li className="muted">لا توجد ملاحظات</li>}
            {notes.map((n) => (
              <li key={n.id} onClick={() => select(n)}
                style={{
                  display: 'flex', alignItems: 'center', gap: '.6rem', cursor: 'pointer',
                  padding: '.55rem .75rem', borderRadius: 6,
                  border: `1px solid ${sel?.id === n.id ? 'var(--brass, #b0892d)' : 'var(--color-border, #e2d3b2)'}`,
                  background: sel?.id === n.id ? 'color-mix(in srgb, var(--brass, #b0892d) 10%, transparent)' : 'transparent',
                }}>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontWeight: 600 }}>{n.title}</div>
                  <div className="muted" style={{ fontSize: '.8rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {(n.content ?? '').slice(0, 80) || '—'}
                  </div>
                </div>
                <button className="btn btn-ghost btn-sm" title="حذف" onClick={(e) => { e.stopPropagation(); remove(n) }}>🗑</button>
              </li>
            ))}
          </ul>
        </motion.section>

        <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.08 }}>
          <h3 className="detail-h3">{sel ? 'تحرير ملاحظة' : 'ملاحظة جديدة'}</h3>
          <div className="form-grid">
            <label className="field field--wide"><span>العنوان</span>
              <input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="عنوان الملاحظة" /></label>
            <label className="field field--wide"><span>المحتوى</span>
              <textarea rows={14} value={content} onChange={(e) => setContent(e.target.value)} placeholder="اكتب ملاحظتك هنا…" /></label>
          </div>
          <div className="form-actions">
            <button className="btn btn-primary" disabled={busy} onClick={save}>{busy ? '…' : 'حفظ'}</button>
            {sel && <button className="btn btn-ghost" onClick={newNote}>جديدة</button>}
          </div>
        </motion.section>
      </div>
    </div>
  )
}
