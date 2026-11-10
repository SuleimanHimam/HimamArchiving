"use client";

import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { TreeView, createTreeCollection } from "@ark-ui/react/tree-view";
import { LocaleProvider } from "@ark-ui/react/locale";
import { ChevronRight } from "lucide-react";
import {
  ContextMenu,
  ContextMenuContent,
  ContextMenuTrigger,
} from "@/components/ui/context-menu";

/* A data-driven Ark UI tree. Generalised from the shadcn/Ark demo so real
   hierarchies can drive it: pass a `collection`, control selection, render a
   custom icon per node, and (optionally) a right-click context menu per node.

   A node is a *branch* (expandable folder) when it has a `children` array —
   even an empty one; give leaves no `children` property. */

export interface TreeNode {
  id: string;
  name: string;
  code?: string | null;
  /** Domain type carried through for icons/actions (e.g. Building, Box). */
  kind?: string;
  children?: TreeNode[];
}

export { createTreeCollection };

export interface AppTreeViewProps {
  collection: ReturnType<typeof createTreeCollection<TreeNode>>;
  label?: ReactNode;
  selectedIds?: string[];
  /** Controlled expansion — persists across collection rebuilds (after CRUD). */
  expandedIds?: string[];
  onExpandedChange?: (ids: string[]) => void;
  onSelect?: (id: string, node: TreeNode) => void;
  /** Leading icon for a node; `open` reflects branch expansion. */
  icon?: (node: TreeNode, open: boolean) => ReactNode;
  /** Trailing content on the row (e.g. hover action buttons). */
  rowEnd?: (node: TreeNode) => ReactNode;
  /** Always-visible trailing badge (e.g. a count). Unlike `rowEnd`, never hidden until hover. */
  badge?: (node: TreeNode) => ReactNode;
  /** Items for a right-click context menu on the node (shadcn ContextMenu). */
  nodeMenu?: (node: TreeNode) => ReactNode;
}

function Node({
  node,
  indexPath,
  ctx,
}: {
  node: TreeNode;
  indexPath: number[];
  ctx: Omit<AppTreeViewProps, "collection" | "label" | "selectedIds">;
}) {
  const isBranch = Array.isArray(node.children);

  const wrap = (row: ReactNode) =>
    ctx.nodeMenu ? (
      <ContextMenu>
        <ContextMenuTrigger asChild>{row}</ContextMenuTrigger>
        <ContextMenuContent className="w-56">{ctx.nodeMenu(node)}</ContextMenuContent>
      </ContextMenu>
    ) : (
      row
    );

  return (
    <TreeView.NodeProvider key={node.id} node={node} indexPath={indexPath}>
      {isBranch ? (
        <TreeView.Branch>
          {wrap(
            <TreeView.BranchControl className="tree-node group flex w-full items-center gap-2 rounded-lg px-2.5 py-1.5 text-sm transition-colors hover:bg-slate-100 dark:hover:bg-slate-800/60">
              <TreeView.BranchIndicator className="flex h-4 w-4 shrink-0 items-center justify-center">
                <ChevronRight className="h-3.5 w-3.5 text-slate-400 transition-transform duration-200 group-data-[state=open]:rotate-90 group-data-[state=open]:text-slate-600" />
              </TreeView.BranchIndicator>
              <TreeView.BranchText className="flex min-w-0 flex-1 items-center gap-2 text-slate-700 dark:text-slate-300">
                {ctx.icon?.(node, true)}
                <span className="truncate font-medium">{node.name}</span>
                {node.code && (
                  <span className="ms-1 rounded bg-slate-100 px-1.5 py-0.5 font-mono text-[0.7rem] text-slate-500 dark:bg-slate-800 dark:text-slate-400">
                    {node.code}
                  </span>
                )}
              </TreeView.BranchText>
              {ctx.badge && <span className="ms-auto flex items-center">{ctx.badge(node)}</span>}
              {ctx.rowEnd && (
                <span className={`${ctx.badge ? "" : "ms-auto "}tree-node__actions flex items-center gap-0.5 opacity-0 transition-opacity group-hover:opacity-100`}>
                  {ctx.rowEnd(node)}
                </span>
              )}
            </TreeView.BranchControl>,
          )}
          <TreeView.BranchContent className="ms-4 mt-1 space-y-1 border-s border-slate-200 ps-3 dark:border-slate-700/60">
            <TreeView.BranchIndentGuide />
            {node.children!.map((child, index) => (
              <Node key={child.id} node={child} indexPath={[...indexPath, index]} ctx={ctx} />
            ))}
          </TreeView.BranchContent>
        </TreeView.Branch>
      ) : (
        wrap(
          <TreeView.Item className="tree-node group flex w-full items-center gap-2 rounded-lg px-2.5 py-1.5 text-sm transition-colors hover:bg-slate-100 dark:hover:bg-slate-800/60 data-[selected]:bg-blue-50 dark:data-[selected]:bg-blue-900/30 data-[selected]:ring-1 data-[selected]:ring-blue-200 dark:data-[selected]:ring-blue-800/40">
            <span className="flex h-4 w-4 shrink-0 items-center justify-center">
              <span className="h-1.5 w-1.5 rounded-full bg-slate-300 dark:bg-slate-600 group-data-[selected]:bg-blue-500" />
            </span>
            <TreeView.ItemText className="flex min-w-0 flex-1 items-center gap-2 text-slate-600 dark:text-slate-400 group-data-[selected]:text-blue-700 dark:group-data-[selected]:text-blue-300">
              {ctx.icon?.(node, false)}
              <span className="truncate">{node.name}</span>
              {node.code && (
                <span className="ml-1 rounded bg-slate-100 px-1.5 py-0.5 font-mono text-[0.7rem] text-slate-500 dark:bg-slate-800 dark:text-slate-400">
                  {node.code}
                </span>
              )}
            </TreeView.ItemText>
            {ctx.badge && <span className="ms-auto flex items-center">{ctx.badge(node)}</span>}
            {ctx.rowEnd && (
              <span className={`${ctx.badge ? "" : "ms-auto "}tree-node__actions flex items-center gap-0.5 opacity-0 transition-opacity group-hover:opacity-100`}>
                {ctx.rowEnd(node)}
              </span>
            )}
          </TreeView.Item>,
        )
      )}
    </TreeView.NodeProvider>
  );
}

export function AppTreeView({
  collection,
  label,
  selectedIds,
  expandedIds,
  onExpandedChange,
  onSelect,
  icon,
  rowEnd,
  badge,
  nodeMenu,
}: AppTreeViewProps) {
  // Ark/Zag read direction from its LocaleContext, NOT the document — its
  // `dir` machine prop is filtered out by TreeView.Root, so passing `dir`
  // directly is ignored and every branch is forced to ltr (chevron on the
  // left). Feed the active language through LocaleProvider instead so the
  // chevrons/indentation flip to match RTL Arabic (and back for LTR English).
  const { i18n } = useTranslation();
  const locale = i18n.language === "ar" ? "ar" : "en-US";
  return (
    <LocaleProvider locale={locale}>
      <TreeView.Root
        collection={collection}
        selectedValue={selectedIds}
        expandedValue={expandedIds}
        onExpandedChange={(e) => onExpandedChange?.(e.expandedValue)}
        onSelectionChange={(e) => {
          const id = e.selectedValue[0];
          if (!id || !onSelect) return;
          const node = collection.findNode(id) as TreeNode | null;
          if (node) onSelect(id, node);
        }}
        className="w-full"
      >
        {label && (
          <TreeView.Label className="mb-3 text-sm font-semibold text-slate-900 dark:text-slate-100">
            {label}
          </TreeView.Label>
        )}
        <TreeView.Tree className="space-y-1">
          {collection.rootNode.children?.map((node, index) => (
            <Node key={node.id} node={node} indexPath={[index]} ctx={{ onSelect, icon, rowEnd, badge, nodeMenu }} />
          ))}
        </TreeView.Tree>
      </TreeView.Root>
    </LocaleProvider>
  );
}

export default AppTreeView;
