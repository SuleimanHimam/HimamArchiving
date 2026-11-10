import { useState, useCallback, useEffect } from 'react'
import { motion, AnimatePresence } from 'motion/react'
import { ScanLine, Printer, RefreshCw, CheckCircle2, WifiOff, Loader2, Info, Download } from 'lucide-react'
import { scanAgent, localAgentBlockedByBrowser, type AgentSession, type ScannerCapabilities } from '../lib/scanAgent'
import { scannerSettings, type ScanPrefs } from '../lib/scannerSettings'
import { SCAN_AGENT_DOWNLOAD_URL, scanAgentDownloadAvailable } from '../lib/downloads'
import { auth } from '../lib/auth'
import '../pages/settings/settings.css'
import './scannersettings.css'

type Probe = 'idle' | 'checking' | 'connected' | 'offline' | 'unpaired'

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
  const [prefs, setPrefs]       = useState<ScanPrefs>(() => scannerSettings.getScanPrefs())
  // Reported by agent v1.2+; undefined from an older agent, which the UI treats as "unknown"
  // rather than "unsupported" so it does not hide options that may in fact work.
  const [caps, setCaps]         = useState<ScannerCapabilities | undefined>()
  const [dpiOptions, setDpiOptions] = useState<number[] | undefined>()
  // Remote Desktop only: which Windows session answered, and the box for pasting a token by hand
  // when the agent could not open a browser itself.
  const [session, setSession] = useState<AgentSession | undefined>()
  const [tokenInput, setTokenInput] = useState('')

  const savePrefs = (patch: Partial<ScanPrefs>) => {
    setPrefs((cur) => {
      const next = { ...cur, ...patch }
      scannerSettings.setScanPrefs(next)
      return next
    })
  }

  useEffect(() => { scanAgentDownloadAvailable().then(setCanDownload) }, [])

  const detect = useCallback(async () => {
    setProbe('checking')
    setScanners([])
    setPrinters([])

    const status = await scanAgent.status()
    if (!status) { setProbe('offline'); return }

    setSession(status.session)

    // An agent answered but rejected our token: on a Remote Desktop host that means we reached a
    // colleague's agent, not that no scanner exists. Telling the two apart is the difference between
    // "pair this browser" and a pointless hunt for a driver problem.
    if (status.authorized === false) { setProbe('unpaired'); return }

    setProbe('connected')
    setMock(status.mock)
    setCaps(status.capabilities)
    setDpiOptions(status.dpiOptions)

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

  function applyToken() {
    const t = tokenInput.trim()
    if (!t) return
    scanAgent.setToken(t)
    setTokenInput('')
    detect()
  }

  function chooseScanner(name: string) {
    setDefScanner(name)
    scannerSettings.setScanner(name)
  }
  function choosePrinter(name: string) {
    setDefPrinter(name)
    scannerSettings.setPrinter(name)
  }

  const totalDevices = scanners.length + printers.length

  // Checked once per render rather than once per probe: it depends on the address this page was
  // opened from, which cannot change without a reload.
  const browserBlock = localAgentBlockedByBrowser()
  const origin = typeof window !== 'undefined' ? window.location.origin : ''
  const httpsOrigin = origin.replace(/^http:/, 'https:')

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

      {/* Acquisition defaults — what every «مسح ضوئي» button in the app will ask the agent for.
          Options the connected scanner does not advertise are hidden rather than offered and
          silently ignored by the driver. */}
      {probe === 'connected' && (
        <fieldset className="dev-scanprefs" disabled={!canEdit}>
          <legend>إعدادات المسح الافتراضية</legend>

          <label>
            <span>الدقة</span>
            <select value={prefs.dpi} onChange={(e) => savePrefs({ dpi: Number(e.target.value) })}>
              {(dpiOptions ?? [150, 200, 300, 600]).map((d) => <option key={d} value={d}>{d} dpi</option>)}
            </select>
          </label>

          <label>
            <span>الألوان</span>
            <select value={prefs.color} onChange={(e) => savePrefs({ color: e.target.value as ScanPrefs['color'] })}>
              <option value="color">ملوّن</option>
              <option value="grayscale">تدرّج رمادي</option>
              <option value="blackwhite">أبيض وأسود</option>
            </select>
          </label>

          <label>
            <span>حجم الورق</span>
            <select value={prefs.paper} onChange={(e) => savePrefs({ paper: e.target.value as ScanPrefs['paper'] })}>
              <option value="a4">A4</option>
              <option value="a5">A5</option>
              <option value="letter">Letter</option>
              <option value="legal">Legal</option>
            </select>
          </label>

          {caps?.feeder && (
            <label>
              <span>مصدر الورق</span>
              <select value={prefs.source} onChange={(e) => savePrefs({ source: e.target.value as ScanPrefs['source'] })}>
                <option value="flatbed">الزجاج (صفحة واحدة)</option>
                <option value="feeder">وحدة التغذية (عدة صفحات)</option>
              </select>
            </label>
          )}

          {caps?.duplex && prefs.source === 'feeder' && (
            <label className="dev-scanprefs__check">
              <input type="checkbox" checked={prefs.duplex} onChange={(e) => savePrefs({ duplex: e.target.checked })} />
              <span>مسح الوجهين</span>
            </label>
          )}

          <label className="dev-scanprefs__check">
            <input type="checkbox" checked={prefs.preview} onChange={(e) => savePrefs({ preview: e.target.checked })} />
            <span>مراجعة الصفحات قبل الإرسال</span>
          </label>

          {!caps && (
            <p className="dev-scanprefs__note">
              الوكيل المتصل لا يبلّغ عن إمكاناته — حدّث برنامج الأجهزة المحلي لإظهار خيارات وحدة التغذية والوجهين.
            </p>
          )}
        </fieldset>
      )}

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

      {/* Which session answered — only interesting where there is more than one of them */}
      {session?.remote && probe === 'connected' && (
        <p className="muted mono" style={{ marginTop: 8 }}>
          الوكيل: {session.machine} · {session.user} · جلسة {session.id}
        </p>
      )}

      {/* An agent is running on this machine, but it belongs to a different Remote Desktop session */}
      {probe === 'unpaired' && (
        <div className="agent-help">
          <p>
            وُجد وكيل مسح ضوئي على هذا الخادم، لكنه <strong>يخصّ جلسة أخرى</strong>. هذا طبيعي على
            خوادم سطح المكتب البعيد: كل مستخدم يشغّل وكيله الخاص، وعلى المتصفح أن يعرف أيّها يخصّك.
          </p>
          <p className="muted">
            افتح نافذة «وكيل المسح الضوئي» من شريط المهام في جلستك، واضغط <strong>«ربط المتصفح»</strong>.
            ستُفتح صفحة تحفظ الرمز في هذا المتصفح مرة واحدة.
          </p>
          <p className="muted">
            وإن تعذّر فتح المتصفح تلقائياً، اضغط «نسخ الرمز» في نافذة الوكيل والصقه هنا:
          </p>
          <p style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            <input
              className="mono"
              value={tokenInput}
              onChange={(e) => setTokenInput(e.target.value)}
              placeholder="رمز الاقتران"
              style={{ flex: '1 1 220px', minWidth: 0 }}
            />
            <button type="button" className="btn btn-seal" onClick={applyToken} disabled={!tokenInput.trim()}>
              ربط
            </button>
          </p>
        </div>
      )}

      {/* Offline help */}
      {probe === 'offline' && (
        <div className="agent-help">
          {/* The browser blocking the call outright comes first: while it holds, installing the
              agent, restarting it and re-detecting all change nothing, and the advice below would
              send the user round that loop indefinitely. */}
          {browserBlock && (
            <div className="agent-help__block">
              <p>
                <strong>المتصفح يمنع هذه الصفحة من الوصول إلى أجهزة جهازك.</strong> فُتح النظام من
                العنوان <span className="mono">{origin}</span> عبر <span className="mono">http</span>،
                ومتصفحات Chrome و Edge لا تسمح لصفحة على عنوان خارجي غير مؤمّن بالاتصال بـ{' '}
                <span className="mono">127.0.0.1</span> — حيث يعمل برنامج الأجهزة المحلي. لذلك لا تظهر
                الماسحات ولا الطابعات حتى وإن كان البرنامج يعمل الآن على جهازك.
              </p>
              <p className="muted">الحلول، من الأفضل إلى الأسرع:</p>
              <ol className="muted">
                <li>
                  تشغيل النظام عبر <strong>HTTPS</strong> (<span className="mono">{httpsOrigin}</span>)
                  — عندها يسمح المتصفح بالاتصال ببرنامج الأجهزة، وهذا هو الحل الدائم.
                </li>
                <li>
                  أو فتح النظام من نفس الجهاز عبر <span className="mono">http://localhost:5080</span>.
                </li>
                <li>
                  أو السماح لهذا العنوان مرة واحدة في المتصفح:{' '}
                  <span className="mono">chrome://flags/#unsafely-treat-insecure-origin-as-secure</span>{' '}
                  ثم أضف <span className="mono">{origin}</span> واختر Enabled وأعد تشغيل المتصفح
                  (في Edge: <span className="mono">edge://flags</span> بنفس الاسم).
                </li>
              </ol>
              <p className="muted">
                متصفح Firefox لا يطبّق هذا القيد، فيمكن استخدامه كحل مؤقت.
              </p>
            </div>
          )}
          <p>
            لتمكين الكشف التلقائي عن الماسحات والطابعات، ثبّت وشغّل{' '}
            <strong>برنامج الأجهزة المحلي</strong> (Archiving Device Agent) على{' '}
            <strong>نفس الجهاز</strong>، ثم اضغط «كشف الأجهزة».
          </p>
          {canDownload && (
            <p>
              <a className="btn btn-seal" href={SCAN_AGENT_DOWNLOAD_URL}>
                <Download size={15} aria-hidden /> تحميل برنامج الأجهزة (Windows)
              </a>
            </p>
          )}
          <p className="muted">
            بعد التحميل: شغّل <span className="mono">archiving-scan-agent.exe</span> ووافق على «تثبيت البرنامج».
            يُنسخ عندها إلى مجلد ثابت، ويُضاف اختصار في قائمة ابدأ وسطح المكتب، ويبدأ تلقائياً مع تشغيل Windows.
            ستظهر نافذة البرنامج وأيقونة في شريط المهام تعرض الماسحات والطابعات وحالة الاتصال.
          </p>
          <p className="muted">
            إن لم تُثبّته فسيظل يعمل من ملف التحميل، وستحتاج إلى تشغيله يدوياً في كل مرة. يمكنك التثبيت لاحقاً
            من زر «تثبيت البرنامج على هذا الجهاز» داخل نافذة الوكيل، وإزالته من «التطبيقات» في إعدادات Windows.
          </p>
          <p className="muted mono">
            العنوان المتوقع: {scanAgent.baseUrl} (ويُبحث أيضاً في المنافذ 8765–8784 لدعم جلسات سطح المكتب البعيد)
          </p>
          <p className="muted">
            بدون البرنامج يمكنك رفع ملف ممسوح يدويًا من زر «مسح ضوئي» في صفحة الوثيقة.
          </p>
        </div>
      )}
    </motion.section>
  )
}
