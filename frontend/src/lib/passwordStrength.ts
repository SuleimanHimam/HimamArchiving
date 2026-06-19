export interface PasswordCheck {
  minLength: boolean
  hasUpper: boolean
  hasLower: boolean
  hasDigit: boolean
  hasSymbol: boolean
}

export function checkPassword(pw: string): PasswordCheck {
  return {
    minLength: pw.length >= 8,
    hasUpper:  /[A-Z]/.test(pw),
    hasLower:  /[a-z]/.test(pw),
    hasDigit:  /[0-9]/.test(pw),
    hasSymbol: /[^A-Za-z0-9]/.test(pw),
  }
}

export function isStrongPassword(pw: string): boolean {
  const c = checkPassword(pw)
  return c.minLength && c.hasUpper && c.hasLower && c.hasDigit && c.hasSymbol
}

export function passwordScore(pw: string): number {
  if (!pw) return 0
  const c = checkPassword(pw)
  return [c.minLength, c.hasUpper, c.hasLower, c.hasDigit, c.hasSymbol].filter(Boolean).length
}

export const PASSWORD_RULES: { key: keyof PasswordCheck; label: string }[] = [
  { key: 'minLength', label: '8 أحرف على الأقل' },
  { key: 'hasUpper',  label: 'حرف كبير (A-Z)' },
  { key: 'hasLower',  label: 'حرف صغير (a-z)' },
  { key: 'hasDigit',  label: 'رقم (0-9)' },
  { key: 'hasSymbol', label: 'رمز خاص (!@#$)' },
]
