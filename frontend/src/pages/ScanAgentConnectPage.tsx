import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { CheckCircle2, XCircle, Loader2, ScanLine } from 'lucide-react'
import { scanAgent } from '../lib/scanAgent'

/**
 * Pairing landing page — `/scan-agent/connect#token=…`
 *
 * The agent opens this from inside the user's own Windows session, and that is the entire mechanism.
 * On a Remote Desktop host every session shares one 127.0.0.1, so an address cannot tell one user's
 * agent from another's; what *can* is that the browser Windows launches here runs under this user's
 * profile, so the token it stores is reachable only by this user's pages.
 *
 * The token arrives in the fragment rather than the query string because fragments are never sent to
 * the server — pairing must not leave the secret in an access log.
 *
 * Deliberately outside ProtectedRoute: the agent starts at login, usually before anyone has signed
 * into the web app, and a pairing link that first demanded a password would simply never be clicked.
 * Nothing here reads or writes application data — it copies a string into localStorage.
 */
export default function ScanAgentConnectPage() {
  const navigate = useNavigate()
  const [state, setState] = useState<'working' | 'done' | 'missing'>('working')
  const [session, setSession] = useState<string | null>(null)

  useEffect(() => {
    // Fragment first (how the agent sends it), query second (a link someone retyped by hand).
    const fromHash = new URLSearchParams(window.location.hash.replace(/^#/, '')).get('token')
    const fromQuery = new URLSearchParams(window.location.search).get('token')
    const token = (fromHash || fromQuery || '').trim()

    if (!token) { setState('missing'); return }

    scanAgent.setToken(token)

    // Drop the token out of the address bar so it does not sit in history or get copied out of a
    // shared screen. replaceState keeps the SPA route intact while losing the fragment.
    window.history.replaceState(null, '', '/scan-agent/connect')

    // Confirm it actually matched an agent, rather than claiming success for a mistyped token.
    scanAgent.status(2000).then((status) => {
      setState('done')
      if (status?.session) {
        const s = status.session
        setSession(`${s.machine} · ${s.user} · جلسة ${s.id}`)
      }
    })
  }, [])

  return (
    <div style={{
      minHeight: '100vh', display: 'grid', placeItems: 'center',
      padding: 24, direction: 'rtl', fontFamily: 'inherit',
    }}>
      <div style={{
        maxWidth: 460, width: '100%', textAlign: 'center',
        border: '1px solid var(--border, #e3e6ea)', borderRadius: 14, padding: 32,
      }}>
        <ScanLine size={34} style={{ opacity: 0.7 }} />

        {state === 'working' && (
          <>
            <h1 style={{ fontSize: 20, margin: '14px 0 6px' }}>جارٍ ربط المتصفح…</h1>
            {/* Keyframes inline: this page is deliberately standalone and imports none of the
                app stylesheets, so it cannot borrow the .spin defined alongside other components. */}
            <style>{'@keyframes sa-spin{to{transform:rotate(360deg)}}'}</style>
            <Loader2 size={20} style={{ opacity: 0.6, animation: 'sa-spin .8s linear infinite' }} />
          </>
        )}

        {state === 'done' && (
          <>
            <h1 style={{ fontSize: 20, margin: '14px 0 6px' }}>
              <CheckCircle2 size={18} style={{ verticalAlign: '-3px', marginInlineEnd: 6 }} />
              تم ربط المتصفح بوكيل المسح الضوئي
            </h1>
            <p style={{ opacity: 0.75, lineHeight: 1.7 }}>
              {session
                ? <>هذا المتصفح مرتبط الآن بالوكيل العامل في: <b>{session}</b>.</>
                : <>حُفظ رمز الاقتران في هذا المتصفح. إذا لم يعمل المسح فتأكد أن الوكيل يعمل في جلستك.</>}
              <br />
              لن تحتاج إلى تكرار هذه الخطوة على هذا الجهاز.
            </p>
            <button
              type="button"
              onClick={() => navigate('/app', { replace: true })}
              style={{ marginTop: 18, padding: '8px 20px', borderRadius: 8, cursor: 'pointer' }}
            >
              الانتقال إلى النظام
            </button>
          </>
        )}

        {state === 'missing' && (
          <>
            <h1 style={{ fontSize: 20, margin: '14px 0 6px' }}>
              <XCircle size={18} style={{ verticalAlign: '-3px', marginInlineEnd: 6 }} />
              رابط الاقتران غير مكتمل
            </h1>
            <p style={{ opacity: 0.75, lineHeight: 1.7 }}>
              لا يحتوي هذا الرابط على رمز اقتران. افتح نافذة وكيل المسح الضوئي في جلستك واضغط
              «ربط المتصفح»، أو انسخ الرمز والصقه في إعدادات الماسح الضوئي.
            </p>
          </>
        )}
      </div>
    </div>
  )
}
