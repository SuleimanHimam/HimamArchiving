import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from 'react'

type Variant = 'primary' | 'secondary' | 'accent' | 'danger' | 'ghost'
type Size = 'sm' | 'md' | 'lg'

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant
  size?: Size
  /** Stretch to the full width of the container. */
  block?: boolean
  /** Show a spinner and disable interaction. */
  loading?: boolean
  /** Icon rendered before the label (inline-start). */
  icon?: ReactNode
}

/**
 * أوراق · Brand button. Semantic <button>, keyboard-focusable with a visible
 * focus ring, RTL-safe, and theme-aware via CSS variables.
 */
export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { variant = 'primary', size = 'md', block, loading, icon, disabled, className = '', children, ...rest },
  ref,
) {
  const classes = [
    'brand-btn',
    variant !== 'primary' && `brand-btn--${variant}`,
    size !== 'md' && `brand-btn--${size}`,
    block && 'brand-btn--block',
    className,
  ].filter(Boolean).join(' ')

  return (
    <button ref={ref} className={classes} disabled={disabled || loading} aria-busy={loading || undefined} {...rest}>
      {loading && <span className="brand-btn__spinner" aria-hidden />}
      {!loading && icon}
      {children}
    </button>
  )
})
