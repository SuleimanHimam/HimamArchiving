import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { locations, type Breadcrumb } from '../lib/locations'

/** Shows a document's physical location (box breadcrumb + code) from the normalized hierarchy. */
export default function DocumentLocation({ boxId }: { boxId: number }) {
  const [bc, setBc] = useState<Breadcrumb | null>(null)
  useEffect(() => { locations.breadcrumb(boxId).then(setBc).catch(() => {}) }, [boxId])
  if (!bc) return null
  return (
    <div className="doc-card" style={{ marginBottom: '1rem', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '1rem', flexWrap: 'wrap' }}>
      <div>
        <strong>📦 الموقع الفعلي: </strong>{bc.path}
        <span className="mono muted" style={{ marginInlineStart: 8, fontSize: '.8rem' }}>{bc.locationCode}</span>
      </div>
      <Link to="/app/locations" className="btn btn-ghost btn-sm">المواقع الفعلية ↗</Link>
    </div>
  )
}
