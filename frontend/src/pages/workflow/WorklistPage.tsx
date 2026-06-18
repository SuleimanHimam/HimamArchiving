import { useEffect, useState, useCallback } from 'react'
import { motion } from 'motion/react'
import { workflow, type WorklistItem, WF_ACTION_LABELS, entityLabel } from '../../lib/workflow'
import { useToast } from '../../components/toast'
import '../incoming/incoming.css'
import './workflow.css'

export default function WorklistPage() {
  const toast = useToast()
  const [tasks, setTasks] = useState<WorklistItem[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [busyId, setBusyId] = useState<number | null>(null)
  const [notes, setNotes] = useState<Record<number, string>>({})

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try { setTasks(await workflow.myTasks()) }
    catch { setError('تعذّر تحميل قائمة الأعمال') }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { load() }, [load])

  async function act(task: WorklistItem, action: number) {
    setBusyId(task.taskId); setError('')
    try {
      await workflow.act(task.taskId, action, notes[task.taskId] || null)
      await load()
      toast.success('تم تنفيذ الإجراء')
    } catch { toast.error('تعذّر تنفيذ الإجراء (قد لا يكون مسموحًا في هذه المرحلة)') }
    finally { setBusyId(null) }
  }

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">WORKFLOW · قائمة أعمالي</span>
          <h1>المهام الواردة إليّ</h1>
        </div>
        <button className="btn btn-ghost" onClick={load} disabled={loading}>↻ تحديث</button>
      </header>

      {error && <p className="login__error">{error}</p>}

      {loading && <p className="muted">…جارٍ التحميل</p>}
      {!loading && tasks.length === 0 && (
        <motion.div className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
          <p className="reg-empty">لا توجد مهام معلّقة 🎉</p>
        </motion.div>
      )}

      <div className="worklist">
        {tasks.map((t, i) => {
          // The stage's permitted actions come back as a flags string; only show those.
          const allowed = t.allowedActions.split(',').map((s) => s.trim())
          const actions = WF_ACTION_LABELS.filter((a) => allowed.includes(a.name))
          return (
            <motion.div
              key={t.taskId}
              className={`doc-card task-card ${t.isOverdue ? 'is-overdue' : ''}`}
              initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: i * 0.04 }}
            >
              <div className="task-head">
                <div>
                  <span className="task-stage">{t.stageName}</span>
                  <span className="task-def mono">{t.definitionName}</span>
                </div>
                <span className={`status-pill ${t.isOverdue ? 's-overdue' : 's-pending'}`}>
                  {t.isOverdue ? 'متأخرة' : 'قيد الانتظار'}
                </span>
              </div>

              <div className="task-meta mono">
                <span>{entityLabel(t.entityType)} #{t.entityId}</span>
                <span>الاستحقاق: {new Date(t.dueAt).toLocaleString('ar')}</span>
              </div>

              <textarea
                className="action-note" rows={2} placeholder="ملاحظة / تأشيرة (اختياري)…"
                value={notes[t.taskId] ?? ''}
                onChange={(e) => setNotes((n) => ({ ...n, [t.taskId]: e.target.value }))}
              />
              <div className="action-bar">
                {actions.map((a) => (
                  <button key={a.value} className={`btn ${a.cls}`} disabled={busyId === t.taskId} onClick={() => act(t, a.value)}>
                    {a.label}
                  </button>
                ))}
              </div>
            </motion.div>
          )
        })}
      </div>
    </div>
  )
}
