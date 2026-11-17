import { Children, Fragment, isValidElement, type ReactNode } from "react";

export interface SelectOption { value: string; label: string; disabled?: boolean }

/** Visible text of an <option>'s children. JSX like `{name}{suffix}` arrives as an array,
 *  so strings, numbers and nested fragments are concatenated; booleans/null are skipped. */
function textOf(node: ReactNode): string {
  if (typeof node === "string" || typeof node === "number") return String(node);
  if (Array.isArray(node)) return node.map(textOf).join("");
  if (isValidElement(node)) return textOf((node.props as { children?: ReactNode }).children);
  return "";
}

/** Flattens <option> children (including those inside <optgroup> and fragments). */
export function collectOptions(children: ReactNode): SelectOption[] {
  const out: SelectOption[] = [];
  const walk = (nodes: ReactNode) => {
    Children.forEach(nodes, (child) => {
      if (!isValidElement(child)) return;
      if (child.type === "option") {
        const p = child.props as { value?: string | number; children?: ReactNode; disabled?: boolean };
        const value = String(p.value ?? "");
        out.push({ value, label: textOf(p.children) || value, disabled: p.disabled });
      } else if (child.type === "optgroup" || child.type === Fragment) {
        walk((child.props as { children?: ReactNode }).children);
      }
    });
  };
  walk(children);
  return out;
}
