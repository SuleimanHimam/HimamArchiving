import { useEffect, useState, type FormEvent } from 'react'
import { motion } from 'motion/react'
import { preservationPolicyApi, type PreservationPolicy } from '../lib/packages'
import { LabeledInput, LabeledSelect } from './ui/labeled-input'
import { useToast } from './toast'

const CONFORMANCE = ['PDF/A-2B', 'PDF/A-2A', 'PDF/A-2U', 'PDF/A-3B', 'PDF/A-3A', 'PDF/A-3U']

export default function PreservationPolicySettings() {
  const toast = useToast()
  const [p, setP] = useState<PreservationPolicy | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => { preservationPolicyApi.get().then(setP).catch(() => {}) }, [])

  async function save(e: FormEvent) {
    e.preventDefault()
    if (!p) return
    setBusy(true)
    try { setP(await preservationPolicyApi.update(p)); toast.success('تم حفظ سياسة الحفظ') }
    catch { toast.error('تعذّر حفظ السياسة') }
    finally { setBusy(false) }
  }

  if (!p) return null

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }}>
      <h3 className="detail-h3">سياسة الحفظ</h3>
      <p className="muted">قواعد الحفظ طويل الأمد: صيغة الحفظ، التحويل التلقائي عند الإدخال، وخوارزمية ودورية التحقق من السلامة.</p>
      <form className="form-grid" onSubmit={save}>
        <LabeledSelect label="صيغة الحفظ (PDF/A)" value={p.targetPdfAConformance}
          onChange={(e) => setP({ ...p, targetPdfAConformance: e.target.value })}>
          {CONFORMANCE.map((c) => <option key={c} value={c}>{c}</option>)}
        </LabeledSelect>
        <LabeledInput label="دورية التحقق (أيام)" type="number" min={1} dir="ltr" value={p.fixityCadenceDays}
          onChange={(e) => setP({ ...p, fixityCadenceDays: Number(e.target.value) })} />
        <LabeledInput label="خوارزمية التحقق" dir="ltr" value={p.fixityAlgorithm}
          onChange={(e) => setP({ ...p, fixityAlgorithm: e.target.value })} />
        <LabeledSelect label="التحويل التلقائي إلى PDF/A عند الإدخال" value={p.autoNormalizeOnIngest ? '1' : '0'}
          onChange={(e) => setP({ ...p, autoNormalizeOnIngest: e.target.value === '1' })}>
          <option value="1">مُفعّل</option><option value="0">مُعطّل</option>
        </LabeledSelect>
        <LabeledInput label="الصيغ المسموح حفظها" containerClassName="field--wide" value={p.allowedPreservationFormats ?? ''}
          onChange={(e) => setP({ ...p, allowedPreservationFormats: e.target.value })} />
        <div className="form-actions"><button className="btn btn-primary" disabled={busy}>حفظ السياسة</button></div>
      </form>
    </motion.section>
  )
}
