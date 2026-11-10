import { useLayoutEffect, useState, type CSSProperties, type RefObject } from 'react'

/**
 * Position for a popup that must escape its container's `overflow`.
 *
 * A dropdown rendered as an absolutely-positioned child is clipped by any scrolling ancestor —
 * inside a dialog body (`overflow: auto`) the option list is cut off at the dialog's edge and
 * most of it becomes unreachable. Rendering it in a portal on `document.body` with `position:
 * fixed` sidesteps every ancestor's overflow and stacking context; this hook supplies the
 * coordinates, tracking scroll and resize so the popup stays glued to its field.
 *
 * It also flips the popup above the field when there is more room there — near the bottom of a
 * viewport a downward list would otherwise open into nothing.
 */
/** Assignable straight to a `style` prop. */
export type AnchoredStyle = CSSProperties & {
  position: 'fixed'
  left: number
  top: number
  width: number
  maxHeight: number
}

const GAP = 4
const MIN_SPACE = 140

export function useAnchoredPopup(
  anchorRef: RefObject<HTMLElement | null>,
  open: boolean,
  preferredMaxHeight = 240,
): AnchoredStyle | null {
  const [style, setStyle] = useState<AnchoredStyle | null>(null)

  useLayoutEffect(() => {
    if (!open) return

    const place = () => {
      const el = anchorRef.current
      if (!el) return
      const r = el.getBoundingClientRect()
      const below = window.innerHeight - r.bottom - GAP
      const above = r.top - GAP
      const openUp = below < MIN_SPACE && above > below
      const maxHeight = Math.max(MIN_SPACE, Math.min(preferredMaxHeight, openUp ? above : below))
      setStyle({
        position: 'fixed',
        left: r.left,
        top: openUp ? r.top - GAP - maxHeight : r.bottom + GAP,
        width: r.width,
        maxHeight,
      })
    }

    /* Measuring the field and then storing the result is the whole job of this hook, and a
       layout effect is the only place the DOM is settled enough to measure. */
    place()
    // `true` = capture, so scrolling of any ancestor (not just the window) repositions it.
    window.addEventListener('scroll', place, true)
    window.addEventListener('resize', place)
    return () => {
      window.removeEventListener('scroll', place, true)
      window.removeEventListener('resize', place)
    }
  }, [anchorRef, open, preferredMaxHeight])

  // Never hand back a stale position from the last time it was open — the caller renders nothing
  // while closed, so there is no reason to clear the state (and no extra render to pay for).
  return open ? style : null
}
