import type { ReactNode } from 'react'

export interface NavLinkItem { label: string; href?: string; active?: boolean; onClick?: () => void }

export interface NavbarProps {
  /** Brand wordmark (Arabic primary). */
  brand?: ReactNode
  /** Single/double-character glyph shown in the gradient logo tile. */
  logoText?: string
  links?: NavLinkItem[]
  /** Right-aligned actions (search, quick-create, avatar…). */
  actions?: ReactNode
}

/**
 * أوراق · Reusable brand top navigation. Semantic <nav> with a labelled landmark
 * and aria-current on the active link. Theme-aware and RTL-safe.
 */
export function Navbar({ brand = 'سند', logoText = 'س', links = [], actions }: NavbarProps) {
  return (
    <nav className="brand-nav" aria-label="التنقل الرئيسي">
      <span className="brand-nav__brand">
        <span className="brand-nav__logo" aria-hidden>{logoText}</span>
        {brand}
      </span>
      {links.length > 0 && (
        <div className="brand-nav__links">
          {links.map((l) => (
            <a
              key={l.label}
              href={l.href ?? '#'}
              onClick={l.onClick}
              className={`brand-nav__link ${l.active ? 'is-active' : ''}`}
              aria-current={l.active ? 'page' : undefined}
            >
              {l.label}
            </a>
          ))}
        </div>
      )}
      <span className="brand-nav__spacer" />
      {actions}
    </nav>
  )
}
