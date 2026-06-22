import { useState, useEffect, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { motion, type Variants } from 'motion/react'
import { AxiosError } from 'axios'
import { useTranslation } from 'react-i18next'
import { login } from '../lib/api'
import { SCAN_AGENT_DOWNLOAD_URL, scanAgentDownloadAvailable } from '../lib/downloads'
import LanguageToggle from '../components/LanguageToggle'
import PasswordInput from '../components/PasswordInput'
import { publicStats, type PublicStats } from '../lib/publicStats'
import './login.css'

function fmtArabicNumber(n: number | undefined, pad = false) {
  if (n == null) return '—'
  return new Intl.NumberFormat('ar-SA', pad ? { minimumIntegerDigits: 2 } : undefined).format(n)
}

const EASE = [0.2, 0.7, 0.2, 1] as const

const stamp: Variants = {
  hidden: { opacity: 0, scale: 0.6, rotate: -28 },
  show: { opacity: 0.92, scale: 1, rotate: -8, transition: { duration: 0.9, ease: EASE } },
}

const rise: Variants = {
  hidden: { opacity: 0, y: 22 },
  show: (i: number) => ({
    opacity: 1, y: 0,
    transition: { duration: 0.7, delay: 0.12 * i, ease: EASE },
  }),
}

const DEMO_EMAIL = 'admin@archiving.local'
const DEMO_PASSWORD = 'Admin@12345'

export default function LoginPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [canDownload, setCanDownload] = useState(false)
  const [stats, setStats] = useState<PublicStats | null>(null)

  useEffect(() => { scanAgentDownloadAvailable().then(setCanDownload) }, [])
  useEffect(() => { publicStats.get().then(setStats).catch(() => {}) }, [])

  function autoFill() { setEmail(DEMO_EMAIL); setPassword(DEMO_PASSWORD); setError('') }

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError('')
    if (!email || !password) { setError(t('auth.errors.required')); return }
    setLoading(true)
    try {
      await login(email.trim(), password)
      navigate('/app', { replace: true })
    } catch (err) {
      const ax = err as AxiosError<{ error?: string }>
      setError(
        ax.response?.data?.error ??
          (ax.code === 'ERR_NETWORK' ? t('auth.errors.networkError') : t('auth.errors.failed')),
      )
    } finally { setLoading(false) }
  }

  return (
    <div className="login">
      <section className="login__brand">
        <div className="login__watermark" aria-hidden>{t('app.name')}</div>
        <div className="login__ledger" aria-hidden />

        <motion.div className="login__masthead" initial="hidden" animate="show">
          <motion.p className="kicker" variants={rise} custom={0}>
            {t('app.officialRegistry')}
          </motion.p>

          <motion.h1 variants={rise} custom={1}>
            {t('app.tagline')}
          </motion.h1>

          <motion.hr className="rule-brass" variants={rise} custom={2} />

          <motion.div className="login__stats" variants={rise} custom={3}>
            <Stat label={t('auth.stats.todayTransactions')} value={fmtArabicNumber(stats?.todayTransactions)} />
            <Stat label={t('auth.stats.pendingApproval')} value={fmtArabicNumber(stats?.pendingApproval)} />
            <Stat label={t('auth.stats.overdue')} value={fmtArabicNumber(stats?.overdue, true)} tone="seal" />
          </motion.div>
        </motion.div>

        <motion.div className="login__seal" variants={stamp} initial="hidden" animate="show" aria-hidden>
          <div className="stamp"><span>{t('app.name')}</span></div>
        </motion.div>
      </section>

      <motion.section
        className="login__panel"
        initial={{ opacity: 0, y: 34 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.7, delay: 0.25, ease: EASE }}
      >
        <div className="login__card doc-card">
          <header className="login__cardhead">
            <div className="login__headrow">
              <span className="kicker">{t('auth.loginSection')}</span>
              <div className="login__headactions">
                <LanguageToggle />
                <button type="button" className="login__autofill" onClick={autoFill} title={t('auth.autoFillTitle')}>
                  {t('auth.autoFill')}
                </button>
              </div>
            </div>
            <h2>{t('auth.login')}</h2>
            <p className="muted">{t('auth.subtitle')}</p>
          </header>

          <form onSubmit={submit} noValidate>
            <div className="field">
              <label htmlFor="email">{t('auth.email')}</label>
              <input
                id="email" type="email" dir="ltr"
                placeholder={t('auth.emailPlaceholder')}
                value={email} onChange={(e) => setEmail(e.target.value)}
                autoComplete="username"
              />
            </div>

            <div className="field">
              <label htmlFor="pwd">{t('auth.password')}</label>
              <PasswordInput
                id="pwd"
                placeholder={t('auth.passwordPlaceholder')}
                value={password} onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password"
              />
            </div>

            {error && (
              <motion.p className="login__error" initial={{ opacity: 0, x: -6 }} animate={{ opacity: 1, x: 0 }}>
                {error}
              </motion.p>
            )}

            <button className="btn btn-primary login__submit" disabled={loading}>
              {loading ? t('auth.loginLoading') : t('auth.loginAction')}
            </button>

            <p className="login__mfa muted">
              <span className="badge secret">{t('common.mfa')}</span>
              {t('auth.mfaNote')}
            </p>
          </form>
        </div>

        <footer className="login__foot">
          <span>{t('auth.footer.institution')}</span>
          {canDownload && (
            <a className="login__agentlink" href={SCAN_AGENT_DOWNLOAD_URL} title={t('documents.scan.notAvailable')}>
              ⬇ {t('auth.scanAgentLink')}
            </a>
          )}
          <span className="mono">v0.1 · {new Date().toLocaleDateString(i18n.language)}</span>
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
