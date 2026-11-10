import type { HTMLAttributes, ReactNode } from 'react'

export interface CardProps extends HTMLAttributes<HTMLDivElement> {
  /** Lift + deepen the shadow on hover (use for clickable cards). */
  interactive?: boolean
}

/** أوراق · Brand surface card. Compose with Card.Header / Card.Body / Card.Footer. */
export function Card({ interactive, className = '', children, ...rest }: CardProps) {
  return (
    <div className={`brand-card ${interactive ? 'brand-card--hover' : ''} ${className}`} {...rest}>
      {children}
    </div>
  )
}

function Header({ title, subtitle, actions, children }: { title?: ReactNode; subtitle?: ReactNode; actions?: ReactNode; children?: ReactNode }) {
  return (
    <div className="brand-card__head">
      <div>
        {title && <h3 className="brand-card__title">{title}</h3>}
        {subtitle && <div className="brand-card__sub">{subtitle}</div>}
        {children}
      </div>
      {actions}
    </div>
  )
}

const Body = ({ className = '', children, ...rest }: HTMLAttributes<HTMLDivElement>) =>
  <div className={`brand-card__body ${className}`} {...rest}>{children}</div>

const Footer = ({ className = '', children, ...rest }: HTMLAttributes<HTMLDivElement>) =>
  <div className={`brand-card__foot ${className}`} {...rest}>{children}</div>

Card.Header = Header
Card.Body = Body
Card.Footer = Footer
