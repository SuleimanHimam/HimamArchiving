import { useState, useEffect, useMemo, useSyncExternalStore, type CSSProperties, type FormEvent } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import { getBranding, subscribeBranding } from '../lib/branding'
import { AxiosError } from 'axios'
import { useTranslation } from 'react-i18next'
import { Archive, FileSignature, FileText, Folder, Gavel, Mail, Stamp, User, Download, MonitorDown } from 'lucide-react'
import { api, login } from '../lib/api'
import { roleLabel } from '../lib/users'
import { withLatnDigits } from '../lib/datetime'
import { SCAN_AGENT_DOWNLOAD_URL, scanAgentDownloadAvailable, DESKTOP_SHORTCUT_URL, isWindows } from '../lib/downloads'
import LanguageToggle from '../components/LanguageToggle'
import ThemeToggle from '../components/ThemeToggle'
import PasswordInput from '../components/PasswordInput'
import BrandMark from '../components/BrandMark'
import './login.css'

/** A try-it account offered by the API (development only — see AuthController.DemoAccounts). */
type DemoAccount = {
  email: string
  password: string
  fullName: string
  jobTitle: string
  role: string
}

const PARTICLE_ICONS = [FileText, Mail, Folder, Stamp, Gavel, FileSignature, Archive]

type Particle = {
  Icon: typeof FileText
  left: number
  size: number
  duration: number
  delay: number
  opacity: number
  sway: number
  /** Where it sits when the animation is off — see the reduced-motion rule in login.css. */
  top: number
  /** 0 = far and faint, 2 = near and crisp. Drives size, opacity, blur and parallax together,
   *  so the three read as one field seen at depth rather than three unrelated speeds. */
  depth: 0 | 1 | 2
}

/** Per-depth look. Kept here rather than randomised so the layers stay distinguishable: a glyph
 *  that is large, sharp and slow-moving reads as near, and one that is small, blurred and faint
 *  reads as far — the illusion collapses the moment the two overlap in every property. */
const DEPTHS = [
  { size: [12, 20], opacity: [0.05, 0.08], duration: [55, 80], blur: 1.4 },
  { size: [20, 30], opacity: [0.09, 0.13], duration: [40, 60], blur: 0.5 },
  { size: [30, 44], opacity: [0.13, 0.18], duration: [28, 44], blur: 0 },
] as const

/** 18 drifting document glyphs across three depths. Built once per mount — re-randomising on
 *  every render would make the field twitch. Negative delays pre-populate the screen instead of
 *  starting empty.
 *
 *  Still slow (28–80s a crossing) and still quiet: the earlier field was invisible at 2.5–5%
 *  opacity — on a laptop screen in daylight it read as a plain gradient, so the animation was
 *  paying its cost and showing nothing. Raised to 5–18% by depth, which is legible without
 *  competing with the card. `sway` is the single sideways excursion each glyph makes; the CSS
 *  keyframes handle the fade at both ends. */
function makeParticles(): Particle[] {
  const between = ([lo, hi]: readonly [number, number]) => lo + Math.random() * (hi - lo)
  return Array.from({ length: 18 }, (_, i) => {
    const depth = (i % 3) as 0 | 1 | 2
    const d = DEPTHS[depth]
    return {
      Icon: PARTICLE_ICONS[i % PARTICLE_ICONS.length],
      left: Math.random() * 100,
      size: Math.round(between(d.size)),
      duration: Math.round(between(d.duration)),
      delay: -Math.random() * 60,
      opacity: Number(between(d.opacity).toFixed(3)),
      sway: Number((Math.random() * 5 - 2.5).toFixed(2)),
      // Without motion every glyph keeps its static position, which is the top of the field — all
      // of them in one row along the edge, reading as a page that failed to load rather than as a
      // still background. Scattered down the field instead.
      top: Number((Math.random() * 88).toFixed(1)),
      depth,
    }
  })
}

export default function LoginPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const location = useLocation()
  // Set by ProtectedRoute (the page that was asked for) and by the idle timer (why we are here).
  const nav = location.state as { from?: string; reason?: 'idle' | 'manual' } | null
  const from = nav?.from ?? '/app'
  const notice = nav?.reason === 'idle' ? t('session.expired') : ''
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [canDownload, setCanDownload] = useState(false)
  const [demoAccounts, setDemoAccounts] = useState<DemoAccount[]>([])
  const branding = useSyncExternalStore(subscribeBranding, getBranding)
  const particles = useMemo(makeParticles, [])


  useEffect(() => { scanAgentDownloadAvailable().then(setCanDownload) }, [])

  /* Try-it accounts, one per role. The list comes from the server rather than being hardcoded
     here: it is empty outside development, which is what keeps a real deployment from advertising
     working credentials on its login screen. A failure is silent — no accounts, no buttons. */
  useEffect(() => {
    api.get<DemoAccount[]>('/auth/demo-accounts')
      .then((r) => setDemoAccounts(r.data))
      .catch(() => setDemoAccounts([]))
  }, [])

  /** Fills the form without submitting, so the credentials are visible before signing in. */
  function fillDemo(acc: DemoAccount) {
    setEmail(acc.email); setPassword(acc.password); setError('')
  }

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError('')
    if (!email || !password) { setError(t('auth.errors.required')); return }
    setLoading(true)
    try {
      // No exit fade here. The prototype can fade its login out *over* the app because both live in
      // one DOM; routing unmounts this page first, so fading out just dips through a blank screen.
      // Navigate straight away and let the shell fade itself in.
      await login(email.trim(), password)
      // Back to whatever was asked for before the redirect, not always the dashboard.
      navigate(from, { replace: true })
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
      <div className="login__bg" aria-hidden>
        {/* One element per depth: the glyphs of a depth share a size, an opacity and a blur, and
            grouping them keeps that reading as one plane rather than as scattered sizes. */}
        {([0, 1, 2] as const).map((depth) => (
          <div key={depth} className="login__layer">
            {particles.filter((p) => p.depth === depth).map((p, i) => (
              <p.Icon
                key={i}
                className="login__particle"
                style={{
                  insetInlineStart: `${p.left}%`,
                  width: p.size, height: p.size,
                  animationDuration: `${p.duration}s`,
                  animationDelay: `${p.delay}s`,
                  filter: DEPTHS[depth].blur ? `blur(${DEPTHS[depth].blur}px)` : undefined,
                  // Read by the keyframes — opacity is animated, so it cannot be set directly here.
                  '--p-op': p.opacity,
                  '--p-sway': `${p.sway}vw`,
                  // Only read when motion is off; the drift keyframes position by transform.
                  '--p-top': `${p.top}vh`,
                } as CSSProperties}
              />
            ))}
          </div>
        ))}
      </div>

      <div className="login__card">
        <div className="login__head">
          {branding.logoBase64
            ? <img src={branding.logoBase64} alt="" className="login__logo" />
            : <BrandMark size={46} />}
          <h2 className="login__title">{branding.nameAr || t('app.name')}</h2>
          <p className="login__sub">{t('auth.subtitle')}</p>
        </div>

        <form onSubmit={submit} noValidate className="login__form">
          <div className="login__field">
            <User className="login__fieldicon" aria-hidden />
            {/* text, not email: the field takes a short user name ("admin") as well as a full
                address, and type="email" would have the browser reject the short form. */}
            {/* No placeholder: the field is identified by its icon and by aria-label. A greyed
                example sitting in the box was read as a value already entered. */}
            <input
              id="email" type="text" dir="ltr"
              value={email} onChange={(e) => setEmail(e.target.value)}
              autoComplete="username"
              aria-label={t('auth.email')}
            />
          </div>

          <div className="login__field login__field--pwd">
            <PasswordInput
              id="pwd"
              placeholder={t('auth.passwordPlaceholder')}
              value={password} onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              aria-label={t('auth.password')}
            />
          </div>

          {/* Why the session ended. Kept as a persistent line rather than a toast: someone who
              walked away comes back to a login screen and needs the reason to still be there. */}
          {notice && !error && <p className="login__notice">{notice}</p>}
          {error && <p className="login__error">{error}</p>}

          <button className="login__submit" disabled={loading}>
            {loading ? t('auth.loginLoading') : t('auth.loginAction')}
          </button>

          <p className="login__mfa">
            <span className="login__mfabadge">{t('common.mfa')}</span>
            {t('auth.mfaNote')}
          </p>
        </form>

        {demoAccounts.length > 0 && (
          <div className="login__demo">
            <span className="login__demolabel">{t('auth.demo.title')}</span>
            <div className="login__demogrid">
              {demoAccounts.map((acc) => (
                <button
                  key={acc.email} type="button" className="login__demobtn"
                  onClick={() => fillDemo(acc)}
                  title={t('auth.demo.fillTitle', { email: acc.email })}
                >
                  <span className="login__demorole">{roleLabel(acc.role)}</span>
                  {/* The administrator's name *is* their role, so printing both reads as a stutter. */}
                  {acc.fullName !== roleLabel(acc.role) && (
                    <span className="login__demoname">{acc.fullName}</span>
                  )}
                </button>
              ))}
            </div>
          </div>
        )}

        <div className="login__foot">
          <div className="login__footactions">
            <LanguageToggle />
            <ThemeToggle />
          </div>
        </div>
      </div>

      <footer className="login__meta">
        <span>{t('auth.footer.institution')}</span>
        {canDownload && (
          <a className="login__agentlink" href={SCAN_AGENT_DOWNLOAD_URL} title={t('documents.scan.notAvailable')}>
            <Download size={15} aria-hidden /> {t('auth.scanAgentLink')}
          </a>
        )}
        {/* Offered here, before sign-in, because the person who most needs it is the one opening
            the system on this machine for the first time. Windows only — it is a .cmd. */}
        {isWindows() && (
          <a className="login__agentlink" href={DESKTOP_SHORTCUT_URL}
            title="ينشئ اختصارًا يفتح النظام في نافذته الخاصة، بلا شريط عنوان">
            <MonitorDown size={15} aria-hidden /> تثبيت على سطح المكتب
          </a>
        )}
        <span className="mono">v0.1 · {new Date().toLocaleDateString(withLatnDigits(i18n.language))}</span>
      </footer>
    </div>
  )
}
