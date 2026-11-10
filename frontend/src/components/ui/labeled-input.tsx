import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";
import { Check, Eye, EyeOff, Plus, SearchX } from "lucide-react";
import { Children, Fragment, isValidElement, useEffect, useId, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useAnchoredPopup } from "./anchored-popup";

/* ────────────────────────────────────────────────────────────────────────────
   Labeled field family — one floating-label look across the three native
   controls the app's forms use (input / textarea / select), so a form can be
   migrated by swapping tags without rewiring value/onChange.

   • input / textarea rest the label inside and float it up on focus or when
     filled (keyed off :placeholder-shown, so the control keeps a " " placeholder).
   • select always shows a value, so its label stays floated.
   ──────────────────────────────────────────────────────────────────────────── */

/** Floating-label classes. The animation itself lives in `.floating-label`
 *  (styles/tailwind.css) which animates transform + color only (GPU-accelerated,
 *  no `top`/`font-size` reflow). `area` = textarea geometry; `error` = red state.
 *  Selects float always — handled in CSS via `.group:has(select)`. */
function labelClass(opts: { area?: boolean; error?: boolean }) {
  return cn(
    "floating-label",
    opts.area && "floating-label--area",
    opts.error && "floating-label--error",
  );
}

function LabelSpan({ label, required }: { label: string; required?: boolean }) {
  return (
    <span className="floating-label__text">
      {label}
      {required && (
        <span className="ms-0.5 text-destructive" aria-hidden="true">
          *
        </span>
      )}
    </span>
  );
}

function FieldMessages({ id, error, hint }: { id: string; error?: string; hint?: string }) {
  return (
    <>
      {hint && !error && (
        <p id={`${id}-hint`} className="px-1 text-xs text-muted-foreground">
          {hint}
        </p>
      )}
      {error && (
        <p id={`${id}-error`} role="alert" className="px-1 text-xs text-destructive">
          {error}
        </p>
      )}
    </>
  );
}

interface LabeledFieldBase {
  label: string;
  required?: boolean;
  containerClassName?: string;
  error?: string;
  hint?: string;
}

/* ── Text input ─────────────────────────────────────────────────────────────── */
interface LabeledInputProps extends React.ComponentProps<"input">, LabeledFieldBase {}

export function LabeledInput({
  label, required, className, containerClassName, error, hint, ...props
}: LabeledInputProps) {
  const id = useId();
  return (
    <div className={cn("space-y-1", containerClassName)}>
      <div className="group relative">
        <label htmlFor={id} className={labelClass({ error: !!error })}>
          <LabelSpan label={label} required={required} />
        </label>
        <Input
          id={id}
          placeholder=" "
          aria-required={required}
          aria-invalid={!!error}
          aria-describedby={error ? `${id}-error` : hint ? `${id}-hint` : undefined}
          className={cn(
            "h-12 pt-3",
            error && "border-destructive focus-visible:border-destructive focus-visible:ring-destructive/20",
            className,
          )}
          {...props}
        />
      </div>
      <FieldMessages id={id} error={error} hint={hint} />
    </div>
  );
}

/* ── Textarea ───────────────────────────────────────────────────────────────── */
interface LabeledTextareaProps extends React.ComponentProps<"textarea">, LabeledFieldBase {}

export function LabeledTextarea({
  label, required, className, containerClassName, error, hint, rows = 3, ...props
}: LabeledTextareaProps) {
  const id = useId();
  return (
    <div className={cn("space-y-1", containerClassName)}>
      <div className="group relative">
        <label htmlFor={id} className={labelClass({ area: true, error: !!error })}>
          <LabelSpan label={label} required={required} />
        </label>
        <textarea
          id={id}
          rows={rows}
          placeholder=" "
          aria-required={required}
          aria-invalid={!!error}
          aria-describedby={error ? `${id}-error` : hint ? `${id}-hint` : undefined}
          className={cn(
            "flex w-full rounded-lg border border-input bg-background px-3 pt-4 pb-2 text-sm text-foreground shadow-sm shadow-black/5 transition-shadow placeholder:text-muted-foreground/70 focus-visible:border-ring focus-visible:outline-none focus-visible:ring-[3px] focus-visible:ring-ring/20 disabled:cursor-not-allowed disabled:opacity-50",
            error && "border-destructive focus-visible:border-destructive focus-visible:ring-destructive/20",
            className,
          )}
          {...props}
        />
      </div>
      <FieldMessages id={id} error={error} hint={hint} />
    </div>
  );
}

/* ── Password (bordered, floating label, eye toggle) ────────────────────────── */
interface LabeledPasswordProps
  extends Omit<React.ComponentProps<"input">, "type">, LabeledFieldBase {
  /** Extra content rendered below the field (e.g. strength meter / match bar). */
  children?: React.ReactNode;
}

export function LabeledPassword({
  label, required, className, containerClassName, error, hint, children, ...props
}: LabeledPasswordProps) {
  const id = useId();
  const [show, setShow] = useState(false);
  return (
    <div className={cn("space-y-1", containerClassName)}>
      <div className="group relative">
        <label htmlFor={id} className={labelClass({ error: !!error })}>
          <LabelSpan label={label} required={required} />
        </label>
        <Input
          id={id}
          type={show ? "text" : "password"}
          placeholder=" "
          aria-required={required}
          aria-invalid={!!error}
          aria-describedby={error ? `${id}-error` : hint ? `${id}-hint` : undefined}
          className={cn(
            "h-12 pt-3 pe-9",
            error && "border-destructive focus-visible:border-destructive focus-visible:ring-destructive/20",
            className,
          )}
          {...props}
        />
        <button
          type="button"
          tabIndex={-1}
          onClick={() => setShow((s) => !s)}
          aria-label={show ? "إخفاء كلمة المرور" : "إظهار كلمة المرور"}
          title={show ? "إخفاء كلمة المرور" : "إظهار كلمة المرور"}
          className="pw-reveal absolute top-1/2 -translate-y-1/2 grid size-7 place-items-center rounded-md text-muted-foreground/70 hover:text-foreground"
          style={{ insetInlineEnd: "0.375rem" }}
        >
          {show ? <EyeOff size={16} /> : <Eye size={16} />}
        </button>
      </div>
      {children}
      <FieldMessages id={id} error={error} hint={hint} />
    </div>
  );
}

/* ── Native select (label always floated) ───────────────────────────────────── */
interface LabeledSelectProps extends React.ComponentProps<"select">, LabeledFieldBase {
  /** Force the searchable listbox on or off. Left undefined, any field with more than
   *  SEARCHABLE_FROM options becomes searchable on its own. */
  searchable?: boolean;
}

/** Above this many options, scanning a native dropdown stops being realistic — departments,
 *  document types and staff lists all run past it. */
const SEARCHABLE_FROM = 8;

export function LabeledSelect({
  label, required, className, containerClassName, error, hint, children, searchable, ...props
}: LabeledSelectProps) {
  const id = useId();
  const options = collectOptions(children);
  if (searchable ?? options.length > SEARCHABLE_FROM) {
    return (
      <SearchableSelect
        label={label} required={required} className={className} containerClassName={containerClassName}
        error={error} hint={hint} options={options} {...props}
      >
        {children}
      </SearchableSelect>
    );
  }
  return (
    <div className={cn("space-y-1", containerClassName)}>
      <div className="group relative">
        <label htmlFor={id} className={labelClass({ error: !!error })}>
          <LabelSpan label={label} required={required} />
        </label>
        <select
          id={id}
          aria-required={required}
          aria-invalid={!!error}
          aria-describedby={error ? `${id}-error` : hint ? `${id}-hint` : undefined}
          className={cn(
            "flex h-12 w-full appearance-none rounded-lg border border-input bg-background px-3 pt-3 pe-8 text-sm text-foreground shadow-sm shadow-black/5 transition-shadow focus-visible:border-ring focus-visible:outline-none focus-visible:ring-[3px] focus-visible:ring-ring/20 disabled:cursor-not-allowed disabled:opacity-50",
            error && "border-destructive focus-visible:border-destructive focus-visible:ring-destructive/20",
            className,
          )}
          {...props}
        >
          {children}
        </select>
        {/* chevron — pointer-events-none so clicks fall through to the select */}
        <svg
          className="pointer-events-none absolute top-1/2 size-4 -translate-y-1/2 text-muted-foreground/70"
          style={{ insetInlineEnd: "0.75rem" }}
          viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"
        >
          <path d="m6 9 6 6 6-6" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </div>
      <FieldMessages id={id} error={error} hint={hint} />
    </div>
  );
}

/* ── Searchable select ──────────────────────────────────────────────────────
   Same props as LabeledSelect — children stay plain <option> elements — but the
   control becomes type-to-filter once the list is long. A hidden real <select>
   stays mounted and carries the value, so `name`/FormData, `onChange(e.target.value)`
   and `e.currentTarget` all behave exactly as they did before: the change is
   dispatched on that element rather than synthesised. */

interface SelectOption { value: string; label: string; disabled?: boolean }

/** Flattens <option> children (including those inside <optgroup> and fragments). */
function collectOptions(children: React.ReactNode): SelectOption[] {
  const out: SelectOption[] = [];
  const walk = (nodes: React.ReactNode) => {
    Children.forEach(nodes, (child) => {
      if (!isValidElement(child)) return;
      if (child.type === "option") {
        const p = child.props as { value?: string | number; children?: React.ReactNode; disabled?: boolean };
        out.push({
          value: String(p.value ?? ""),
          label: typeof p.children === "string" || typeof p.children === "number"
            ? String(p.children)
            : String(p.value ?? ""),
          disabled: p.disabled,
        });
      } else if (child.type === "optgroup" || child.type === Fragment) {
        walk((child.props as { children?: React.ReactNode }).children);
      }
    });
  };
  walk(children);
  return out;
}

function SearchableSelect({
  label, required, className, containerClassName, error, hint, options, children,
  disabled, value, defaultValue, onChange, ...props
}: LabeledSelectProps & { options: SelectOption[] }) {
  const id = useId();
  const listId = `${id}-listbox`;
  const [open, setOpen] = useState(false);
  const [q, setQ] = useState("");
  const [active, setActive] = useState(-1);
  const boxRef = useRef<HTMLDivElement>(null);
  const selectRef = useRef<HTMLSelectElement>(null);
  const fieldRef = useRef<HTMLInputElement>(null);
  /* Portalled and anchored: inside a dialog whose body scrolls, an absolutely-positioned list is
     clipped at the dialog's edge and most options cannot be reached. */
  const popup = useAnchoredPopup(fieldRef, open, 240);

  useEffect(() => {
    if (!open) return;
    const close = () => { setOpen(false); setQ(""); setActive(-1); };
    const onDown = (e: MouseEvent) => {
      const t = e.target as Node;
      // The listbox is portalled out of this subtree, so check it separately.
      if (boxRef.current?.contains(t) || document.getElementById(listId)?.contains(t)) return;
      close();
    };
    document.addEventListener("mousedown", onDown);
    openComboboxes.add(close);
    return () => { document.removeEventListener("mousedown", onDown); openComboboxes.delete(close); };
  }, [open, listId]);

  const current = String(value ?? defaultValue ?? "");
  const selected = options.find((o) => o.value === current);
  const needle = q.trim().toLocaleLowerCase();
  const filtered = needle
    ? options.filter((o) => o.label.toLocaleLowerCase().includes(needle))
    : options;

  /* Writes through the hidden <select> using the native value setter, then fires a real
     `change`. React picks that up and calls the caller's onChange with a genuine event —
     no hand-rolled `{ target: { value } }` object that breaks on e.currentTarget. */
  function commit(v: string) {
    const el = selectRef.current;
    setOpen(false); setQ(""); setActive(-1);
    if (!el) return;
    const setter = Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, "value")?.set;
    setter?.call(el, v);
    el.dispatchEvent(new Event("change", { bubbles: true }));
  }

  function keyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === "ArrowDown" || e.key === "ArrowUp") {
      e.preventDefault();
      if (!open) { setOpen(true); setActive(0); return; }
      const n = filtered.length;
      if (!n) return;
      setActive((i) => (e.key === "ArrowDown" ? (i + 1) % n : (i <= 0 ? n - 1 : i - 1)));
      return;
    }
    if (e.key === "Enter" && open) {
      const pick = active >= 0 ? filtered[active] : filtered.length === 1 ? filtered[0] : undefined;
      if (pick && !pick.disabled) { e.preventDefault(); commit(pick.value); }
    }
  }

  return (
    <div className={cn("space-y-1", containerClassName)}>
      <div className="group relative" ref={boxRef}>
        {/* The real control: hidden from view and from AT, but present in the form. */}
        <select
          ref={selectRef} tabIndex={-1} aria-hidden="true"
          className="sr-only" value={value} defaultValue={defaultValue} onChange={onChange}
          disabled={disabled} {...props}
        >
          {children}
        </select>
        <label htmlFor={id} className={labelClass({ error: !!error })}>
          <LabelSpan label={label} required={required} />
        </label>
        <Input
          id={id}
          ref={fieldRef}
          role="combobox"
          aria-expanded={open}
          aria-controls={open ? listId : undefined}
          aria-autocomplete="list"
          aria-required={required}
          aria-invalid={!!error}
          aria-activedescendant={open && active >= 0 ? `${id}-opt-${active}` : undefined}
          aria-describedby={error ? `${id}-error` : hint ? `${id}-hint` : undefined}
          placeholder=" "
          autoComplete="off"
          disabled={disabled}
          readOnly={!open}
          // Closed, the field reads as a select showing its value; open, it is a search box.
          value={open ? q : (selected?.label ?? "")}
          className={cn("h-12 pt-3 pe-8 cursor-pointer", open && "cursor-text",
            error && "border-destructive focus-visible:border-destructive focus-visible:ring-destructive/20",
            className)}
          onChange={(e) => { setQ(e.target.value); setActive(-1); }}
          onFocus={() => setOpen(true)}
          onClick={() => setOpen(true)}
          onKeyDown={keyDown}
        />
        <svg
          className={cn("pointer-events-none absolute top-1/2 size-4 -translate-y-1/2 text-muted-foreground/70 transition-transform",
            open && "rotate-180")}
          style={{ insetInlineEnd: "0.75rem" }}
          viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"
        >
          <path d="m6 9 6 6 6-6" strokeLinecap="round" strokeLinejoin="round" />
        </svg>

        {open && !disabled && popup && createPortal(
          <ul
            id={listId}
            role="listbox"
            style={popup}
            className={cn(
              "z-[200] m-0 list-none ps-0 overflow-auto rounded-xl border border-border/70",
              "bg-popover p-1.5 text-popover-foreground shadow-lg shadow-black/20",
              "animate-in fade-in-0 zoom-in-95 duration-150",
            )}
          >
            {filtered.map((o, i) => {
              const picked = o.value === current;
              return (
                <li key={`${o.value}-${i}`}>
                  <button
                    type="button"
                    id={`${id}-opt-${i}`}
                    role="option"
                    aria-selected={picked}
                    disabled={o.disabled}
                    onMouseDown={(e) => { e.preventDefault(); if (!o.disabled) commit(o.value); }}
                    onMouseEnter={() => setActive(i)}
                    className={cn(
                      "flex w-full appearance-none items-center gap-2.5 border-0 bg-transparent",
                      "rounded-lg px-3 py-2.5 text-start text-sm text-foreground",
                      "transition-colors duration-100 cursor-pointer disabled:opacity-50",
                      i === active && "bg-accent text-accent-foreground",
                      picked && "font-medium",
                    )}
                  >
                    <Check className={cn("size-4 shrink-0 text-primary transition-opacity", !picked && "opacity-0")} aria-hidden="true" />
                    <span className="truncate">{o.label}</span>
                  </button>
                </li>
              );
            })}
            {filtered.length === 0 && (
              <li className="flex flex-col items-center gap-1.5 px-3 py-6 text-center">
                <SearchX className="size-5 text-muted-foreground/60" aria-hidden="true" />
                <span className="text-sm text-muted-foreground">لا نتائج مطابقة</span>
              </li>
            )}
          </ul>,
          document.body,
        )}
      </div>
      <FieldMessages id={id} error={error} hint={hint} />
    </div>
  );
}

/* Escape must dismiss an open suggestion list WITHOUT closing the dialog around it.
   Radix's DismissableLayer listens on `document` with { capture: true }, so it sees the
   key before any React handler on the input and there is no ordering trick that lets a
   target-phase handler win. The one thing that does work is registering our own capture
   listener first — at module load, which happens at app bootstrap, long before any dialog
   mounts — and stopping the event there while a list is open. */
const openComboboxes = new Set<() => void>();
if (typeof document !== "undefined") {
  document.addEventListener("keydown", (e) => {
    if (e.key !== "Escape" || openComboboxes.size === 0) return;
    e.preventDefault();
    e.stopImmediatePropagation();
    for (const close of [...openComboboxes]) close();
  }, { capture: true });
}

/* ── Combobox ───────────────────────────────────────────────────────────────────
   Type a value OR pick a suggestion. The popup is ours rather than a native
   <datalist>: the browser's list cannot be styled, ignores the app's theme and
   renders detached from the field. This keeps the LabeledInput look (floating
   label, h-12, RTL) and adds a themed listbox with keyboard support.

   Free text is a first-class outcome, not an escape hatch — use where the value
   may legitimately be something the list does not contain yet. */
interface LabeledComboboxProps
  extends Omit<React.ComponentProps<"input">, "value" | "onChange">, LabeledFieldBase {
  /** Suggestions. Typing something absent is still valid. */
  options: string[];
  value: string;
  onValueChange: (value: string) => void;
  /** Row offered when the typed text matches no option; omit to hide it. */
  createLabel?: (typed: string) => string;
  emptyLabel?: string;
}

export function LabeledCombobox({
  label, required, options, value, onValueChange, createLabel,
  emptyLabel = "لا نتائج مطابقة",
  className, containerClassName, error, hint, disabled, onKeyDown, ...props
}: LabeledComboboxProps) {
  const id = useId();
  const listId = `${id}-listbox`;
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(-1);
  const boxRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const close = () => { setOpen(false); setActive(-1); };
    const onDown = (e: MouseEvent) => {
      if (boxRef.current && !boxRef.current.contains(e.target as Node)) close();
    };
    document.addEventListener("mousedown", onDown);
    openComboboxes.add(close);           // claims Escape while the list is open
    return () => {
      document.removeEventListener("mousedown", onDown);
      openComboboxes.delete(close);
    };
  }, [open]);

  // Typing filters; once the value IS one of the options, show the whole list again so
  // reopening the field lets you browse rather than stranding you on a single match.
  const q = value.trim().toLocaleLowerCase();
  const exact = options.some((o) => o.toLocaleLowerCase() === q);
  const filtered = q && !exact ? options.filter((o) => o.toLocaleLowerCase().includes(q)) : options;
  const showCreate = !!createLabel && !!value.trim() && !exact;

  const commit = (v: string) => { onValueChange(v); setOpen(false); setActive(-1); };

  /** Emphasise the typed run inside an option, so it is obvious *why* a row matched. */
  const highlight = (text: string) => {
    if (!q || exact) return text;
    const at = text.toLocaleLowerCase().indexOf(q);
    if (at < 0) return text;
    return (
      <>
        {text.slice(0, at)}
        <mark className="bg-transparent font-semibold text-primary">{text.slice(at, at + q.length)}</mark>
        {text.slice(at + q.length)}
      </>
    );
  };

  function keyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === "ArrowDown" || e.key === "ArrowUp") {
      e.preventDefault();
      if (!open) { setOpen(true); setActive(0); return; }
      const n = filtered.length;
      if (!n) return;
      setActive((i) => (e.key === "ArrowDown" ? (i + 1) % n : (i <= 0 ? n - 1 : i - 1)));
      return;
    }
    // Escape is handled by the module-level capture listener above, which has to run before
    // Radix's own — nothing to do here.
    // Enter only belongs to the popup while a row is highlighted; otherwise it is the
    // caller's (submit-on-Enter) to handle.
    if (e.key === "Enter" && open && active >= 0 && filtered[active]) {
      e.preventDefault();
      commit(filtered[active]);
      return;
    }
    onKeyDown?.(e);
  }

  return (
    <div className={cn("space-y-1", containerClassName)}>
      <div className="group relative" ref={boxRef}>
        <label htmlFor={id} className={labelClass({ error: !!error })}>
          <LabelSpan label={label} required={required} />
        </label>
        <Input
          id={id}
          role="combobox"
          aria-expanded={open}
          aria-controls={open ? listId : undefined}
          aria-autocomplete="list"
          aria-activedescendant={open && active >= 0 ? `${id}-opt-${active}` : undefined}
          placeholder=" "
          autoComplete="off"
          disabled={disabled}
          value={value}
          aria-required={required}
          aria-invalid={!!error}
          aria-describedby={error ? `${id}-error` : hint ? `${id}-hint` : undefined}
          className={cn(
            "h-12 pt-3 pe-8",
            error && "border-destructive focus-visible:border-destructive focus-visible:ring-destructive/20",
            className,
          )}
          onChange={(e) => { onValueChange(e.target.value); setOpen(true); setActive(-1); }}
          onFocus={() => setOpen(true)}
          onClick={() => setOpen(true)}
          onKeyDown={keyDown}
          {...props}
        />
        {/* chevron — pointer-events-none so clicks fall through to the input */}
        <svg
          className={cn(
            "pointer-events-none absolute top-1/2 size-4 -translate-y-1/2 text-muted-foreground/70 transition-transform",
            open && "rotate-180",
          )}
          style={{ insetInlineEnd: "0.75rem" }}
          viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"
        >
          <path d="m6 9 6 6 6-6" strokeLinecap="round" strokeLinejoin="round" />
        </svg>

        {open && !disabled && (filtered.length > 0 || showCreate || !!emptyLabel) && (
          <ul
            id={listId}
            role="listbox"
            className={cn(
              "absolute start-0 end-0 z-50 mt-1.5 max-h-60 overflow-auto rounded-xl border border-border/70",
              "bg-popover/95 p-1.5 text-popover-foreground shadow-lg shadow-black/5 backdrop-blur-sm",
              "animate-in fade-in-0 zoom-in-95 slide-in-from-top-1 duration-150",
            )}
          >
            {filtered.map((o, i) => {
              const picked = o === value;
              return (
                <li key={o}>
                  <button
                    type="button"
                    id={`${id}-opt-${i}`}
                    role="option"
                    aria-selected={picked}
                    // mousedown, not click: the input's blur would close the list first.
                    onMouseDown={(e) => { e.preventDefault(); commit(o); }}
                    onMouseEnter={() => setActive(i)}
                    className={cn(
                      // Explicit reset — a bare <button> otherwise picks up the platform's
                      // border/background chrome and each row renders as a boxed control.
                      "flex w-full appearance-none items-center gap-2.5 border-0 bg-transparent",
                      "rounded-lg px-3 py-2.5 text-start text-sm text-foreground",
                      "transition-colors duration-100 cursor-pointer",
                      i === active && "bg-accent text-accent-foreground",
                      picked && "font-medium",
                    )}
                  >
                    <Check
                      className={cn("size-4 shrink-0 text-primary transition-opacity", !picked && "opacity-0")}
                      aria-hidden="true"
                    />
                    <span className="truncate">{highlight(o)}</span>
                  </button>
                </li>
              );
            })}

            {filtered.length === 0 && !showCreate && (
              <li className="flex flex-col items-center gap-1.5 px-3 py-6 text-center">
                <SearchX className="size-5 text-muted-foreground/60" aria-hidden="true" />
                <span className="text-sm text-muted-foreground">{emptyLabel}</span>
              </li>
            )}

            {showCreate && (
              <li className={cn(filtered.length > 0 && "mt-1.5 border-t border-border/60 pt-1.5")}>
                <button
                  type="button"
                  onMouseDown={(e) => { e.preventDefault(); commit(value.trim()); }}
                  className={cn(
                    "flex w-full appearance-none items-center gap-2.5 border-0 bg-transparent",
                    "rounded-lg px-3 py-2.5 text-start text-sm text-foreground",
                    "transition-colors duration-100 cursor-pointer hover:bg-accent",
                  )}
                >
                  <span className="grid size-5 shrink-0 place-items-center rounded-md bg-primary/10 text-primary">
                    <Plus className="size-3.5" aria-hidden="true" />
                  </span>
                  <span className="truncate">{createLabel!(value.trim())}</span>
                </button>
              </li>
            )}
          </ul>
        )}
      </div>
      <FieldMessages id={id} error={error} hint={hint} />
    </div>
  );
}
