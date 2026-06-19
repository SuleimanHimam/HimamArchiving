import { checkPassword, PASSWORD_RULES } from '../lib/passwordStrength'
import './passwordstrength.css'

const LEVEL_LABEL = ['ضعيفة جدًا', 'ضعيفة', 'متوسطة', 'جيدة', 'قوية']
const LEVEL_CLASS = ['lvl-0', 'lvl-1', 'lvl-2', 'lvl-3', 'lvl-4']

export default function PasswordStrengthMeter({ password }: { password: string }) {
  if (!password) return null
  const c = checkPassword(password)
  const score = [c.minLength, c.hasUpper, c.hasLower, c.hasDigit, c.hasSymbol].filter(Boolean).length

  return (
    <div className="pwsm">
      <div className="pwsm__bar">
        {[0, 1, 2, 3, 4].map((i) => (
          <span key={i} className={`pwsm__seg ${i < score ? LEVEL_CLASS[score] : ''}`} />
        ))}
      </div>
      <span className={`pwsm__level ${LEVEL_CLASS[score]}`}>{LEVEL_LABEL[score]}</span>
      <ul className="pwsm__rules">
        {PASSWORD_RULES.map((r) => (
          <li key={r.key} className={c[r.key] ? 'is-met' : ''}>
            {c[r.key] ? '✓' : '○'} {r.label}
          </li>
        ))}
      </ul>
    </div>
  )
}
