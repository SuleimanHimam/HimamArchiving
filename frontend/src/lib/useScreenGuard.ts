import { useEffect, useRef } from 'react'
import { attachScreenGuard, type ScreenGuardOptions } from './screenGuard'

/** Confidentiality levels that make a screen "sensitive" enough to guard. */
const SENSITIVE = new Set(['Confidential', 'HighlyConfidential'])

/** Whether an item's classification (سري / سري للغاية) warrants the capture guard. */
export const isSensitive = (classification?: string | null) => SENSITIVE.has(classification ?? '')

/**
 * Turn the screen-capture guard on for the current page while `active` is true.
 * See `screenGuard.ts` for what it can and cannot do.
 */
export function useScreenGuard(active: boolean, opts: ScreenGuardOptions = {}) {
  const { message, blockPrint, onViolation } = opts

  // Held in a ref so a caller passing an inline callback does not tear the guard down and
  // re-arm it on every render.
  const onViolationRef = useRef(onViolation)
  useEffect(() => { onViolationRef.current = onViolation })

  useEffect(() => {
    if (!active) return
    return attachScreenGuard(document, {
      message, blockPrint,
      onViolation: (reason) => onViolationRef.current?.(reason),
    })
  }, [active, message, blockPrint])
}
