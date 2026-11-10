import {
  createContext, useCallback, useContext, useRef, useState, type ReactNode,
} from "react";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";

/* Imperative, promise-based replacement for window.confirm / window.prompt.
   Mirrors the toast provider: mount <ConfirmProvider> once, then anywhere call
   `const { confirm, prompt } = useConfirmDialog()` and `await confirm(...)`.
   Only one dialog shows at a time (native confirm/prompt were the same). */

export interface ConfirmOptions {
  title?: string;
  message?: ReactNode;
  confirmText?: string;
  cancelText?: string;
  /** Paints the confirm button red for delete/irreversible actions. */
  destructive?: boolean;
}

export interface PromptOptions {
  title?: string;
  message?: ReactNode;
  label?: string;
  defaultValue?: string;
  placeholder?: string;
  confirmText?: string;
  cancelText?: string;
  /** Written left-to-right (URLs, codes); Arabic labels stay RTL when false. */
  ltr?: boolean;
}

export interface ChooseOption {
  key: string;
  label: string;
  description?: string;
}

export interface ChooseOptions {
  title?: string;
  message?: ReactNode;
  options: ChooseOption[];
  cancelText?: string;
}

interface ConfirmApi {
  confirm: (opts: string | ConfirmOptions) => Promise<boolean>;
  prompt: (opts: string | PromptOptions) => Promise<string | null>;
  /** Pick one of several actions. Resolves the chosen key, or null if dismissed. */
  choose: (opts: ChooseOptions) => Promise<string | null>;
}

const Ctx = createContext<ConfirmApi | null>(null);

type State =
  | { kind: "confirm"; opts: ConfirmOptions }
  | { kind: "prompt"; opts: PromptOptions; value: string }
  | { kind: "choose"; opts: ChooseOptions }
  | null;

export function ConfirmProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<State>(null);
  // Resolver for the pending promise; called on confirm/cancel/close.
  const resolveRef = useRef<((v: boolean | string | null) => void) | null>(null);

  const settle = useCallback((v: boolean | string | null) => {
    resolveRef.current?.(v);
    resolveRef.current = null;
    setState(null);
  }, []);

  const confirm = useCallback((opts: string | ConfirmOptions) =>
    new Promise<boolean>((resolve) => {
      resolveRef.current = (v) => resolve(v === true);
      setState({ kind: "confirm", opts: typeof opts === "string" ? { message: opts } : opts });
    }), []);

  const prompt = useCallback((opts: string | PromptOptions) =>
    new Promise<string | null>((resolve) => {
      resolveRef.current = (v) => resolve(typeof v === "string" ? v : null);
      const o = typeof opts === "string" ? { label: opts } : opts;
      setState({ kind: "prompt", opts: o, value: o.defaultValue ?? "" });
    }), []);

  const choose = useCallback((opts: ChooseOptions) =>
    new Promise<string | null>((resolve) => {
      resolveRef.current = (v) => resolve(typeof v === "string" ? v : null);
      setState({ kind: "choose", opts });
    }), []);

  const onConfirm = () => {
    if (state?.kind === "prompt") settle(state.value.trim() === "" ? null : state.value);
    else settle(true);
  };
  const onCancel = () => settle(state?.kind === "prompt" || state?.kind === "choose" ? null : false);

  const o = state?.opts;

  return (
    <Ctx.Provider value={{ confirm, prompt, choose }}>
      {children}
      <Dialog open={state !== null} onOpenChange={(open) => { if (!open) onCancel(); }}>
        {state && (
          <DialogContent className="max-w-md">
            <DialogHeader className="text-start">
              <DialogTitle>
                {o?.title ?? (state.kind === "prompt" ? "إدخال" : state.kind === "choose" ? "اختيار" : "تأكيد")}
              </DialogTitle>
              {o?.message != null && (
                <DialogDescription className="text-start">{o.message}</DialogDescription>
              )}
            </DialogHeader>

            {state.kind === "prompt" && (
              <div className="flex flex-col gap-1.5">
                {state.opts.label && (
                  <label className="text-sm text-muted-foreground">{state.opts.label}</label>
                )}
                <Input
                  autoFocus
                  dir={state.opts.ltr ? "ltr" : undefined}
                  value={state.value}
                  placeholder={state.opts.placeholder}
                  onChange={(e) => setState((s) => (s && s.kind === "prompt" ? { ...s, value: e.target.value } : s))}
                  onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); onConfirm(); } }}
                />
              </div>
            )}

            {state.kind === "choose" && (
              <div className="flex flex-col gap-2">
                {state.opts.options.map((opt) => (
                  <button
                    key={opt.key}
                    type="button"
                    onClick={() => settle(opt.key)}
                    className="flex w-full flex-col items-start gap-0.5 rounded-md border border-input px-3 py-2 text-start transition-colors hover:bg-accent hover:text-accent-foreground"
                  >
                    <span className="text-sm font-medium">{opt.label}</span>
                    {opt.description && (
                      <span className="text-xs text-muted-foreground">{opt.description}</span>
                    )}
                  </button>
                ))}
              </div>
            )}

            <div className="flex justify-end gap-2 pt-2">
              <Button variant="outline" size="sm" onClick={onCancel}>
                {o?.cancelText ?? "إلغاء"}
              </Button>
              {/* A choice dialog commits on the option itself — no separate confirm button. */}
              {state.kind !== "choose" && (
                <Button
                  size="sm"
                  variant={state.kind === "confirm" && (state.opts as ConfirmOptions).destructive ? "destructive" : "default"}
                  onClick={onConfirm}
                >
                  {(o as ConfirmOptions | PromptOptions)?.confirmText ?? (state.kind === "prompt" ? "حفظ" : "تأكيد")}
                </Button>
              )}
            </div>
          </DialogContent>
        )}
      </Dialog>
    </Ctx.Provider>
  );
}

export function useConfirmDialog(): ConfirmApi {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error("useConfirmDialog must be used within ConfirmProvider");
  return ctx;
}
