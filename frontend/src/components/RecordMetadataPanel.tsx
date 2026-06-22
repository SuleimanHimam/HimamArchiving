import { useState } from 'react'
import { motion } from 'motion/react'
import {
  metadataApi, type RecordMetadata,
  AGENT_ROLE_LABELS, AGENT_KIND_LABELS, REL_TYPE_LABELS,
} from '../lib/metadata'
import { useToast } from './toast'
import '../pages/documents/documents.css'

export default function RecordMetadataPanel({ docId }: { docId: number }) {
  const toast = useToast()
  const [data, setData] = useState<RecordMetadata | null>(null)
  const [open, setOpen] = useState(false)
  const [busy, setBusy] = useState(false)

  async function load() {
    setBusy(true)
    try { setData(await metadataApi.get(docId)); setOpen(true) }
    catch { toast.error('تعذّر تحميل البيانات الوصفية') }
    finally { setBusy(false) }
  }

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.16 }}>
      <div className="attach-header">
        <span className="kicker">METADATA · البيانات الوصفية</span>
        {!open && <button className="btn btn-ghost btn-sm" onClick={load} disabled={busy}>{busy ? '…' : 'عرض'}</button>}
      </div>

      {open && data && (
        <div className="meta-cols">
          <div>
            <h4 className="detail-h3">الأطراف (Agents)</h4>
            <ul className="meta-list">
              {data.agents.length === 0 && <li className="muted">—</li>}
              {data.agents.map((a) => (
                <li key={a.id}>
                  <span className="meta-role">{AGENT_ROLE_LABELS[a.role] ?? a.role}</span>
                  <span>{a.agentName}</span>
                  <span className="muted mono">{AGENT_KIND_LABELS[a.agentKind] ?? a.agentKind}</span>
                </li>
              ))}
            </ul>
          </div>

          <div>
            <h4 className="detail-h3">العلاقات (Relationships)</h4>
            <ul className="meta-list">
              {data.relationships.length === 0 && <li className="muted">—</li>}
              {data.relationships.map((r) => (
                <li key={r.id}>
                  <span className="meta-role">{REL_TYPE_LABELS[r.type] ?? r.type}</span>
                  <span>{r.targetTitle ?? `${r.targetType} #${r.targetId}`}</span>
                </li>
              ))}
            </ul>
          </div>

          <div>
            <h4 className="detail-h3">الأنشطة (Business activities)</h4>
            <ul className="meta-list">
              {data.activities.length === 0 && <li className="muted">—</li>}
              {data.activities.map((x) => (
                <li key={x.workflowInstanceId}>
                  <span>{x.definitionName}</span>
                  <span className="muted mono">{x.status}</span>
                </li>
              ))}
            </ul>
          </div>
        </div>
      )}
    </motion.section>
  )
}
