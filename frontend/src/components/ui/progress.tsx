import * as React from "react"

import { cn } from "@/lib/utils"

interface ProgressProps extends React.HTMLAttributes<HTMLDivElement> {
  /** 0–100. */
  value?: number | null
}

/**
 * Minimal shadcn-compatible progress bar (same API: `value` + `className`).
 * Fill is width-based off the inline-start edge, so it reads correctly in RTL.
 */
const Progress = React.forwardRef<HTMLDivElement, ProgressProps>(
  ({ className, value, ...props }, ref) => (
    <div
      ref={ref}
      role="progressbar"
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={value ?? 0}
      className={cn(
        "relative h-4 w-full overflow-hidden rounded-full bg-secondary",
        className,
      )}
      {...props}
    >
      <div
        className="h-full rounded-full bg-primary transition-all"
        style={{ inlineSize: `${Math.max(0, Math.min(100, value || 0))}%` }}
      />
    </div>
  ),
)
Progress.displayName = "Progress"

export { Progress }
