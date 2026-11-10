import { useState, useMemo } from 'react'
import { Download } from 'lucide-react'
import { motion } from 'motion/react'
import { packagesApi, type DocumentPackages } from '../lib/packages'
import { formatBytes } from '../lib/documents'
import { useExcelTable, type ColumnDef } from './ui/use-excel-table'
import { ColumnHeaderMenu } from './ui/column-filter'
import { useToast } from './toast'

type RepRow = DocumentPackages['representation'][number]
import '../pages/documents/documents.css'

export default function PreservationPackagesPanel({ docId, documentNumber }: { docId: number; documentNumber: string }) {
  const toast = useToast()
  const [data, setData] = useState<DocumentPackages | null>(null)
  const [open, setOpen] = useState(false)
  const [busy, setBusy] = useState(false)

  async function load() {
    setBusy(true)
    try { setData(await packagesApi.get(docId)); setOpen(true) }
    catch { toast.error('تعذّر تحميل حزم الحفظ') }
    finally { setBusy(false) }
  }

  async function exportAip() {
    setBusy(true)
    try { await packagesApi.exportAip(docId, documentNumber); toast.success('تم تنزيل حزمة الأرشفة (AIP)') }
    catch { toast.error('تعذّر تنزيل الحزمة') }
    finally { setBusy(false) }
  }

  const columns = useMemo<ColumnDef<RepRow>[]>(() => [
    { key: 'file',   accessor: (r) => r.fileName },
    { key: 'type',   accessor: (r) => r.mimeType ?? '—' },
    { key: 'format', accessor: (r) => r.formatName ?? '—' },
    { key: 'pronom', accessor: (r) => r.pronomPuid ?? '—' },
  ], [])
  const { viewRows, getColumnProps } = useExcelTable(data?.representation ?? [], columns)

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.12 }}>
      <div className="attach-header">
        {!open
          ? <button className="btn btn-ghost btn-sm" onClick={load} disabled={busy}>{busy ? '…' : 'عرض'}</button>
          : <button className="btn btn-seal btn-sm" onClick={exportAip} disabled={busy}><Download size={15} aria-hidden /> تنزيل حزمة الأرشفة (AIP)</button>}
      </div>

      {open && data && (
        <>
          <div className="pkg-grid">
            <div className="pkg-card">
              <span className="pkg-tag">SIP</span><span className="pkg-name">المُدخَل (الأصل)</span>
              <span className="pkg-meta mono">{data.sip?.fileCount ?? 0} ملف · {data.sip ? formatBytes(data.sip.totalBytes) : '—'}</span>
            </div>
            <div className="pkg-card">
              <span className="pkg-tag">AIP</span><span className="pkg-name">الأرشيف (PDF/A)</span>
              <span className="pkg-meta mono">{data.aip?.fileCount ?? 0} ملف · {data.aip ? formatBytes(data.aip.totalBytes) : '—'}</span>
            </div>
            <div className="pkg-card">
              <span className="pkg-tag">DIP</span><span className="pkg-name">التسليم</span>
              <span className="pkg-meta mono">{data.dips.length} مرّة</span>
            </div>
          </div>

          <h4 className="detail-h3" style={{ marginTop: '1rem' }}>معلومات التمثيل (Representation Information)</h4>
          <div className="table-scroll">
            <table className="reg-table">
              <thead><tr><th><ColumnHeaderMenu label="الملف" {...getColumnProps('file')} /></th><th><ColumnHeaderMenu label="النوع" {...getColumnProps('type')} /></th><th><ColumnHeaderMenu label="الصيغة" {...getColumnProps('format')} /></th><th><ColumnHeaderMenu label="PRONOM" {...getColumnProps('pronom')} /></th></tr></thead>
              <tbody>
                {viewRows.map((r) => (
                  <tr key={r.attachmentId}>
                    <td>{r.fileName}</td>
                    <td>{r.mimeType ?? '—'}</td>
                    <td>{r.formatName ?? '—'}</td>
                    <td className="mono">{r.pronomPuid ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </motion.section>
  )
}
