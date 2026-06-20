import { motion } from 'motion/react'
import { auth } from '../lib/auth'
import { ROLE_LABELS } from '../lib/users'

// This section is built with Tailwind utilities (themed to the Diwan tokens) as the
// reference for migrating the rest of the UI.
export default function AboutSettings() {
  const user = auth.getUser()

  const rows: [string, React.ReactNode][] = [
    ['النظام', 'نظام إدارة الوثائق والأرشفة وسير العمل'],
    ['الإصدار', <span className="font-mono">v0.1</span>],
    ['المستخدم', user?.fullName ?? '—'],
    ['البريد الإلكتروني', <span className="font-mono" dir="ltr">{user?.email ?? '—'}</span>],
    ['المسمى الوظيفي', user?.jobTitle ?? '—'],
    ['الأدوار', user?.roles?.map((r) => ROLE_LABELS[r] ?? r).join('، ') || '—'],
    ['التصنيف الأمني', <span className="rounded bg-seal/10 px-2 py-0.5 text-xs font-semibold text-seal">{user?.clearance}</span>],
    ['عدد الصلاحيات', <span className="font-mono">{user?.permissions?.length ?? 0}</span>],
  ]

  return (
    <motion.section
      className="rounded-doc-lg border border-parchment-3 bg-paper p-5 shadow-sm"
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
    >
      <h3 className="mb-4 font-display text-lg text-ink">حول النظام والحساب</h3>
      <dl className="grid grid-cols-[max-content_1fr] gap-x-6 gap-y-2.5 text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="contents">
            <dt className="text-muted-foreground">{label}</dt>
            <dd className="text-ink-text">{value}</dd>
          </div>
        ))}
      </dl>
    </motion.section>
  )
}
