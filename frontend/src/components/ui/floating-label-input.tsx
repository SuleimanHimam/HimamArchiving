import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";
import * as React from "react";
import { useId } from "react";

interface FloatingLabelInputProps
  extends Omit<React.ComponentProps<"input">, "placeholder"> {
  /** Text shown inside the field, floating up on focus / when filled. */
  label: string;
  /** Extra classes for the wrapper (e.g. width / grid placement). */
  wrapperClassName?: string;
}

/**
 * Text field whose label sits inside like a placeholder and animates up to a
 * caption when the field is focused or has a value. RTL-safe (logical props).
 *
 * The animation keys off `:placeholder-shown`, so the underlying input keeps an
 * empty placeholder (" ") — the visible label is the sibling <label>.
 */
const FloatingLabelInput = React.forwardRef<
  HTMLInputElement,
  FloatingLabelInputProps
>(({ label, id, className, wrapperClassName, ...props }, ref) => {
  const generatedId = useId();
  const inputId = id ?? generatedId;
  return (
    <div className={cn("group relative", wrapperClassName)}>
      <label
        htmlFor={inputId}
        className="origin-start absolute top-1/2 block -translate-y-1/2 cursor-text px-1 text-sm text-muted-foreground/70 transition-all group-focus-within:pointer-events-none group-focus-within:top-0 group-focus-within:cursor-default group-focus-within:text-xs group-focus-within:font-medium group-focus-within:text-foreground has-[+input:not(:placeholder-shown)]:pointer-events-none has-[+input:not(:placeholder-shown)]:top-0 has-[+input:not(:placeholder-shown)]:cursor-default has-[+input:not(:placeholder-shown)]:text-xs has-[+input:not(:placeholder-shown)]:font-medium has-[+input:not(:placeholder-shown)]:text-foreground"
      >
        <span className="inline-flex bg-background px-2">{label}</span>
      </label>
      <Input id={inputId} ref={ref} placeholder=" " className={className} {...props} />
    </div>
  );
});
FloatingLabelInput.displayName = "FloatingLabelInput";

export { FloatingLabelInput };
