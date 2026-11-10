import { useEffect, useState, type FormEvent } from 'react'
import { motion } from 'motion/react'
import { designatedCommunityApi, type DesignatedCommunity } from '../lib/packages'
import { LabeledInput, LabeledTextarea } from './ui/labeled-input'
import { useToast } from './toast'

export default function DesignatedCommunitySettings() {
  const toast = useToast()
  const [form, setForm] = useState<DesignatedCommunity>({ name: '', description: '', renderingExpectations: '' })
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    designatedCommunityApi.get()
      .then((c) => setForm({ name: c.name ?? '', description: c.description ?? '', renderingExpectations: c.renderingExpectations ?? '' }))
      .catch(() => {})
  }, [])

  async function save(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    try { await designatedCommunityApi.update(form); toast.success('تم حفظ المجتمع المستهدف') }
    catch { toast.error('تعذّر الحفظ') }
    finally { setBusy(false) }
  }

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }}>
      <h3 className="detail-h3">المجتمع المستهدف (Designated Community)</h3>
      <p className="muted">تعريف الجهة المستفيدة من الأرشيف ومتطلبات عرض الوثائق على المدى الطويل.</p>
      <form className="form-grid" onSubmit={save}>
        <LabeledInput label="الاسم" containerClassName="field--wide" value={form.name}
          onChange={(e) => setForm({ ...form, name: e.target.value })} />
        <LabeledInput label="الوصف" containerClassName="field--wide" value={form.description ?? ''}
          onChange={(e) => setForm({ ...form, description: e.target.value })} />
        <LabeledTextarea label="متطلبات العرض" containerClassName="field--wide" rows={2}
          value={form.renderingExpectations ?? ''}
          onChange={(e) => setForm({ ...form, renderingExpectations: e.target.value })} />
        <div className="form-actions"><button className="btn btn-primary" disabled={busy}>حفظ</button></div>
      </form>
    </motion.section>
  )
}
