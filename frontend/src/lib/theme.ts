/* Light/dark theme handling for the Marasil re-skin.
   Drives both our CSS tokens (data-theme on <html>) and Tailwind's dark: variant (.dark class). */
const KEY = 'marasil.theme'
export type Theme = 'light' | 'dark'

export function getTheme(): Theme {
  return (localStorage.getItem(KEY) as Theme) === 'dark' ? 'dark' : 'light'
}

export function applyTheme(t: Theme): void {
  const el = document.documentElement
  el.dataset.theme = t
  el.classList.toggle('dark', t === 'dark')
  localStorage.setItem(KEY, t)
}

export function toggleTheme(): Theme {
  const next: Theme = getTheme() === 'dark' ? 'light' : 'dark'
  applyTheme(next)
  return next
}

// Apply the saved theme as early as possible (this module is imported in main.tsx).
applyTheme(getTheme())
