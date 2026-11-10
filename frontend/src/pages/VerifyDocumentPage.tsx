import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { ShieldCheck, ShieldAlert, Loader2, Building2, Calendar, FileText, Hash } from 'lucide-react'
import axios from 'axios'
import './verify.css'

/* «هل هذه الورقة من هذا النظام؟»
 *
 * Where the QR printed on a document lands. Whoever scans it is usually not a user of this system
 * — a supplier, another ministry, a citizen at a counter — so the page carries no shell, no login,
 * and asks for nothing. It answers one question and shows the few facts that let the reader match
 * the answer against the paper in their hand: the number, the kind, the issuing department, the
 * date. The content itself is not read out, because confirming a document exists is a different
 * act from publishing it. */

interface VerificationResult {
  valid: boolean
  documentNumber: string | null
  typeName: string | null
  orgUnitName: string | null
  documentDate: string | null
  registeredAt: string | null
  status: string | null
  title: string | null
  checkedAt: string
}

const STATUS_AR: Record<string, string> = {
  Active: 'سارية', Archived: 'مؤرشفة', Draft: 'مسودة',
  Deleted: 'محذوفة من السجل', Destroyed: 'أُتلفت وفق سياسة الحفظ',
}

const dateOf = (v: string | null) => {
  if (!v) return '—'
  const d = new Date(v)
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleDateString('ar-EG', { year: 'numeric', month: 'long', day: 'numeric' })
}

export default function VerifyDocumentPage() {
  const { code = '' } = useParams()
  const [state, setState] = useState<'loading' | 'done' | 'error'>('loading')
  const [result, setResult] = useState<VerificationResult | null>(null)

  useEffect(() => {
    // Its own axios call, not the app's client: that one attaches a token and redirects to the
    // login page on 401, and this page must work for someone who has neither.
    const base = (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/$/, '') ?? '/api'
    axios.get<VerificationResult>(`${base}/public/verify/${encodeURIComponent(code)}`)
      .then((r) => { setResult(r.data); setState('done') })
      .catch(() => setState('error'))
  }, [code])

  const valid = result?.valid === true
  const withdrawn = valid && (result?.status === 'Deleted' || result?.status === 'Destroyed')

  return (
    <div className="vfy">
      <main className={`vfy__card ${state === 'done' ? (valid ? (withdrawn ? 'is-warn' : 'is-ok') : 'is-bad') : ''}`}>
        {state === 'loading' && (
          <p className="vfy__wait"><Loader2 size={18} className="spin" aria-hidden /> جارٍ التحقق…</p>
        )}

        {state === 'error' && (
          <>
            <ShieldAlert size={40} aria-hidden />
            <h1>تعذّر التحقق</h1>
            <p className="vfy__sub">لم نتمكّن من الوصول إلى النظام الآن. أعد المحاولة بعد قليل.</p>
          </>
        )}

        {state === 'done' && !valid && (
          <>
            <ShieldAlert size={40} aria-hidden />
            <h1>لا يوجد سجل لهذا الرمز</h1>
            <p className="vfy__sub">
              هذه الورقة لا تطابق أي وثيقة في هذا النظام. تأكّد من الرمز أدناه، وإن كان صحيحًا فراجع
              الجهة التي أصدرت الورقة.
            </p>
            <p className="vfy__code mono">{code}</p>
          </>
        )}

        {state === 'done' && valid && (
          <>
            <ShieldCheck size={40} aria-hidden />
            <h1>وثيقة صادرة عن هذا النظام</h1>
            <p className="vfy__sub">
              {withdrawn
                ? 'الورقة صادرة عن النظام، لكن الوثيقة لم تعد سارية — راجع حالتها أدناه.'
                : 'طابق الرمز سجلًا في النظام. قارن التفاصيل التالية بما هو مطبوع على الورقة.'}
            </p>

            <dl className="vfy__facts">
              <div><dt><Hash size={14} aria-hidden /> رقم الوثيقة</dt><dd className="mono">{result?.documentNumber ?? '—'}</dd></div>
              <div><dt><FileText size={14} aria-hidden /> النوع</dt><dd>{result?.typeName ?? '—'}</dd></div>
              <div><dt><Building2 size={14} aria-hidden /> الجهة المُصدِرة</dt><dd>{result?.orgUnitName ?? '—'}</dd></div>
              <div><dt><Calendar size={14} aria-hidden /> تاريخ الوثيقة</dt><dd>{dateOf(result?.documentDate ?? null)}</dd></div>
              <div><dt><Calendar size={14} aria-hidden /> تاريخ القيد</dt><dd>{dateOf(result?.registeredAt ?? null)}</dd></div>
              <div><dt>الحالة</dt><dd>{STATUS_AR[result?.status ?? ''] ?? result?.status ?? '—'}</dd></div>
              {result?.title && <div className="vfy__wide"><dt>العنوان</dt><dd>{result.title}</dd></div>}
            </dl>

            <p className="vfy__code mono">{code}</p>
            <p className="vfy__note">
              يعرض هذا التحقق بيانات القيد فقط ولا يكشف محتوى الوثيقة. للاطّلاع على المحتوى راجع الجهة المُصدِرة.
            </p>
          </>
        )}
      </main>
    </div>
  )
}
