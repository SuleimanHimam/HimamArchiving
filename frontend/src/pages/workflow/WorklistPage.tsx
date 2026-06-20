import { useEffect, useState, useCallback } from 'react'
import { motion } from 'motion/react'
import { useTranslation } from 'react-i18next'
import { workflow, type WorklistItem, WF_ACTION_LABELS, entityLabel } from '../../lib/workflow'
import { useToast } from '../../components/toast'
import '../incoming/incoming.css'
import './workflow.css'

export default function WorklistPage() {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const [tasks, setTasks] = useState<WorklistItem[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [busyId, setBusyId] = useState<number | null>(null)
  const [notes, setNotes] = useState<Record<number, string>>({})
  const locale = i18n.language === 'ar' ? 'ar' : 'en'

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try { setTasks(await workflow.myTasks()) }
    catch { setError(t('workflow.loadError')) }
    finally { setLoading(false) }
  }, [t])

  useEffect(() => { load() }, [load])

  async function act(task: WorklistItem, action: number) {
    setBusyId(task.taskId); setError('')
    try {
      await workflow.act(task.taskId, action, notes[task.taskId] || null)
      await load()
      toast.success(t('workflow.actions.approve'))
    } catch { toast.error(t('workflow.loadError')) }
    finally { setBusyId(null) }
  }

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">{t('workflow.kicker')}</span>
          <h1>{t('workflow.title')}</h1>
        </div>
        <button className="btn btn-ghost" onClick={load} disabled={loading}>↻ {t('common.actions.refresh')}</button>
      </header>

      {error && <p className="login__error">{error}</p>}

      {loading && <p className="muted">{t('common.loading')}</p>}
      {!loading && tasks.length === 0 && (
        <motion.div className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
          <p className="reg-empty">{t('workflow.empty')} 🎉</p>
        </motion.div>
      )}

      <div className="worklist">
        {tasks.map((task, i) => {
          const allowed = task.allowedActions.split(',').map((s) => s.trim())
          const actions = WF_ACTION_LABELS.filter((a) => allowed.includes(a.name))
          return (
            <motion.div
              key={task.taskId}
              className={`doc-card task-card ${task.isOverdue ? 'is-overdue' : ''}`}
              initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: i * 0.04 }}
            >
              <div className="task-head">
                <div>
                  <span className="task-stage">{task.stageName}</span>
                  <span className="task-def mono">{task.definitionName}</span>
                </div>
                <span className={`status-pill ${task.isOverdue ? 's-overdue' : 's-pending'}`}>
                  {task.isOverdue ? t('workflow.statuses.escalated') : t('workflow.statuses.pending')}
                </span>
              </div>

              <div className="task-meta mono">
                <span>{entityLabel(task.entityType)} #{task.entityId}</span>
                <span>{t('workflow.columns.dueDate')}: {new Date(task.dueAt).toLocaleString(locale)}</span>
              </div>

              <textarea
                className="action-note" rows={2} placeholder={t('common.optional') + '…'}
                value={notes[task.taskId] ?? ''}
                onChange={(e) => setNotes((n) => ({ ...n, [task.taskId]: e.target.value }))}
              />
              <div className="action-bar">
                {actions.map((a) => (
                  <button key={a.value} className={`btn ${a.cls}`} disabled={busyId === task.taskId} onClick={() => act(task, a.value)}>
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
