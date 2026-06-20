import { useEffect, useState, useCallback } from 'react'
import { motion, AnimatePresence } from 'motion/react'
import { Lock, Pencil, Trash2, Plus, Check, X, GripVertical, Users } from 'lucide-react'
import {
  classificationApi,
  type ClassificationType,
  type UpsertClassification,
  COLOR_SWATCHES,
} from '../lib/classification'
import { usersApi, type AdminRole } from '../lib/users'
import { useToast } from './toast'
import '../pages/settings/settings.css'
import './classificationsettings.css'

// ── Inline edit form ────────────────────────────────────────────────────────
interface EditFormProps {
  initial?: Partial<UpsertClassification> & { roleIds?: number[] }
  roles: AdminRole[]
  onSave: (v: UpsertClassification, roleIds: number[]) => Promise<void>
  onCancel: () => void
  busy: boolean
  isNew?: boolean
}

function EditForm({ initial, roles, onSave, onCancel, busy, isNew }: EditFormProps) {
  const [nameAr,      setNameAr]      = useState(initial?.nameAr ?? '')
  const [nameEn,      setNameEn]      = useState(initial?.nameEn ?? '')
  const [description, setDescription] = useState(initial?.description ?? '')
  const [color,       setColor]       = useState(initial?.color ?? '#6b7280')
  const [sortOrder,   setSortOrder]   = useState(initial?.sortOrder ?? 99)
  const [isActive,    setIsActive]    = useState(initial?.isActive ?? true)
  const [selectedRoles, setSelectedRoles] = useState<Set<number>>(
    new Set(initial?.roleIds ?? [])
  )

  function toggleRole(id: number) {
    setSelectedRoles((prev) => {
      const next = new Set(prev)
      next.has(id) ? next.delete(id) : next.add(id)
      return next
    })
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    if (!nameAr.trim()) return
    await onSave(
      { nameAr: nameAr.trim(), nameEn: nameEn.trim() || null,
        description: description.trim() || null, color, sortOrder, isActive },
      [...selectedRoles]
    )
  }

  return (
    <form className="clf-form" onSubmit={submit}>
      <div className="clf-form__row">
        {/* Color section */}
        <div className="clf-color-wrap">
          <div className="clf-color-preview" style={{ background: color }} />
          <div className="clf-swatches">
            {COLOR_SWATCHES.map((s) => (
              <button
                key={s.hex}
                type="button"
                className={`clf-swatch ${color === s.hex ? 'is-selected' : ''}`}
                style={{ background: s.hex }}
                title={s.label}
                onClick={() => setColor(s.hex)}
              />
            ))}
            <label className="clf-swatch clf-swatch--custom" title="لون مخصص">
              <input
                type="color" value={color}
                onChange={(e) => setColor(e.target.value)}
              />
              <span>#</span>
            </label>
          </div>
        </div>

        {/* Name / meta fields */}
        <div className="clf-form__fields">
          <label className="clf-field">
            <span>الاسم بالعربية *</span>
            <input
              value={nameAr}
              onChange={(e) => setNameAr(e.target.value)}
              placeholder="مثال: للاستخدام الرسمي فقط"
              required
            />
          </label>
          <label className="clf-field">
            <span>الاسم بالإنجليزية</span>
            <input
              dir="ltr"
              value={nameEn}
              onChange={(e) => setNameEn(e.target.value)}
              placeholder="e.g. Official Use Only"
            />
          </label>
          <label className="clf-field">
            <span>الوصف</span>
            <input
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="نبذة مختصرة عن هذا التصنيف"
            />
          </label>
          <div className="clf-form__meta">
            <label className="clf-field clf-field--sm">
              <span>الترتيب</span>
              <input
                type="number" min={0} max={999}
                value={sortOrder}
                onChange={(e) => setSortOrder(Number(e.target.value))}
                style={{ width: 70 }}
              />
            </label>
            <label className="clf-active-toggle">
              <input
                type="checkbox"
                checked={isActive}
                onChange={(e) => setIsActive(e.target.checked)}
              />
              <span>مفعّل</span>
            </label>
          </div>
        </div>
      </div>

      {/* Role association */}
      {roles.length > 0 && (
        <div className="clf-roles-section">
          <p className="clf-roles-label">
            <Users size={13} />
            الأدوار المسموح لها باستخدام هذا التصنيف
          </p>
          <div className="clf-roles-grid">
            {roles.map((r) => {
              const checked = selectedRoles.has(r.id)
              return (
                <label key={r.id} className={`clf-role-chip ${checked ? 'is-checked' : ''}`}>
                  <input
                    type="checkbox"
                    checked={checked}
                    onChange={() => toggleRole(r.id)}
                  />
                  <span>{r.name}</span>
                </label>
              )
            })}
          </div>
          {selectedRoles.size === 0 && (
            <p className="clf-roles-warn">لم تُحدَّد أي أدوار — لن يستطيع أحد استخدام هذا التصنيف.</p>
          )}
        </div>
      )}

      <div className="clf-form__actions">
        <button className="btn btn-seal btn-sm" disabled={busy || !nameAr.trim()}>
          <Check size={14} />
          {isNew ? 'إضافة التصنيف' : 'حفظ التعديلات'}
        </button>
        <button type="button" className="btn btn-ghost btn-sm" onClick={onCancel}>
          <X size={14} />
          إلغاء
        </button>
      </div>
    </form>
  )
}

// ── Main component ──────────────────────────────────────────────────────────
export default function ClassificationSettings() {
  const toast = useToast()
  const [items, setItems]           = useState<ClassificationType[]>([])
  const [roles, setRoles]           = useState<AdminRole[]>([])
  const [editingId, setEditingId]   = useState<number | null>(null)
  const [creating, setCreating]     = useState(false)
  const [busy, setBusy]             = useState(false)

  const load = useCallback(async () => {
    try {
      const [types, roleList] = await Promise.all([
        classificationApi.list(),
        usersApi.roles(),
      ])
      setItems(types)
      setRoles(roleList)
    } catch {
      toast.error('تعذّر تحميل البيانات')
    }
  }, [toast])

  useEffect(() => { load() }, [load])

  // Helper: role name lookup
  function roleName(id: number) {
    return roles.find((r) => r.id === id)?.name ?? `#${id}`
  }

  async function handleSave(id: number, data: UpsertClassification, roleIds: number[]) {
    setBusy(true)
    try {
      await Promise.all([
        classificationApi.update(id, data),
        classificationApi.setRoles(id, roleIds),
      ])
      setEditingId(null)
      await load()
      toast.success('تم حفظ التصنيف')
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } } }
      toast.error(ax.response?.data?.error ?? 'تعذّر الحفظ')
    } finally { setBusy(false) }
  }

  async function handleCreate(data: UpsertClassification, roleIds: number[]) {
    setBusy(true)
    try {
      const created = await classificationApi.create(data)
      await classificationApi.setRoles(created.id, roleIds)
      setCreating(false)
      await load()
      toast.success('تم إضافة التصنيف')
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } } }
      toast.error(ax.response?.data?.error ?? 'تعذّر الإضافة')
    } finally { setBusy(false) }
  }

  async function handleDelete(item: ClassificationType) {
    if (!confirm(`حذف التصنيف «${item.nameAr}»؟`)) return
    setBusy(true)
    try {
      await classificationApi.delete(item.id)
      await load()
      toast.success('تم حذف التصنيف')
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } } }
      toast.error(ax.response?.data?.error ?? 'تعذّر الحذف')
    } finally { setBusy(false) }
  }

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }}>
      <div className="set-head">
        <div>
          <h3 className="detail-h3">أنواع التصنيف الأمني</h3>
          <p className="clf-subtitle">
            مستويات السرية المتاحة في النظام — لكل تصنيف الأدوار المسموح لها باستخدامه
          </p>
        </div>
        <button
          className="btn btn-primary clf-add-btn"
          onClick={() => { setCreating(true); setEditingId(null) }}
          disabled={creating}
        >
          <Plus size={15} /> إضافة نوع
        </button>
      </div>

      {/* New-item form */}
      <AnimatePresence>
        {creating && (
          <motion.div
            className="clf-new-form"
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={{ opacity: 0, height: 0 }}
          >
            <p className="clf-new-label">
              <Plus size={14} /> نوع تصنيف جديد
            </p>
            <EditForm
              isNew
              roles={roles}
              onSave={handleCreate}
              onCancel={() => setCreating(false)}
              busy={busy}
            />
          </motion.div>
        )}
      </AnimatePresence>

      {/* Classification list */}
      <ul className="clf-list">
        {items.map((item) => (
          <li key={item.id} className={`clf-item ${!item.isActive ? 'clf-item--inactive' : ''}`}>
            {editingId === item.id ? (
              <motion.div className="clf-item__edit" initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
                <div className="clf-item__edit-head">
                  <span className="clf-dot" style={{ background: item.color }} />
                  <span className="clf-item__name">{item.nameAr}</span>
                  <span className="clf-edit-label">جارٍ التعديل…</span>
                </div>
                <EditForm
                  roles={roles}
                  initial={{
                    nameAr: item.nameAr, nameEn: item.nameEn ?? '',
                    description: item.description ?? '',
                    color: item.color, sortOrder: item.sortOrder,
                    isActive: item.isActive, roleIds: item.roleIds,
                  }}
                  onSave={(data, roleIds) => handleSave(item.id, data, roleIds)}
                  onCancel={() => setEditingId(null)}
                  busy={busy}
                />
              </motion.div>
            ) : (
              <div className="clf-item__row">
                <GripVertical size={14} className="clf-grip" />
                <span className="clf-dot" style={{ background: item.color }} />
                <div className="clf-item__info">
                  <span className="clf-item__name">
                    {item.nameAr}
                    {item.nameEn && <span className="clf-item__name-en">{item.nameEn}</span>}
                    {item.isSystem && (
                      <span className="clf-system-badge" title="مدمج في النظام">
                        <Lock size={10} /> مدمج
                      </span>
                    )}
                    {!item.isActive && <span className="clf-inactive-badge">معطّل</span>}
                  </span>
                  {item.description && <span className="clf-item__desc">{item.description}</span>}
                  {/* Role badges */}
                  {item.roleIds.length > 0 ? (
                    <span className="clf-item__roles">
                      <Users size={11} className="clf-roles-icon" />
                      {item.roleIds.map((rid) => (
                        <span key={rid} className="clf-role-tag">{roleName(rid)}</span>
                      ))}
                    </span>
                  ) : (
                    <span className="clf-no-roles">لا توجد أدوار مرتبطة</span>
                  )}
                </div>
                <span className="clf-order">#{item.sortOrder}</span>
                <div className="clf-item__actions">
                  <button
                    className="clf-action-btn"
                    title="تعديل"
                    onClick={() => { setEditingId(item.id); setCreating(false) }}
                    disabled={busy}
                  >
                    <Pencil size={14} />
                  </button>
                  {!item.isSystem && (
                    <button
                      className="clf-action-btn clf-action-btn--del"
                      title="حذف"
                      onClick={() => handleDelete(item)}
                      disabled={busy}
                    >
                      <Trash2 size={14} />
                    </button>
                  )}
                </div>
              </div>
            )}
          </li>
        ))}
      </ul>

      {items.length === 0 && (
        <p className="muted" style={{ textAlign: 'center', padding: '2rem' }}>
          لا توجد أنواع تصنيف. اضغط «إضافة نوع» لبدء الإعداد.
        </p>
      )}
    </motion.section>
  )
}
