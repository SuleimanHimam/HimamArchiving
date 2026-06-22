import { useEffect, useState, useCallback } from 'react'
import { motion } from 'motion/react'
import { docNotesApi, type DocNote } from '../lib/userFeatures'
import { auth } from '../lib/auth'
import { useToast } from './toast'

/// Notes/comments attached to a single document — visible to everyone who can see the document.
export default function DocumentNotesPanel({ docId }: { docId: number }) {
  const toast = useToast()
  const [notes, setNotes] = useState<DocNote[]>([])
  const [text, setText] = useState('')
  const [busy, setBusy] = useState(false)
  const myId = auth.getUser()?.id

  const load = useCallback(async () => {
    try { setNotes(await docNotesApi.list(docId)) } catch { /* access-gated; ignore */ }
  }, [docId])
  useEffect(() => { load() }, [load])

  async function add() {
    if (!text.trim()) return
    setBusy(true)
    try { await docNotesApi.add(docId, text.trim()); setText(''); await load() }
    catch { toast.error('تعذّر إضافة الملاحظة') } finally { setBusy(false) }
  }
  async function remove(id: number) {
    if (!window.confirm('حذف الملاحظة؟')) return
    try { await docNotesApi.remove(docId, id); await load() } catch { toast.error('تعذّر الحذف') }
  }

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.12 }}>
      <h3 className="detail-h3">ملاحظات الوثيقة ({notes.length})</h3>

      <div style={{ display: 'flex', gap: '.5rem', marginBottom: '.8rem' }}>
        <input value={text} onChange={(e) => setText(e.target.value)} style={{ flex: 1 }}
          placeholder="أضف ملاحظة على هذه الوثيقة…"
          onKeyDown={(e) => { if (e.key === 'Enter') add() }} />
        <button className="btn btn-primary" disabled={busy || !text.trim()} onClick={add}>إضافة</button>
      </div>

      <ul style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: '.5rem' }}>
        {notes.length === 0 && <li className="muted">لا توجد ملاحظات بعد</li>}
        {notes.map((n) => (
          <li key={n.id} style={{ border: '1px solid var(--color-border, #e2d3b2)', borderRadius: 6, padding: '.6rem .8rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '.5rem' }}>
              <strong>{n.authorName}</strong>
              <span className="muted mono" style={{ fontSize: '.75rem' }}>{new Date(n.createdAt).toLocaleString('ar')}</span>
            </div>
            <p style={{ margin: '.35rem 0 0', whiteSpace: 'pre-wrap' }}>{n.content}</p>
            {myId === n.userId && (
              <div style={{ marginTop: '.35rem' }}>
                <button className="btn btn-ghost btn-sm" onClick={() => remove(n.id)}>حذف</button>
              </div>
            )}
          </li>
        ))}
      </ul>
    </motion.section>
  )
}
