import { useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Trash2, Users } from 'lucide-react'
import {
  workflow, ASSIGNEE_TYPE, ASSIGNMENT_TYPE, REJECT_BEHAVIOR, RETURN_RESUME, STAGE_TYPE, WF_ACTION,
  type CreateStageRequest, type WorkflowDefinitionListItem,
} from '../../lib/workflow'
import { diwan } from '../../lib/diwan'
import { organization } from '../../lib/organization'
import { documents } from '../../lib/documents'
import { LabeledInput, LabeledSelect, LabeledTextarea } from '../../components/ui/labeled-input'
import { Card, CardContent, CardHeader } from '../../components/ui/card'
import WorkflowCanvas, {
  STAGE_COLORS, DEFAULT_STAGE_COLOR, type CanvasStage, type CanvasMenuActions, type StageColor,
} from '../../components/workflow/WorkflowCanvas'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import '../../styles/workflow.css'
import '../../styles/forms.css'

/* مصمّم سير العمل — the administrator draws a route here and the engine runs it. No workflow is
   written in code: a new kind of request is a new route on this canvas, bound to a تصنيف البريد,
   and composing internal mail under that classification starts it. */

type StageDraft = CreateStageRequest & { key: string }

/** Cycled when a stage is created; the administrator overrides it per stage. */
const PALETTE = Object.keys(STAGE_COLORS) as (keyof typeof STAGE_COLORS)[]

const ACTION_CHOICES: { value: number; label: string }[] = [
  { value: WF_ACTION.Approve, label: 'اعتماد' },
  { value: WF_ACTION.Reject, label: 'رفض' },
  { value: WF_ACTION.RequestChanges, label: 'إعادة للتعديل' },
  { value: WF_ACTION.Return, label: 'إرجاع لمرحلة سابقة' },
  { value: WF_ACTION.Comment, label: 'تعليق' },
  { value: WF_ACTION.Close, label: 'إغلاق' },
]

const DEFAULT_ACTIONS = WF_ACTION.Approve | WF_ACTION.Reject | WF_ACTION.RequestChanges | WF_ACTION.Comment

let seq = 0
function newStage(order: number): StageDraft {
  seq += 1
  return {
    key: `s${seq}`,
    name: '',
    order,
    assigneeType: ASSIGNEE_TYPE.Position,
    assignmentType: ASSIGNMENT_TYPE.Static,
    responseHours: 48,
    allowedActions: DEFAULT_ACTIONS,
    isFinal: false,
    rejectBehavior: REJECT_BEHAVIOR.StopAndReturnToRequester,
    returnResume: RETURN_RESUME.ResumeAtSameStage,
    type: STAGE_TYPE.Sequential,
    // Each stage drawn gets the next colour in the palette, so a fresh route is already legible
    // before anyone opens the picker — and two adjacent stages never land on the same tile.
    color: PALETTE[(order - 1) % PALETTE.length],
  }
}

export default function WorkflowDesignerPage() {
  const toast = useToast()
  const { confirm } = useConfirmDialog()
  const qc = useQueryClient()

  const { data: defs } = useQuery({ queryKey: ['workflow', 'definitions'], queryFn: workflow.definitions })
  const { data: categories } = useQuery({
    queryKey: ['diwan', 'categories', 'internal'],
    queryFn: () => diwan.categories(),
  })
  const { data: positions } = useQuery({ queryKey: ['org', 'positions'], queryFn: () => organization.positions() })
  const { data: orgUnits } = useQuery({ queryKey: ['org', 'units'], queryFn: () => documents.orgUnits() })
  const { data: users } = useQuery({ queryKey: ['org', 'users'], queryFn: () => documents.users() })

  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [stages, setStages] = useState<StageDraft[]>(() => [newStage(1)])
  const [selected, setSelected] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  // Binding a route to an outgoing-only classification would build a trigger that can never fire.
  const usableCategories = useMemo(
    () => (categories ?? []).filter((c) => c.isActive && c.scope !== 'External'),
    [categories],
  )
  const triggerLabel = usableCategories.find((c) => String(c.id) === categoryId)?.name ?? ''

  const patch = (key: string, next: Partial<StageDraft>) =>
    setStages((list) => list.map((s) => (s.key === key ? { ...s, ...next } : s)))

  const insertAfter = (afterKey: string | null) =>
    setStages((list) => {
      const at = afterKey === null ? list.length : list.findIndex((s) => s.key === afterKey) + 1
      const stage = newStage(at + 1)
      const next = [...list.slice(0, at), stage, ...list.slice(at)]
      setSelected(stage.key)
      return next.map((s, i) => ({ ...s, order: i + 1 }))
    })

  const removeStage = (key: string) =>
    setStages((list) => {
      setSelected(null)
      return list.filter((s) => s.key !== key).map((s, i) => ({ ...s, order: i + 1 }))
    })

  const insertBefore = (beforeKey: string) =>
    setStages((list) => {
      const at = Math.max(0, list.findIndex((s) => s.key === beforeKey))
      const stage = newStage(at + 1)
      const next = [...list.slice(0, at), stage, ...list.slice(at)]
      setSelected(stage.key)
      return next.map((s, i) => ({ ...s, order: i + 1 }))
    })

  /** A copy of a configured stage, dropped in right after it — the quickest way to build the
   *  second of two near-identical approvals without walking the whole form again. */
  const duplicate = (key: string) =>
    setStages((list) => {
      const at = list.findIndex((s) => s.key === key)
      if (at < 0) return list
      const copy: StageDraft = { ...list[at], key: `s${seq += 1}`, name: `${list[at].name} (نسخة)` }
      const next = [...list.slice(0, at + 1), copy, ...list.slice(at + 1)]
      setSelected(copy.key)
      return next.map((s, i) => ({ ...s, order: i + 1 }))
    })

  /** Drag-and-drop from the canvas: pull the dragged stage out and drop it where the target sits. */
  const reorder = (fromKey: string, toKey: string) =>
    setStages((list) => {
      const from = list.findIndex((s) => s.key === fromKey)
      const to = list.findIndex((s) => s.key === toKey)
      if (from < 0 || to < 0 || from === to) return list
      const next = [...list]
      const [moved] = next.splice(from, 1)
      next.splice(to, 0, moved)
      return next.map((s, i) => ({ ...s, order: i + 1 }))
    })

  const move = (key: string, delta: number) =>
    setStages((list) => {
      const i = list.findIndex((s) => s.key === key)
      const j = i + delta
      if (i < 0 || j < 0 || j >= list.length) return list
      const next = [...list]
      ;[next[i], next[j]] = [next[j], next[i]]
      return next.map((s, k) => ({ ...s, order: k + 1 }))
    })

  /** Everything the canvas's right-click menu can change, in one object. The menu edits the same
   *  draft the settings panel does, so a change made either way shows in both immediately. */
  const canvasMenu: CanvasMenuActions = {
    insertBefore,
    duplicate,
    move,
    setColor: (key: string, color: StageColor) => patch(key, { color }),
    setType: (key: string, type: number) => setStages((list) => list.map((s) => (s.key === key
      // Same rule as the settings panel: the type decides which routing field means anything, so
      // switching clears the one that no longer applies.
      ? {
        ...s,
        type,
        parallelGroupId: type === STAGE_TYPE.Parallel ? (s.parallelGroupId ?? 1) : null,
        conditionExpression: type === STAGE_TYPE.Conditional ? s.conditionExpression : null,
        minimumApprovals: type === STAGE_TYPE.Parallel ? s.minimumApprovals : null,
      }
      : s))),
    setFinal: (key: string, isFinal: boolean) => patch(key, { isFinal }),
  }

  const toggleAction = (key: string, bit: number, on: boolean) =>
    setStages((list) => list.map((s) => (s.key === key
      ? { ...s, allowedActions: on ? s.allowedActions | bit : s.allowedActions & ~bit }
      : s)))

  /** The stage list as the canvas needs it, with assignees resolved to names. */
  const canvasStages: CanvasStage[] = useMemo(() => stages.map((s) => ({
    key: s.key,
    name: s.name,
    assignmentType: s.assignmentType,
    assigneeType: s.assigneeType,
    parallelGroupId: s.parallelGroupId,
    minimumApprovals: s.minimumApprovals,
    conditionExpression: s.conditionExpression,
    isFinal: s.isFinal,
    color: s.color,
    type: s.type,
    assigneeName:
      (s.assignmentType ?? 0) !== ASSIGNMENT_TYPE.Static ? null
        : s.assigneeType === ASSIGNEE_TYPE.Position
          ? positions?.find((p) => p.id === s.assigneePositionId)?.title ?? null
          : s.assigneeType === ASSIGNEE_TYPE.OrgUnit
            ? orgUnits?.find((u) => u.id === s.assigneeOrgUnitId)?.name ?? null
            : s.assigneeType === ASSIGNEE_TYPE.User
              ? users?.find((u) => u.id === s.assigneeUserId)?.fullName ?? null
              : null,
  })), [stages, positions, orgUnits, users])

  const current = stages.find((s) => s.key === selected) ?? null
  const currentIndex = stages.findIndex((s) => s.key === selected)

  async function save() {
    if (!name.trim()) { toast.error('اسم سير العمل مطلوب.'); return }
    if (!categoryId) { toast.error('اختر تصنيف البريد الذي يُشغّل سير العمل.'); return }
    if (!stages.length) { toast.error('أضف مرحلة واحدة على الأقل.'); return }
    const unnamed = stages.find((s) => !s.name.trim())
    if (unnamed) { setSelected(unnamed.key); toast.error('كل مرحلة تحتاج اسمًا.'); return }
    if (!stages.some((s) => s.isFinal)) {
      if (!(await confirm({
        message: 'لم تُعلَّم أي مرحلة كنهائية. سينتهي سير العمل بعد آخر مرحلة. المتابعة؟',
      }))) return
    }

    setSaving(true)
    try {
      await workflow.createDefinition({
        name: name.trim(),
        description: description.trim() || null,
        triggerModule: 'InternalMail',
        mailCategoryId: Number(categoryId),
        isActive: true,
        // The draft key is a UI concern for React reconciliation; the API takes the stage as configured.
        stages: stages.map((s) => {
          const rest: Partial<StageDraft> = { ...s }
          delete rest.key
          return rest as CreateStageRequest
        }),
      })
      toast.success('حُفظ المسار، وسيبدأ تلقائيًا مع أول مراسلة بهذا التصنيف.')
      setName(''); setDescription(''); setCategoryId('')
      setStages([newStage(1)]); setSelected(null)
      qc.invalidateQueries({ queryKey: ['workflow', 'definitions'] })
    } catch {
      toast.error('تعذّر حفظ المسار.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div>
      <header className="page__head">
        <div>
          <h1>مصمّم سير العمل</h1>
          <p className="muted">
            ارسم المسار هنا وينفّذه النظام تلقائيًا عبر المراسلات — لا يحوّل أحد المعاملة يدويًا.
          </p>
        </div>
        <div className="page__headactions">
          <button type="button" className="btn btn-primary" disabled={saving} onClick={save}>
            حفظ المسار
          </button>
        </div>
      </header>

      <Card>
        <CardHeader><h2 className="wfd__h2">مسار جديد</h2></CardHeader>
        <CardContent>
          <div className="wfd__grid">
            <LabeledInput label="اسم سير العمل" value={name} required
              onChange={(e) => setName(e.target.value)} />
            <LabeledSelect label="يبدأ عند مراسلة بتصنيف" value={categoryId} required
              onChange={(e) => setCategoryId(e.target.value)}
              hint="الشرط: بريد داخلي جديد + هذا التصنيف ⇒ يبدأ المسار.">
              <option value="">— اختر —</option>
              {usableCategories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </LabeledSelect>
            <LabeledTextarea label="وصف" rows={1} value={description}
              onChange={(e) => setDescription(e.target.value)} />
          </div>

          <div className="wfd__canvaswrap">
            <WorkflowCanvas
              stages={canvasStages}
              triggerLabel={triggerLabel}
              selectedKey={selected}
              onSelect={setSelected}
              onInsertAfter={insertAfter}
              onReorder={reorder}
              onDelete={removeStage}
              menu={canvasMenu}
            />
          </div>

          {!current ? (
            <p className="wfd__empty">اختر مرحلة من المخطّط لتحرير إعداداتها.</p>
          ) : (
            <section className="wfd__stage">
              <div className="wfd__panelhead">
                <span className="wfd__num">{currentIndex + 1}</span>
                <h3>إعدادات المرحلة</h3>
                <span style={{ marginInlineStart: 'auto', display: 'flex', gap: '.4rem' }}>
                  <button type="button" className="btn btn-ghost btn-sm" title="تقديمها في الترتيب"
                    disabled={currentIndex === 0} onClick={() => move(current.key, -1)}>▲</button>
                  <button type="button" className="btn btn-ghost btn-sm" title="تأخيرها في الترتيب"
                    disabled={currentIndex === stages.length - 1} onClick={() => move(current.key, 1)}>▼</button>
                  <button type="button" className="btn btn-seal btn-sm" title="حذف المرحلة"
                    onClick={() => removeStage(current.key)}>
                    <Trash2 size={14} aria-hidden />
                  </button>
                </span>
              </div>

              {/* Colour first: it is what the administrator will look for on the canvas before
                  reading a single label, and it costs one click. */}
              <div className="wfd__swatches" role="radiogroup" aria-label="لون المرحلة">
                <span className="wfd__swatches__label">لون المرحلة</span>
                {(Object.keys(STAGE_COLORS) as (keyof typeof STAGE_COLORS)[]).map((k) => {
                  const on = (current.color ?? DEFAULT_STAGE_COLOR) === k
                  return (
                    <button
                      key={k}
                      type="button"
                      role="radio"
                      aria-checked={on}
                      className={`wfd__swatch ${on ? 'is-on' : ''}`}
                      style={{ background: STAGE_COLORS[k].bg, color: STAGE_COLORS[k].fg }}
                      title={STAGE_COLORS[k].label}
                      onClick={() => patch(current.key, { color: k })}
                    >
                      {on ? '●' : ''}
                    </button>
                  )
                })}
              </div>

              <div className="wfd__grid">
                <LabeledInput label="اسم المرحلة" value={current.name} required
                  onChange={(e) => patch(current.key, { name: e.target.value })} />

                <LabeledSelect label="نوع المرحلة" value={String(current.type ?? STAGE_TYPE.Sequential)}
                  onChange={(e) => {
                    const type = Number(e.target.value)
                    // The type decides which routing field means anything, so switching clears the
                    // other one — a stage left with both a group and a condition reads as two
                    // different kinds of step at once.
                    patch(current.key, {
                      type,
                      parallelGroupId: type === STAGE_TYPE.Parallel ? (current.parallelGroupId ?? 1) : null,
                      conditionExpression: type === STAGE_TYPE.Conditional ? current.conditionExpression : null,
                      minimumApprovals: type === STAGE_TYPE.Parallel ? current.minimumApprovals : null,
                    })
                  }}
                  hint="تسلسلية: تبدأ بعد سابقتها · متوازية: مع مراحل مجموعتها · شرطية: تُتخطّى إن لم يتحقق شرطها.">
                  <option value={STAGE_TYPE.Sequential}>تسلسلية</option>
                  <option value={STAGE_TYPE.Parallel}>متوازية</option>
                  <option value={STAGE_TYPE.Conditional}>شرطية</option>
                </LabeledSelect>

                <LabeledSelect label="المسؤول" value={String(current.assignmentType ?? 0)}
                  onChange={(e) => patch(current.key, { assignmentType: Number(e.target.value) })}
                  hint="الديناميكي يُحسب من بيانات مُقدّم الطلب، فيعمل المسار لكل الموظفين بلا تكرار.">
                  <option value={ASSIGNMENT_TYPE.Static}>محدَّد (وظيفة/إدارة/شخص)</option>
                  <option value={ASSIGNMENT_TYPE.DepartmentHead}>مدير إدارة مُقدّم الطلب</option>
                  <option value={ASSIGNMENT_TYPE.OrgHierarchy}>المستوى الأعلى في الهيكل</option>
                </LabeledSelect>

                {(current.assignmentType ?? 0) === ASSIGNMENT_TYPE.Static && (
                  <LabeledSelect label="نوع الجهة" value={String(current.assigneeType)}
                    onChange={(e) => patch(current.key, {
                      assigneeType: Number(e.target.value),
                      assigneePositionId: null, assigneeOrgUnitId: null, assigneeUserId: null,
                    })}>
                    <option value={ASSIGNEE_TYPE.Position}>وظيفة (المقعد)</option>
                    <option value={ASSIGNEE_TYPE.OrgUnit}>إدارة</option>
                    <option value={ASSIGNEE_TYPE.User}>شخص بعينه</option>
                    <option value={ASSIGNEE_TYPE.DirectManager}>المدير المباشر</option>
                  </LabeledSelect>
                )}

                {(current.assignmentType ?? 0) === ASSIGNMENT_TYPE.Static
                  && current.assigneeType === ASSIGNEE_TYPE.Position && (
                  <LabeledSelect label="الوظيفة" value={String(current.assigneePositionId ?? '')}
                    onChange={(e) => patch(current.key, { assigneePositionId: Number(e.target.value) || null })}>
                    <option value="">— اختر —</option>
                    {(positions ?? []).map((p) => (
                      <option key={p.id} value={p.id}>{p.title} — {p.orgUnitName}</option>
                    ))}
                  </LabeledSelect>
                )}

                {(current.assignmentType ?? 0) === ASSIGNMENT_TYPE.Static
                  && current.assigneeType === ASSIGNEE_TYPE.OrgUnit && (
                  <LabeledSelect label="الإدارة" value={String(current.assigneeOrgUnitId ?? '')}
                    onChange={(e) => patch(current.key, { assigneeOrgUnitId: Number(e.target.value) || null })}>
                    <option value="">— اختر —</option>
                    {(orgUnits ?? []).map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
                  </LabeledSelect>
                )}

                {(current.assignmentType ?? 0) === ASSIGNMENT_TYPE.Static
                  && current.assigneeType === ASSIGNEE_TYPE.User && (
                  <LabeledSelect label="الموظف" value={String(current.assigneeUserId ?? '')}
                    onChange={(e) => patch(current.key, { assigneeUserId: Number(e.target.value) || null })}>
                    <option value="">— اختر —</option>
                    {(users ?? []).map((u) => <option key={u.id} value={u.id}>{u.fullName}</option>)}
                  </LabeledSelect>
                )}

                <LabeledInput label="مهلة الرد (ساعات)" type="number" min={1}
                  value={String(current.responseHours)}
                  onChange={(e) => patch(current.key, { responseHours: Number(e.target.value) || 24 })} />

                <LabeledSelect label="عند الرفض" value={String(current.rejectBehavior ?? 0)}
                  onChange={(e) => patch(current.key, { rejectBehavior: Number(e.target.value) })}>
                  <option value={REJECT_BEHAVIOR.StopAndReturnToRequester}>
                    إيقاف المسار وإبلاغ مُقدّم الطلب بالسبب
                  </option>
                  <option value={REJECT_BEHAVIOR.ReturnToPreviousStage}>الإرجاع إلى المرحلة السابقة</option>
                </LabeledSelect>

                <LabeledSelect label="بعد التعديل يستأنف من" value={String(current.returnResume ?? 0)}
                  onChange={(e) => patch(current.key, { returnResume: Number(e.target.value) })}>
                  <option value={RETURN_RESUME.ResumeAtSameStage}>هذه المرحلة (تبقى الاعتمادات السابقة)</option>
                  <option value={RETURN_RESUME.RestartFromStart}>البداية</option>
                </LabeledSelect>

                {/* Shown only where the stage's own type gives them meaning. */}
                {(current.type ?? STAGE_TYPE.Sequential) === STAGE_TYPE.Parallel && (
                  <LabeledInput label="مجموعة التوازي" type="number" min={1}
                    value={String(current.parallelGroupId ?? '')}
                    onChange={(e) => patch(current.key, { parallelGroupId: Number(e.target.value) || null })}
                    hint="المراحل المتجاورة التي تحمل نفس الرقم تُرسَل معًا وتظهر متفرّعة في المخطّط." />
                )}

                {(current.type ?? STAGE_TYPE.Sequential) === STAGE_TYPE.Parallel && (
                  <LabeledInput label="النصاب (مثال: 2 من 3)" type="number" min={1}
                    value={String(current.minimumApprovals ?? '')}
                    onChange={(e) => patch(current.key, { minimumApprovals: Number(e.target.value) || null })}
                    hint="اتركه فارغًا ليلزم اعتماد الجميع." />
                )}

                {(current.type ?? STAGE_TYPE.Sequential) === STAGE_TYPE.Conditional && (
                  <LabeledInput label="شرط التنفيذ (JSON)"
                    value={current.conditionExpression ?? ''}
                    onChange={(e) => patch(current.key, { conditionExpression: e.target.value || null })}
                    hint='مثال: {"field":"Amount","operator":"greaterThan","value":5000}' />
                )}
              </div>

              <fieldset className="wfd__actions">
                <legend>الإجراءات المتاحة في هذه المرحلة</legend>
                {ACTION_CHOICES.map((a) => (
                  <label key={a.value} className="wfd__check">
                    <input type="checkbox" checked={(current.allowedActions & a.value) !== 0}
                      onChange={(e) => toggleAction(current.key, a.value, e.target.checked)} />
                    {a.label}
                  </label>
                ))}
                <label className="wfd__check wfd__check--final">
                  <input type="checkbox" checked={current.isFinal}
                    onChange={(e) => patch(current.key, { isFinal: e.target.checked })} />
                  مرحلة نهائية (ينتهي المسار عندها)
                </label>
              </fieldset>
            </section>
          )}

          <div className="wfd__foot">
            <span className="wfd__hint">
              <Users size={14} aria-hidden />
              تنتهي كل مسارات المراسلات بإعادة النتيجة تلقائيًا إلى مُقدّم الطلب.
            </span>
            <button type="button" className="btn btn-primary" disabled={saving} onClick={save}>
              حفظ المسار
            </button>
          </div>
        </CardContent>
      </Card>

      <Card className="wfd__list">
        <CardHeader><h2 className="wfd__h2">المسارات المعرّفة</h2></CardHeader>
        <CardContent>
          {!defs?.length
            ? <p className="muted">لا توجد مسارات بعد.</p>
            : (
              <table className="wfd__table">
                <thead>
                  <tr>
                    <th>الاسم</th><th>المُشغِّل</th><th>تصنيف البريد</th>
                    <th>المراحل</th><th>الإصدار</th><th>الحالة</th>
                  </tr>
                </thead>
                <tbody>
                  {defs.map((d: WorkflowDefinitionListItem) => (
                    <tr key={d.id}>
                      <td>{d.name}</td>
                      <td><span className="badge">{d.triggerModule}</span></td>
                      <td>{d.mailCategoryName ?? '—'}</td>
                      <td className="wf-step">{d.stageCount}</td>
                      <td className="wf-step">{d.version}</td>
                      <td>
                        <span className={`badge ${d.isActive ? 'public' : 'internal'}`}>
                          {d.isActive ? 'مفعّل' : 'موقوف'}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
        </CardContent>
      </Card>
    </div>
  )
}
