import { useEffect, useState, type FormEvent } from 'react'
import { motion } from 'motion/react'
import { designatedCommunityApi, type DesignatedCommunity } from '../lib/packages'
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
        <label className="field field--wide"><span>الاسم</span>
          <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="مثال: موظفو المؤسسة والجهات الحكومية" /></label>
        <label className="field field--wide"><span>الوصف</span>
          <input value={form.description ?? ''} onChange={(e) => setForm({ ...form, description: e.target.value })} /></label>
        <label className="field field--wide"><span>متطلبات العرض</span>
          <textarea rows={2} value={form.renderingExpectations ?? ''} onChange={(e) => setForm({ ...form, renderingExpectations: e.target.value })}
            placeholder="مثال: قارئات PDF/A، نصوص عربية utf8mb4" /></label>
        <div className="form-actions"><button className="btn btn-primary" disabled={busy}>حفظ</button></div>
      </form>
    </motion.section>
  )
}
