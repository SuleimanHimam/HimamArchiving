"use client";

import * as TabsPrimitive from "@radix-ui/react-tabs";
import * as React from "react";
import { motion } from "motion/react";

import { cn } from "@/lib/utils";

const Tabs = TabsPrimitive.Root;

/**
 * TabsList with an animated morphing-pill indicator (PillMorphTabs effect),
 * themed to Marasil (navy → brass-gold). The active pill is measured from the
 * live DOM so every existing <Tabs> usage gets it with no markup changes; it is
 * RTL-aware (works off getBoundingClientRect deltas) and re-measures on resize,
 * font load, and active-tab changes.
 */
const TabsList = React.forwardRef<
  React.ElementRef<typeof TabsPrimitive.List>,
  React.ComponentPropsWithoutRef<typeof TabsPrimitive.List>
>(({ className, children, ...props }, ref) => {
  const innerRef = React.useRef<HTMLDivElement | null>(null);
  const setRefs = React.useCallback(
    (node: HTMLDivElement | null) => {
      innerRef.current = node;
      if (typeof ref === "function") ref(node);
      else if (ref) (ref as React.MutableRefObject<HTMLDivElement | null>).current = node;
    },
    [ref],
  );

  const [pill, setPill] = React.useState<{ left: number; width: number } | null>(null);

  const measure = React.useCallback(() => {
    const list = innerRef.current;
    if (!list) return;
    const active = list.querySelector<HTMLElement>('[role="tab"][data-state="active"]');
    if (!active) {
      setPill(null);
      return;
    }
    const lr = list.getBoundingClientRect();
    const ar = active.getBoundingClientRect();
    setPill({ left: ar.left - lr.left + list.scrollLeft, width: ar.width });
  }, []);

  React.useLayoutEffect(() => {
    measure();
    const list = innerRef.current;
    if (!list) return;
    const ro = new ResizeObserver(measure);
    ro.observe(list);
    list.querySelectorAll('[role="tab"]').forEach((el) => ro.observe(el));
    const mo = new MutationObserver(measure);
    mo.observe(list, { attributes: true, subtree: true, attributeFilter: ["data-state"] });
    window.addEventListener("resize", measure);
    const t = window.setTimeout(measure, 60); // after fonts/layout settle
    return () => {
      ro.disconnect();
      mo.disconnect();
      window.removeEventListener("resize", measure);
      window.clearTimeout(t);
    };
  }, [measure, children]);

  return (
    <TabsPrimitive.List
      ref={setRefs}
      className={cn(
        "relative inline-flex items-center justify-center gap-1.5 rounded-full p-1.5",
        "border border-[var(--line)] bg-[color-mix(in_srgb,var(--paper)_70%,transparent)] backdrop-blur-sm",
        className,
      )}
      {...props}
    >
      {pill && (
        <>
          {/* soft brass glow for depth */}
          <motion.span
            aria-hidden
            initial={false}
            animate={{ left: pill.left, width: pill.width }}
            transition={{ type: "spring", stiffness: 320, damping: 30 }}
            className="pointer-events-none absolute top-0 bottom-0 z-0 rounded-full blur-xl opacity-40"
            style={{ background: "linear-gradient(90deg, var(--brand-solid), var(--brass))" }}
          />
          {/* morphing pill */}
          <motion.span
            aria-hidden
            initial={false}
            animate={{ left: pill.left, width: pill.width }}
            transition={{ type: "spring", stiffness: 320, damping: 30 }}
            className="pointer-events-none absolute top-1.5 bottom-1.5 z-0 rounded-full"
            style={{
              background: "linear-gradient(90deg, var(--brand-solid), var(--brass))",
              boxShadow: "0 6px 18px rgba(27,36,64,0.20)",
              border: "1px solid color-mix(in srgb, var(--brass) 35%, transparent)",
            }}
          />
        </>
      )}
      {children}
    </TabsPrimitive.List>
  );
});
TabsList.displayName = TabsPrimitive.List.displayName;

const TabsTrigger = React.forwardRef<
  React.ElementRef<typeof TabsPrimitive.Trigger>,
  React.ComponentPropsWithoutRef<typeof TabsPrimitive.Trigger>
>(({ className, ...props }, ref) => (
  <TabsPrimitive.Trigger
    ref={ref}
    className={cn(
      "relative z-10 inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-full border-0 bg-transparent shadow-none px-5 py-2.5 text-[15px] font-semibold [&_svg]:size-5 outline-offset-2 transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-ring/70 disabled:pointer-events-none disabled:opacity-50 text-[var(--text-muted)] hover:text-[var(--text)] data-[state=active]:text-white",
      className,
    )}
    {...props}
  />
));
TabsTrigger.displayName = TabsPrimitive.Trigger.displayName;

const TabsContent = React.forwardRef<
  React.ElementRef<typeof TabsPrimitive.Content>,
  React.ComponentPropsWithoutRef<typeof TabsPrimitive.Content>
>(({ className, ...props }, ref) => (
  <TabsPrimitive.Content
    ref={ref}
    className={cn(
      "mt-2 outline-offset-2 focus-visible:outline focus-visible:outline-2 focus-visible:outline-ring/70",
      className,
    )}
    {...props}
  />
));
TabsContent.displayName = TabsPrimitive.Content.displayName;

export { Tabs, TabsContent, TabsList, TabsTrigger };
