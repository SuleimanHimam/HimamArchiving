import { useState } from 'react'
import { Sun, Moon } from 'lucide-react'
import { getTheme, toggleTheme, type Theme } from '../lib/theme'

/** Light/dark theme toggle for the topbar (Marasil re-skin). */
export default function ThemeToggle() {
  const [theme, setTheme] = useState<Theme>(getTheme())
  return (
    <button
      type="button"
      className="shell__themebtn"
      title={theme === 'dark' ? 'الوضع الفاتح' : 'الوضع الداكن'}
      aria-label="Toggle theme"
      onClick={() => setTheme(toggleTheme())}
    >
      {theme === 'dark' ? <Sun size={15} aria-hidden /> : <Moon size={15} aria-hidden />}
    </button>
  )
}
