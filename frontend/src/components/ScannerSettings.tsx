import { useState, useCallback, useEffect } from 'react'
import { motion } from 'motion/react'
import { scanAgent } from '../lib/scanAgent'
import { scannerSettings } from '../lib/scannerSettings'
import { SCAN_AGENT_DOWNLOAD_URL, scanAgentDownloadAvailable } from '../lib/downloads'

type Probe = 'idle' | 'checking' | 'connected' | 'offline'

export default function ScannerSettings() {
  const [probe, setProbe] = useState<Probe>('idle')
  const [scanners, setScanners] = useState<string[]>([])
  const [selected, setSelected] = useState<string | null>(scannerSettings.get())
  const [mock, setMock] = useState(false)
  const [canDownload, setCanDownload] = useState(false)

  useEffect(() => { scanAgentDownloadAvailable().then(setCanDownload) }, [])

  const detect = useCallback(async () => {
    setProbe('checking'); setScanners([])
    const status = await scanAgent.status()
    if (!status) { setProbe('offline'); return }
    setProbe('connected')
    setMock(status.mock)
    setScanners(status.scanners)
    setSelected((cur) => {
      const next = cur && status.scanners.includes(cur) ? cur : (status.scanners[0] ?? null)
      scannerSettings.set(next)
      return next
    })
  }, [])

  useEffect(() => { detect() }, [detect])

  function choose(name: string) {
    setSelected(name)
    scannerSettings.set(name)
  }

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }}>
      <div className="set-head">
        <h3 className="detail-h3">الماسحات الضوئية المتصلة بجهازك</h3>
        <button className="btn btn-ghost" onClick={detect} disabled={probe === 'checking'}>
          {probe === 'checking' ? '…جارٍ الفحص' : '↻ كشف الماسحات'}
        </button>
      </div>

      <div className={`agent-status agent-${probe}`}>
        {probe === 'checking' && <span>جارٍ الاتصال ببرنامج المسح المحلي…</span>}
        {probe === 'connected' && (
          <span>● برنامج المسح متصل{mock ? ' (وضع تجريبي)' : ''} — {scanners.length} ماسح ضوئي</span>
        )}
        {probe === 'offline' && <span>● برنامج المسح المحلي غير متصل</span>}
        {probe === 'idle' && <span>—</span>}
      </div>

      {probe === 'connected' && (
        scanners.length === 0 ? (
          <p className="muted">لم يتم العثور على ماسح ضوئي. تأكد من توصيل الماسح وتثبيت تعريفه على جهازك.</p>
        ) : (
          <ul className="scanner-list">
            {scanners.map((s) => (
              <li key={s} className={`scanner-item ${selected === s ? 'is-selected' : ''}`} onClick={() => choose(s)}>
                <span className="scanner-radio" aria-hidden>{selected === s ? '◉' : '○'}</span>
                <span className="scanner-name">{s}</span>
                {selected === s && <span className="scanner-default">افتراضي</span>}
              </li>
            ))}
          </ul>
        )
      )}

      {probe === 'offline' && (
        <div className="agent-help">
          <p>لتمكين المسح الضوئي مباشرةً من الماسح المتصل بجهازك، ثبّت وشغّل <strong>برنامج المسح المحلي</strong>
            (Archiving Scan Agent) على <strong>نفس الجهاز المتصل به الماسح</strong>، ثم اضغط «كشف الماسحات».</p>
          {canDownload && (
            <p><a className="btn btn-seal" href={SCAN_AGENT_DOWNLOAD_URL}>⬇ تحميل برنامج المسح (Windows)</a></p>
          )}
          <p className="muted">بعد التحميل: شغّل <span className="mono">start-scan-agent.bat</span> أو
            <span className="mono"> archiving-scan-agent.exe</span>، أو شغّل <span className="mono">install-startup.bat</span> لبدء التشغيل تلقائيًا.</p>
          <p className="muted mono">العنوان المتوقع: {scanAgent.baseUrl}</p>
          <p className="muted">بدون البرنامج يمكنك رفع ملف ممسوح من زر «مسح ضوئي» في صفحة الوثيقة.</p>
        </div>
      )}
    </motion.section>
  )
}
