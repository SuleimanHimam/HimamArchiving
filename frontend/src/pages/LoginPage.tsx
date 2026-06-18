import { useState, useEffect, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { motion, type Variants } from 'motion/react'
import { AxiosError } from 'axios'
import { login } from '../lib/api'
import { SCAN_AGENT_DOWNLOAD_URL, scanAgentDownloadAvailable } from '../lib/downloads'
import './login.css'

const EASE = [0.2, 0.7, 0.2, 1] as const

const stamp: Variants = {
  hidden: { opacity: 0, scale: 0.6, rotate: -28 },
  show: { opacity: 0.92, scale: 1, rotate: -8, transition: { duration: 0.9, ease: EASE } },
}

const rise: Variants = {
  hidden: { opacity: 0, y: 22 },
  show: (i: number) => ({
    opacity: 1,
    y: 0,
    transition: { duration: 0.7, delay: 0.12 * i, ease: EASE },
  }),
}

// Demo admin credentials — these will be seeded when JWT auth is built.
const DEMO_EMAIL = 'admin@archiving.local'
const DEMO_PASSWORD = 'Admin@12345'

export default function LoginPage() {
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [canDownload, setCanDownload] = useState(false)

  useEffect(() => { scanAgentDownloadAvailable().then(setCanDownload) }, [])

  function autoFill() {
    setEmail(DEMO_EMAIL)
    setPassword(DEMO_PASSWORD)
    setError('')
  }

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError('')
    if (!email || !password) {
      setError('يرجى إدخال البريد الإلكتروني وكلمة المرور')
      return
    }
    setLoading(true)
    try {
      await login(email.trim(), password)
      navigate('/app', { replace: true })
    } catch (err) {
      const ax = err as AxiosError<{ error?: string }>
      setError(
        ax.response?.data?.error ??
          (ax.code === 'ERR_NETWORK'
            ? 'تعذّر الاتصال بالخادم. تأكد من تشغيل واجهة الـ API.'
            : 'فشل تسجيل الدخول'),
      )
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="login">
      {/* ---------- Masthead (institutional) ---------- */}
      <section className="login__brand">
        <div className="login__watermark" aria-hidden>ديوان</div>
        <div className="login__ledger" aria-hidden />

        <motion.div
          className="login__masthead"
          initial="hidden"
          animate="show"
        >
          <motion.p className="kicker" variants={rise} custom={0}>
            OFFICIAL REGISTRY · الديوان الإلكتروني
          </motion.p>

          <motion.h1 variants={rise} custom={1}>
            نظام إدارة<br />الوثائق والأرشفة<br />
            <span className="login__accent">وسير العمل المؤسسي</span>
          </motion.h1>

          <motion.hr className="rule-brass" variants={rise} custom={2} />

          <motion.p className="login__lede" variants={rise} custom={3}>
            منصّة موحّدة لتسجيل المعاملات الواردة والصادرة، وتتبّع دورة حياة الوثيقة،
            وإدارة سير العمل والاعتمادات — بحوكمة كاملة وسجل تدقيق محصّن.
          </motion.p>

          <motion.div className="login__stats" variants={rise} custom={4}>
            <Stat label="معاملات اليوم" value="٢٤٧" />
            <Stat label="بانتظار الاعتماد" value="٣١" />
            <Stat label="متأخّرة" value="٠٤" tone="seal" />
          </motion.div>
        </motion.div>

        <motion.div
          className="login__seal"
          variants={stamp}
          initial="hidden"
          animate="show"
          aria-hidden
        >
          <div className="stamp">
            <span>ختم<br />الديوان</span>
          </div>
        </motion.div>
      </section>

      {/* ---------- Auth panel ---------- */}
      <motion.section
        className="login__panel"
        initial={{ opacity: 0, y: 34 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.7, delay: 0.25, ease: EASE }}
      >
        <div className="login__card doc-card">
          <header className="login__cardhead">
            <div className="login__headrow">
              <span className="kicker">AUTHENTICATION · مصادقة</span>
              <button type="button" className="login__autofill" onClick={autoFill} title="تعبئة بيانات الدخول التجريبية">
                تعبئة تلقائية
              </button>
            </div>
            <h2>تسجيل الدخول</h2>
            <p className="muted">أدخل بيانات اعتمادك للوصول إلى الديوان.</p>
          </header>

          <form onSubmit={submit} noValidate>
            <div className="field">
              <label htmlFor="email">البريد الإلكتروني</label>
              <input
                id="email"
                type="email"
                dir="ltr"
                placeholder="name@institution.gov"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                autoComplete="username"
              />
            </div>

            <div className="field">
              <label htmlFor="pwd">كلمة المرور</label>
              <input
                id="pwd"
                type="password"
                placeholder="••••••••"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password"
              />
            </div>

            {error && (
              <motion.p
                className="login__error"
                initial={{ opacity: 0, x: -6 }}
                animate={{ opacity: 1, x: 0 }}
              >
                {error}
              </motion.p>
            )}

            <button className="btn btn-primary login__submit" disabled={loading}>
              {loading ? '...جارٍ التحقق' : 'دخول الديوان'}
            </button>

            <div className="login__or"><span>أو</span></div>

            <button type="button" className="btn btn-ghost login__ad">
              الدخول عبر حساب المؤسسة (Active Directory)
            </button>

            <p className="login__mfa muted">
              <span className="badge secret">MFA</span>
              قد يُطلب منك رمز تحقّق ثنائي بعد إدخال كلمة المرور.
            </p>
          </form>
        </div>

        <footer className="login__foot">
          <span>وزارة / مؤسسة — الديوان الإلكتروني</span>
          {canDownload && (
            <a className="login__agentlink" href={SCAN_AGENT_DOWNLOAD_URL} title="لتمكين المسح الضوئي من جهازك">
              ⬇ برنامج المسح الضوئي
            </a>
          )}
          <span className="mono">v0.1 · {new Date().toLocaleDateString('ar')}</span>
        </footer>
      </motion.section>
    </div>
  )
}

function Stat({ label, value, tone }: { label: string; value: string; tone?: 'seal' }) {
  return (
    <div className="login__stat">
      <span className={`login__statnum mono ${tone === 'seal' ? 'is-seal' : ''}`}>{value}</span>
      <span className="login__statlabel">{label}</span>
    </div>
  )
}
