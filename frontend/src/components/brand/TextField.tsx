import { forwardRef, useId, type InputHTMLAttributes, type ReactNode } from 'react'

export interface TextFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'size'> {
  label?: string
  /** Inline error message — sets the field to the error state + aria-invalid. */
  error?: string | null
  /** Success/helper confirmation shown under the control. */
  success?: string | null
  /** Neutral helper text (hidden when an error is present). */
  hint?: string
  /** Leading icon (inline-start). */
  icon?: ReactNode
  required?: boolean
}

/**
 * أوراق · Brand text input with label, inline validation, and accessible wiring
 * (aria-invalid, aria-describedby, aria-required). Theme-aware and RTL-safe.
 */
export const TextField = forwardRef<HTMLInputElement, TextFieldProps>(function TextField(
  { label, error, success, hint, icon, required, id, className = '', ...rest },
  ref,
) {
  const autoId = useId()
  const inputId = id ?? autoId
  const msgId = `${inputId}-msg`
  const state = error ? 'error' : success ? 'success' : ''
  const msg = error || success || hint

  return (
    <div className={`brand-field ${state ? `brand-field--${state}` : ''} ${className}`}>
      {label && (
        <label className="brand-field__label" htmlFor={inputId}>
          {label}{required && <span className="brand-field__req" aria-hidden>*</span>}
        </label>
      )}
      <div className="brand-field__control">
        {icon && <span className="brand-field__icon" aria-hidden>{icon}</span>}
        <input
          ref={ref}
          id={inputId}
          className="brand-field__input"
          aria-invalid={error ? true : undefined}
          aria-describedby={msg ? msgId : undefined}
          aria-required={required || undefined}
          required={required}
          {...rest}
        />
      </div>
      {msg && <span id={msgId} className="brand-field__msg" role={error ? 'alert' : undefined}>{msg}</span>}
    </div>
  )
})
