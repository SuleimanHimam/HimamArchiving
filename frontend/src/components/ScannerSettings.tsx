import { useState, useCallback, useEffect } from 'react'
import { motion, AnimatePresence } from 'motion/react'
import { ScanLine, Printer, RefreshCw, CheckCircle2, WifiOff, Loader2, Info } from 'lucide-react'
import { scanAgent } from '../lib/scanAgent'
import { scannerSettings } from '../lib/scannerSettings'
import { SCAN_AGENT_DOWNLOAD_URL, scanAgentDownloadAvailable } from '../lib/downloads'
import { auth } from '../lib/auth'
import '../pages/settings/settings.css'
import './scannersettings.css'

type Probe = 'idle' | 'checking' | 'connected' | 'offline'

interface DeviceListProps {
  type: 'scanner' | 'printer'
  devices: string[]
  selected: string | null
  onSelect: (name: string) => void
  canEdit: boolean
}

function DeviceList({ type, devices, selected, onSelect, canEdit }: DeviceListProps) {
  const isScanner = type === 'scanner'
  const Icon = isScanner ? ScanLine : Printer
  const label = isScanner ? 'الماسحات الضوئية' : 'الطابعات'
  const emptyMsg = isScanner
    ? 'لم يُعثر على ماسح ضوئي. تحقق من توصيله وتثبيت تعريفه (TWAIN / WIA).'
    : 'لم يُعثر على طابعة. تحقق من توصيلها وتثبيتها في نظام الطباعة.'
  const typeClass = isScanner ? 'dev-scanner' : 'dev-printer'

  return (
    <div className={`dev-section ${typeClass}`}>
      <div className="dev-section__head">
        <Icon className="dev-section__icon" size={18} />
        <span className="dev-section__label">{label}</span>
        <span className="dev-section__count">{devices.length}</span>
      </div>

      {devices.length === 0 ? (
        <p className="dev-empty">{emptyMsg}</p>
      ) : (
        <ul className="dev-list">
          {devices.map((d) => {
            const active = selected === d
            return (
              <li
                key={d}
                className={`dev-item ${active ? 'is-active' : ''} ${!canEdit ? 'is-readonly' : ''}`}
                onClick={() => canEdit && onSelect(d)}
                role="radio"
                aria-checked={active}
              >
                <span className={`dev-radio ${typeClass}`} aria-hidden>
                  {active ? '◉' : '○'}
                </span>
                <div className="dev-info">
                  <span className="dev-name">{d}</span>
                  <span className="dev-type-badge">{isScanner ? 'TWAIN · WIA' : 'Print Queue'}</span>
                </div>
                {active && (
                  <span className={`dev-default ${typeClass}`}>
                    <CheckCircle2 size={12} />
                    افتراضي
                  </span>
                )}
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}

export default function ScannerSettings() {
  const canEdit = auth.hasPermission('Scanner.Edit')
  const [probe, setProbe]       = useState<Probe>('idle')
  const [scanners, setScanners] = useState<string[]>([])
  const [printers, setPrinters] = useState<string[]>([])
  const [defScanner, setDefScanner] = useState<string | null>(scannerSettings.getScanner())
  const [defPrinter, setDefPrinter] = useState<string | null>(scannerSettings.getPrinter())
  const [mock, setMock]         = useState(false)
  const [canDownload, setCanDownload] = useState(false)

  useEffect(() => { scanAgentDownloadAvailable().then(setCanDownload) }, [])

  const detect = useCallback(async () => {
    setProbe('checking')
    setScanners([])
    setPrinters([])

    const status = await scanAgent.status()
    if (!status) { setProbe('offline'); return }

    setProbe('connected')
    setMock(status.mock)

    const sc = status.scanners ?? []
    const pr = status.printers ?? []
    setScanners(sc)
    setPrinters(pr)

    // Auto-select defaults: keep existing if still present, else take first
    setDefScanner((cur) => {
      const next = cur && sc.includes(cur) ? cur : (sc[0] ?? null)
      scannerSettings.setScanner(next)
      return next
    })
    setDefPrinter((cur) => {
      const next = cur && pr.includes(cur) ? cur : (pr[0] ?? null)
      scannerSettings.setPrinter(next)
      return next
    })
  }, [])

  useEffect(() => { detect() }, [detect])

  function chooseScanner(name: string) {
    setDefScanner(name)
    scannerSettings.setScanner(name)
  }
  function choosePrinter(name: string) {
    setDefPrinter(name)
    scannerSettings.setPrinter(name)
  }

  const totalDevices = scanners.length + printers.length

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }}>
      {/* Header */}
      <div className="set-head">
        <div>
          <h3 className="detail-h3">الأجهزة المتصلة</h3>
          <p className="dev-subtitle">
            الماسحات الضوئية (TWAIN / WIA) والطابعات على هذا الجهاز
          </p>
        </div>
        <button
          className="btn btn-ghost dev-refresh-btn"
          onClick={detect}
          disabled={probe === 'checking'}
        >
          <RefreshCw size={15} className={probe === 'checking' ? 'spin' : ''} />
          {probe === 'checking' ? 'جارٍ الكشف…' : 'كشف الأجهزة'}
        </button>
      </div>

      {/* Status bar */}
      <div className={`agent-status agent-${probe}`}>
        {probe === 'checking' && (
          <><Loader2 size={14} className="spin" /> جارٍ الاتصال ببرنامج الأجهزة المحلي…</>
        )}
        {probe === 'connected' && (
          <>
            <span className="agent-dot agent-dot--ok" />
            برنامج الأجهزة متصل{mock ? ' (وضع تجريبي)' : ''}
            {' — '}
            {scanners.length > 0 && <><ScanLine size={13} /> {scanners.length} ماسح</>}
            {scanners.length > 0 && printers.length > 0 && ' · '}
            {printers.length > 0 && <><Printer size={13} /> {printers.length} طابعة</>}
            {totalDevices === 0 && 'لا توجد أجهزة'}
          </>
        )}
        {probe === 'offline' && (
          <><WifiOff size={14} /> برنامج الأجهزة المحلي غير متصل</>
        )}
        {probe === 'idle' && <span>—</span>}
      </div>

      {/* Device lists */}
      <AnimatePresence mode="wait">
        {probe === 'connected' && (
          <motion.div
            className="dev-grid"
            key="devices"
            initial={{ opacity: 0, y: 8 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0 }}
          >
            <DeviceList
              type="scanner"
              devices={scanners}
              selected={defScanner}
              onSelect={chooseScanner}
              canEdit={canEdit}
            />
            <DeviceList
              type="printer"
              devices={printers}
              selected={defPrinter}
              onSelect={choosePrinter}
              canEdit={canEdit}
            />
          </motion.div>
        )}
      </AnimatePresence>

      {/* Difference explanation */}
      {probe === 'connected' && (
        <div className="dev-legend">
          <Info size={13} />
          <span>
            <strong>الماسح الضوئي</strong>: يحوّل الورق إلى ملف رقمي (PDF/JPEG) ·{' '}
            <strong>الطابعة</strong>: تحوّل الملف الرقمي إلى ورق مطبوع
          </span>
        </div>
      )}

      {/* Offline help */}
      {probe === 'offline' && (
        <div className="agent-help">
          <p>
            لتمكين الكشف التلقائي عن الماسحات والطابعات، ثبّت وشغّل{' '}
            <strong>برنامج الأجهزة المحلي</strong> (Archiving Device Agent) على{' '}
            <strong>نفس الجهاز</strong>، ثم اضغط «كشف الأجهزة».
          </p>
          {canDownload && (
            <p>
              <a className="btn btn-seal" href={SCAN_AGENT_DOWNLOAD_URL}>
                ⬇ تحميل برنامج الأجهزة (Windows)
              </a>
            </p>
          )}
          <p className="muted">
            بعد التحميل: شغّل <span className="mono">start-agent.bat</span> أو{' '}
            <span className="mono">archiving-device-agent.exe</span>، أو{' '}
            <span className="mono">install-startup.bat</span> للتشغيل التلقائي.
          </p>
          <p className="muted mono">العنوان المتوقع: {scanAgent.baseUrl}</p>
          <p className="muted">
            بدون البرنامج يمكنك رفع ملف ممسوح يدويًا من زر «مسح ضوئي» في صفحة الوثيقة.
          </p>
        </div>
      )}
    </motion.section>
  )
}
