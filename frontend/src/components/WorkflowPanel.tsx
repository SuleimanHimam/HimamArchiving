import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, XCircle, Undo2, Clock, Send, FileCheck2, CornerUpLeft } from 'lucide-react'
import { workflow, WF_ACTIONS, stageAllows, type WorkflowTimelineEntry } from '../lib/workflow'
import { useDateFormat } from '../lib/useCalendar'
import { useToast } from './toast'
import { useConfirmDialog } from './ui/confirm-dialog'
import { LabeledTextarea } from './ui/labeled-input'
import '../styles/workflow.css'

/**
 * The workflow strip inside a piece of internal correspondence.
 *
 * Renders nothing at all for ordinary mail, which is most of it. When the message does belong to a
 * run it shows where the run has got to, the buttons this reader may press — and only those the
 * stage allows — and the timeline of everything that has happened, as the participants would
 * narrate it.
 */
export default function WorkflowPanel({ mailId }: { mailId: number }) {
  const { dateTime } = useDateFormat()
  const toast = useToast()
  const { confirm } = useConfirmDialog()
  const qc = useQueryClient()
  const [note, setNote] = useState('')
  const [busy, setBusy] = useState(false)

  const { data: ctx } = useQuery({
    queryKey: ['workflow', 'for-mail', mailId],
    queryFn: () => workflow.forMail(mailId),
    enabled: Number.isFinite(mailId),
  })

  if (!ctx) return null

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ['workflow', 'for-mail'] })
    qc.invalidateQueries({ queryKey: ['internal-mail'] })
  }

  async function act(value: number, label: string, needsNote: boolean) {
    if (!ctx?.myTaskId) return
    const reason = note.trim()
    if (needsNote && !reason) {
      toast.error('السبب مطلوب قبل هذا الإجراء.')
      return
    }
    if (!(await confirm({ message: `${label}: هل أنت متأكد؟`, destructive: value === 2 }))) return
    setBusy(true)
    try {
      await workflow.act(ctx.myTaskId, value, reason || null)
      setNote('')
      toast.success('تم تسجيل إجراءك، وأُرسلت المعاملة تلقائيًا إلى الخطوة التالية.')
      refresh()
    } catch {
      toast.error('تعذّر تنفيذ الإجراء.')
    } finally {
      setBusy(false)
    }
  }

  async function resubmit() {
    if (!ctx) return
    setBusy(true)
    try {
      await workflow.resubmit(ctx.instanceId, note.trim() || undefined)
      setNote('')
      toast.success('أُعيد إرسال الطلب، وتابع سير العمل من حيث توقّف.')
      refresh()
    } catch {
      toast.error('تعذّر إعادة الإرسال.')
    } finally {
      setBusy(false)
    }
  }

  const state = STATE[ctx.status] ?? STATE.Running

  return (
    <section className={`wf wf--${state.tone}`}>
      <header className="wf__head">
        <span className="wf__tag">سير عمل</span>
        <h3 className="wf__title">{ctx.definitionName}</h3>
        <span className={`wf__state wf__state--${state.tone}`}>{state.label}</span>
      </header>

      <div className="wf__meta">
        <span>
          الخطوة <strong>{ctx.stepNumber}</strong> من <strong>{ctx.totalSteps}</strong>
          {ctx.currentStageName ? ` — ${ctx.currentStageName}` : ''}
        </span>
        {ctx.actionRequired && <span className="wf__await">بانتظار إجرائك</span>}
        {ctx.myTaskDueAt && (
          <span className="wf__due"><Clock size={13} aria-hidden /> {dateTime(ctx.myTaskDueAt)}</span>
        )}
      </div>

      {/* Progress across the route's stages, so «خطوة 2 من 5» is also visible at a glance. */}
      <ol className="wf__steps" aria-label="مراحل سير العمل">
        {Array.from({ length: ctx.totalSteps }, (_, i) => (
          <li key={i} className={`wf__step ${i + 1 < ctx.stepNumber ? 'is-done'
            : i + 1 === ctx.stepNumber ? 'is-now' : ''}`} />
        ))}
      </ol>

      {(ctx.actionRequired || ctx.canResubmit) && (
        <div className="wf__act">
          <LabeledTextarea
            label="ملاحظة / سبب"
            rows={2}
            value={note}
            onChange={(e) => setNote(e.target.value)}
            hint="مطلوب عند الرفض أو الإعادة للتعديل."
          />
          <div className="wf__buttons">
            {ctx.actionRequired && WF_ACTIONS
              .filter((a) => stageAllows(ctx.myTaskAllowedActions, a.name))
              .map((a) => (
                <button key={a.name} type="button" className={`btn ${a.cls}`} disabled={busy}
                  onClick={() => act(a.value, a.label, a.needsNote)}>
                  {a.label}
                </button>
              ))}
            {ctx.canResubmit && (
              <button type="button" className="btn btn-primary" disabled={busy} onClick={resubmit}>
                إعادة الإرسال بعد التعديل
              </button>
            )}
          </div>
        </div>
      )}

      <h4 className="wf__h4">الخط الزمني</h4>
      <ol className="wf__timeline">
        {ctx.timeline.map((e, i) => (
          <li key={i} className={`wf__tl wf__tl--${KIND[e.kind]?.tone ?? 'plain'}`}>
            <span className="wf__tlicon" aria-hidden>{KIND[e.kind]?.icon ?? <Send size={14} />}</span>
            <span className="wf__tltime mono">{dateTime(e.at)}</span>
            <span className="wf__tltext">
              {label(e)}
              {e.note && <em className="wf__tlnote">{e.note}</em>}
            </span>
          </li>
        ))}
      </ol>
    </section>
  )
}

/** One line of the timeline, phrased as a person would say it. */
function label(e: WorkflowTimelineEntry): string {
  const who = e.actorName ?? 'غير محدّد'
  switch (e.kind) {
    case 'Submitted': return `${who} قدّم الطلب`
    case 'Sent': return `أُرسلت إلى ${who} — ${e.stageName}`
    case 'Approved': return `${who} اعتمد — ${e.stageName}`
    case 'Rejected': return `${who} رفض — ${e.stageName}`
    case 'ChangesRequested': return `${who} أعادها للتعديل — ${e.stageName}`
    case 'Returned': return `${who} أرجعها للمرحلة السابقة — ${e.stageName}`
    case 'Completed': return 'اكتمل سير العمل'
    default: return `${e.kind} — ${e.stageName}`
  }
}

const STATE: Record<string, { label: string; tone: string }> = {
  Running: { label: 'قيد التنفيذ', tone: 'run' },
  Completed: { label: 'مكتمل', tone: 'ok' },
  Rejected: { label: 'مرفوض', tone: 'bad' },
  Cancelled: { label: 'ملغى', tone: 'plain' },
  ChangesRequested: { label: 'بانتظار تعديل مُقدّم الطلب', tone: 'warn' },
}

const KIND: Record<string, { tone: string; icon: React.ReactNode }> = {
  Submitted: { tone: 'plain', icon: <FileCheck2 size={14} /> },
  Sent: { tone: 'plain', icon: <Send size={14} /> },
  Approved: { tone: 'ok', icon: <CheckCircle2 size={14} /> },
  Rejected: { tone: 'bad', icon: <XCircle size={14} /> },
  ChangesRequested: { tone: 'warn', icon: <CornerUpLeft size={14} /> },
  Returned: { tone: 'warn', icon: <Undo2 size={14} /> },
  Completed: { tone: 'ok', icon: <CheckCircle2 size={14} /> },
}
