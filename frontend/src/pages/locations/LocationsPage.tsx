import { useCallback, useEffect, useMemo, useState } from "react";
import { motion } from "motion/react";
import { AxiosError } from "axios";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Building2, DoorClosed, Archive as CabinetIcon, Layers, Package, Plus, Pencil, Trash2, Tag, RefreshCw, ExternalLink, Eye, EyeOff, Search, X } from "lucide-react";
import { auth } from "../../lib/auth";
import { locations, type TreeNode as LibNode, type Box } from "../../lib/locations";
import { documents } from "../../lib/documents";
import { useDateFormat } from "../../lib/useCalendar";
import { useToast } from "../../components/toast";
import BoxLabel from "../../components/BoxLabel";
import { AppTreeView, createTreeCollection, type TreeNode as UiNode } from "../../components/ui/tree-view";
import { ContextMenuItem, ContextMenuSeparator, ContextMenuLabel } from "../../components/ui/context-menu";
import { useConfirmDialog } from "../../components/ui/confirm-dialog";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "../../components/ui/dialog";
import { LabeledInput, LabeledCombobox } from "../../components/ui/labeled-input";
import { FilterSearchRow, FilterSidebar } from "../../components/ui/filter-sidebar";
import "../../styles/forms.css";
import "./locations.css";

const errOf = (err: unknown, fallback: string) =>
  (err as AxiosError<{ error?: string }>).response?.data?.error ?? fallback;

type NodeType = "Building" | "Room" | "Cabinet" | "Shelf" | "Box";

const META: Record<NodeType, { label: string; child: NodeType | null; childLabel: string }> = {
  Building: { label: "مبنى", child: "Room", childLabel: "غرفة" },
  Room: { label: "غرفة", child: "Cabinet", childLabel: "خزانة" },
  Cabinet: { label: "خزانة", child: "Shelf", childLabel: "رف" },
  Shelf: { label: "رف", child: "Box", childLabel: "صندوق" },
  Box: { label: "صندوق", child: null, childLabel: "" },
};

const ICON: Record<NodeType, typeof Building2> = {
  Building: Building2, Room: DoorClosed, Cabinet: CabinetIcon, Shelf: Layers, Box: Package,
};

const nid = (type: string, id: number) => `${type}:${id}`;
const parseNid = (s: string) => { const [t, i] = s.split(":"); return { type: t as NodeType, id: Number(i) }; };

// Prune the tree to nodes that match the search text and/or a chosen level, keeping any
// ancestor whose descendant matches so the path to a hit stays visible. An empty query and
// no type returns the full tree unchanged.
function pruneTree(nodes: LibNode[], query: string, type: NodeType | ""): LibNode[] {
  const q = query.trim().toLowerCase();
  if (!q && !type) return nodes;
  const selfMatch = (n: LibNode) =>
    (!type || n.type === type) &&
    (!q || n.name.toLowerCase().includes(q) || (n.code ?? "").toLowerCase().includes(q));
  const walk = (list: LibNode[]): LibNode[] => {
    const out: LibNode[] = [];
    for (const n of list) {
      const kids = walk(n.children);
      if (selfMatch(n) || kids.length) out.push({ ...n, children: kids });
    }
    return out;
  };
  return walk(nodes);
}

interface EditorForm {
  name: string; code: string; capacity: string;
  /** What the user typed or picked for the parent. Resolved to an id on save; a name
   *  matching nothing creates the place (see resolveParentId). */
  parentText: string;
}
interface EditorState {
  mode: "add" | "edit"; type: NodeType; id?: number;
  parentType?: NodeType; parentId?: number; form: EditorForm; loading: boolean;
}

export default function LocationsPage() {
  const toast = useToast();
  const { confirm } = useConfirmDialog();
  const canCreate = auth.hasPermission("Archive.Create");
  const canEdit = auth.hasPermission("Archive.Edit");
  const canDelete = auth.hasPermission("Archive.Delete");

  const [libTree, setLibTree] = useState<LibNode[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [expandedIds, setExpandedIds] = useState<string[]>([]);
  // The place whose contents are listed underneath the tree. Any level, not just a box: standing
  // on a room and being told nothing is in it is the question an archivist actually asks.
  const [placeSel, setPlaceSel] = useState<{ kind: NodeType; id: number; name: string } | null>(null);
  // Off to begin with: this page is primarily where the tree is built and rearranged, and a long
  // document list under every click pushed that work off the screen. «عرض الوثائق» on a node — or
  // the switch in the panel header — asks for it.
  const [showDocs, setShowDocs] = useState(false);
  const [editor, setEditor] = useState<EditorState | null>(null);
  const [labelBox, setLabelBox] = useState<Box | null>(null);
  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState<NodeType | "">("");
  const [showFilters, setShowFilters] = useState(false);
  const filtering = !!search.trim() || typeFilter !== "";

  const loadTree = useCallback(async () => {
    setLoading(true);
    try { setLibTree(await locations.tree()); }
    catch { toast.error("تعذّر تحميل شجرة المواقع"); }
    finally { setLoading(false); }
  }, [toast]);
  useEffect(() => { loadTree(); }, [loadTree]);

  // The tree actually shown — pruned by the search box / level chips.
  const treeForView = useMemo(() => pruneTree(libTree, search, typeFilter), [libTree, search, typeFilter]);

  // Build the Ark collection + a child→parent map. Containers keep a `children`
  // array (so they render as folders even when empty); boxes are leaves.
  const { collection, parentOf } = useMemo(() => {
    const parent: Record<string, { type: NodeType; id: number }> = {};
    const convert = (n: LibNode, p?: { type: NodeType; id: number }): UiNode => {
      const id = nid(n.type, n.id);
      if (p) parent[id] = p;
      const self = { type: n.type as NodeType, id: n.id };
      const isContainer = n.type !== "Box";
      return {
        id, name: n.name, code: n.code, kind: n.type,
        ...(isContainer ? { children: n.children.map((c) => convert(c, self)) } : {}),
      };
    };
    const roots = treeForView.map((n) => convert(n));
    const collection = createTreeCollection<UiNode>({
      nodeToValue: (node) => node.id,
      nodeToString: (node) => node.name,
      rootNode: { id: "ROOT", name: "", children: roots },
    });
    return { collection, parentOf: parent };
  }, [treeForView]);

  // Every container currently in view — expanded automatically while filtering so hits show.
  const viewContainers = useMemo(() => {
    const out: string[] = [];
    const walk = (nodes: LibNode[]) => {
      for (const n of nodes) { if (n.type !== "Box") out.push(nid(n.type, n.id)); walk(n.children); }
    };
    walk(treeForView);
    return out;
  }, [treeForView]);

  // Valid move targets: all nodes of the parent level, with a readable path.
  const byType = useMemo(() => {
    const map: Record<string, { id: number; label: string }[]> = {};
    const walk = (nodes: LibNode[], trail: string) => {
      for (const n of nodes) {
        (map[n.type] ??= []).push({ id: n.id, label: `${trail}${n.name}` });
        walk(n.children, `${trail}${n.name} › `);
      }
    };
    walk(libTree, "");
    return map;
  }, [libTree]);

  // Flat list of container nodes with their depth (Building=1 … Shelf=4), used
  // to expand/collapse the whole tree to a chosen level.
  const containers = useMemo(() => {
    const out: { id: string; depth: number }[] = [];
    const walk = (nodes: LibNode[], depth: number) => {
      for (const n of nodes) {
        if (n.type !== "Box") out.push({ id: nid(n.type, n.id), depth });
        walk(n.children, depth + 1);
      }
    };
    walk(libTree, 1);
    return out;
  }, [libTree]);

  // Show every node up to `level` by expanding all shallower containers.
  const expandToLevel = (level: number) =>
    setExpandedIds(containers.filter((c) => c.depth < level).map((c) => c.id));

  const selectedNode = selectedIds[0]
    ? (collection.findNode(selectedIds[0]) as UiNode | null)
    : null;

  /* ── Editor ── */
  function openAdd(type: NodeType, parentType?: NodeType, parentId?: number) {
    // Expand the parent so the new child is visible once created.
    if (parentType && parentId != null) setExpandedIds((s) => s.includes(nid(parentType, parentId)) ? s : [...s, nid(parentType, parentId)]);
    const preset = parentId != null
      ? (byType[parentTypeOf(type)] ?? []).find((o) => o.id === parentId)
      : undefined;
    setEditor({ mode: "add", type, parentType, parentId, loading: false,
      form: { name: "", code: "", capacity: "", parentText: preset?.label ?? "" } });
  }

  async function openEdit(uiNode: UiNode) {
    const { type, id } = parseNid(uiNode.id);
    const p = parentOf[uiNode.id];
    const current = p ? (byType[p.type] ?? []).find((o) => o.id === p.id) : undefined;
    setEditor({ mode: "edit", type, id, parentType: p?.type, parentId: p?.id, loading: true,
      form: { name: uiNode.name, code: uiNode.code ?? "", capacity: "", parentText: current?.label ?? "" } });
    try {
      let cap = "";
      if (type === "Shelf" && p) cap = String((await locations.shelves(p.id)).find((x) => x.id === id)?.capacity ?? "");
      else if (type === "Box" && p) cap = String((await locations.boxes({ shelfId: p.id })).find((x) => x.id === id)?.capacity ?? "");
      setEditor((e) => e && { ...e, loading: false, form: { ...e.form, capacity: cap === "null" ? "" : cap } });
    } catch { setEditor((e) => e && { ...e, loading: false }); }
  }

  /** Turn the parent combobox text into an id. A picked suggestion or a typed name that matches
   *  an existing place resolves to it; an unrecognised name creates the place — but only where
   *  that can be done without inventing its own ancestors, i.e. buildings. Returns `undefined`
   *  to abort the save (the user has to fix the input first). */
  async function resolveParentId(type: NodeType, f: EditorForm): Promise<number | null | undefined> {
    const parentType = parentTypeOf(type);
    const typed = f.parentText.trim();
    if (!typed) { toast.error(`${META[parentType].label} مطلوب`); return undefined; }

    // Match the full path first, then case-insensitively, then just the leaf segment — the
    // suggestions read "مبنى › غرفة ٣" but a user typing "غرفة ٣" clearly means that room.
    const leafOf = (s: string) => s.split("›").pop()!.trim().toLocaleLowerCase();
    const t = typed.toLocaleLowerCase();
    const opts = byType[parentType] ?? [];
    const match = opts.find((o) => o.label === typed)
      ?? opts.find((o) => o.label.toLocaleLowerCase() === t)
      ?? opts.find((o) => leafOf(o.label) === t);
    if (match) return match.id;

    if (parentType === "Building") return (await locations.createBuilding({ nameAr: typed, code: null })).id;

    // A new room/cabinet/shelf would need its own parent picked too, which this dialog has no
    // field for — better to say so than to guess an ancestor.
    toast.error(`لا يوجد ${META[parentType].label} بهذا الاسم — أنشئه أولًا ثم اختره`);
    return undefined;
  }

  async function saveEditor() {
    if (!editor) return;
    const f = editor.form;
    if (!f.name.trim()) { toast.error("الاسم مطلوب"); return; }
    const cap = f.capacity.trim() ? Number(f.capacity) : null;

    // Resolve the parent from whatever is in the combobox: a picked suggestion, a typed
    // name matching an existing place, or a brand-new name. Returns undefined to abort.
    let pid: number | null;
    if (editor.type === "Building") pid = null;
    else {
      const resolved = await resolveParentId(editor.type, f);
      if (resolved === undefined) return;
      pid = resolved;
    }
    const body: Record<string, unknown> = (() => {
      switch (editor.type) {
        case "Building": return { nameAr: f.name.trim(), code: f.code.trim() || null };
        case "Room": return { nameAr: f.name.trim(), roomNumber: f.code.trim() || null, buildingId: pid };
        case "Cabinet": return { nameAr: f.name.trim(), cabinetCode: f.code.trim() || null, roomId: pid };
        case "Shelf": return { shelfNumber: f.name.trim(), capacity: cap, cabinetId: pid };
        case "Box": return { boxCode: f.name.trim(), capacity: cap, shelfId: pid };
      }
    })();

    setEditor((e) => e && { ...e, loading: true });
    try {
      const create = { Building: locations.createBuilding, Room: locations.createRoom, Cabinet: locations.createCabinet, Shelf: locations.createShelf, Box: locations.createBox } as const;
      const update = { Building: locations.updateBuilding, Room: locations.updateRoom, Cabinet: locations.updateCabinet, Shelf: locations.updateShelf, Box: locations.updateBox } as const;
      if (editor.mode === "add") await create[editor.type](body);
      else if (editor.id != null) await update[editor.type](editor.id, body);
      setEditor(null);
      toast.success("تم الحفظ");
      await loadTree();
    } catch (err) {
      toast.error(errOf(err, "تعذّر الحفظ"));
      setEditor((e) => e && { ...e, loading: false });
    }
  }

  async function remove(uiNode: UiNode) {
    const { type, id } = parseNid(uiNode.id);
    if (!(await confirm({ message: `حذف "${uiNode.name}"؟ سيُحذف كل ما بداخله.`, destructive: true }))) return;
    try {
      const del = { Building: locations.deleteBuilding, Room: locations.deleteRoom, Cabinet: locations.deleteCabinet, Shelf: locations.deleteShelf, Box: locations.deleteBox } as const;
      await del[type](id);
      if (placeSel?.kind === type && placeSel.id === id) setPlaceSel(null);
      toast.success("تم الحذف");
      await loadTree();
    } catch (err) { toast.error(errOf(err, "تعذّر الحذف — تأكد أنه فارغ")); }
  }

  /* ── Per-node context menu + row actions ── */
  const nodeMenu = (uiNode: UiNode) => {
    const { type } = parseNid(uiNode.id);
    const meta = META[type];
    return (
      <>
        <ContextMenuLabel className="truncate text-xs text-muted-foreground">{meta.label} · {uiNode.name}</ContextMenuLabel>
        <ContextMenuSeparator />
        {/* Selects the node and opens the list in one act — with the list hidden by default,
            this is the direct answer to "what is in here?". */}
        <ContextMenuItem onSelect={() => {
          setSelectedIds([uiNode.id]);
          setPlaceSel({ kind: type, id: parseNid(uiNode.id).id, name: uiNode.name });
          setShowDocs(true);
        }}>
          <Eye className="ms-2 h-4 w-4 opacity-60" /> عرض الوثائق
        </ContextMenuItem>
        {canCreate && meta.child && <ContextMenuSeparator />}
        {canCreate && meta.child && (
          <ContextMenuItem onSelect={() => openAdd(meta.child!, type, parseNid(uiNode.id).id)}>
            <Plus className="ms-2 h-4 w-4 opacity-60" /> إضافة {meta.childLabel}
          </ContextMenuItem>
        )}
        {type === "Box" && (
          <ContextMenuItem onSelect={() => setLabelBox({ id: parseNid(uiNode.id).id, boxCode: uiNode.name, barcode: uiNode.code } as Box)}>
            <Tag className="ms-2 h-4 w-4 opacity-60" /> بطاقة الصندوق
          </ContextMenuItem>
        )}
        {(canEdit || canDelete) && <ContextMenuSeparator />}
        {canEdit && (
          <ContextMenuItem onSelect={() => openEdit(uiNode)}>
            <Pencil className="ms-2 h-4 w-4 opacity-60" /> تعديل
          </ContextMenuItem>
        )}
        {canDelete && (
          <ContextMenuItem className="text-destructive focus:text-destructive" onSelect={() => remove(uiNode)}>
            <Trash2 className="ms-2 h-4 w-4 opacity-60" /> حذف
          </ContextMenuItem>
        )}
      </>
    );
  };

  const rowEnd = (uiNode: UiNode) => {
    const { type } = parseNid(uiNode.id);
    const meta = META[type];
    if (!canCreate || !meta.child) return null;
    return (
      <button type="button" className="tree-actbtn" title={`إضافة ${meta.childLabel}`}
        onClick={(e) => { e.stopPropagation(); openAdd(meta.child!, type, parseNid(uiNode.id).id); }}>
        <Plus className="h-3.5 w-3.5" />
      </button>
    );
  };

  return (
    <div>
      {/* Chart-of-accounts style toolbar: quick-add every level, act on the
          selection, and expand/collapse the tree to a chosen depth. */}
      <div className="tree-toolbar" role="toolbar" aria-label="أدوات الشجرة">
        {canCreate && (
          <div className="tree-toolbar__grp">
            {(["Building", "Room", "Cabinet", "Shelf", "Box"] as NodeType[]).map((type) => {
              const Ico = ICON[type];
              return (
                <button key={type} type="button" className="tree-toolbar__btn tree-toolbar__btn--add"
                  onClick={() => openAdd(type)} title={`إضافة ${META[type].label}`}>
                  <Ico className="h-4 w-4" /><Plus className="h-3 w-3 tree-toolbar__plus" />
                  <span>{META[type].label}</span>
                </button>
              );
            })}
          </div>
        )}

        {(canEdit || canDelete) && (
          <div className="tree-toolbar__grp">
            {canEdit && (
              <button type="button" className="tree-toolbar__btn" disabled={!selectedNode}
                onClick={() => selectedNode && openEdit(selectedNode)} title="تعديل المحدد">
                <Pencil className="h-4 w-4" /><span>تعديل</span>
              </button>
            )}
            {canDelete && (
              <button type="button" className="tree-toolbar__btn tree-toolbar__btn--danger" disabled={!selectedNode}
                onClick={() => selectedNode && remove(selectedNode)} title="حذف المحدد">
                <Trash2 className="h-4 w-4" /><span>حذف</span>
              </button>
            )}
          </div>
        )}

        <div className="tree-toolbar__grp tree-toolbar__grp--levels">
          <span className="tree-toolbar__lbl">المستوى</span>
          {[1, 2, 3, 4, 5].map((lvl) => (
            <button key={lvl} type="button" className="tree-toolbar__lvl"
              onClick={() => expandToLevel(lvl)} title={`عرض حتى المستوى ${lvl}`}>{lvl}</button>
          ))}
          <button type="button" className="tree-toolbar__btn" onClick={() => setExpandedIds(containers.map((c) => c.id))} title="توسيع الكل">توسيع</button>
          <button type="button" className="tree-toolbar__btn" onClick={() => setExpandedIds([])} title="طي الكل">طي</button>
        </div>

        <button type="button" className="tree-toolbar__btn tree-toolbar__refresh" onClick={loadTree} title="تحديث"><RefreshCw size={14} aria-hidden /></button>
      </div>

      <div className="mt-2">
        <FilterSearchRow
          search={search}
          onSearch={setSearch}
          placeholder="ابحث في المواقع بالاسم أو الرمز…"
          showFilters={showFilters}
          onToggleFilters={() => setShowFilters((v) => !v)}
          activeCount={typeFilter ? 1 : 0}
        />
      </div>

      <div className="mt-3 flex items-start gap-4">
        <FilterSidebar
          open={showFilters}
          panelLabel="المرشحات"
          clearLabel="مسح"
          hasActive={typeFilter !== ""}
          onClear={() => setTypeFilter("")}
          groups={[{
            key: "type",
            label: "المستوى",
            selected: typeFilter,
            onSelect: (v) => setTypeFilter(v as NodeType | ""),
            options: (["Building", "Room", "Cabinet", "Shelf", "Box"] as NodeType[]).map((tp) => ({ value: tp, label: META[tp].label })),
          }]}
        />

        <section className="doc-card tree-card min-w-0 flex-1">
        {loading ? (
          <p className="muted">جارِ التحميل…</p>
        ) : libTree.length === 0 ? (
          <div className="tree__empty">
            <p className="muted">لا توجد مواقع بعد.</p>
            {canCreate && <button className="btn btn-primary btn-sm" onClick={() => openAdd("Building")}><Plus className="h-4 w-4" /> إضافة أول مبنى</button>}
          </div>
        ) : treeForView.length === 0 ? (
          <div className="tree__empty"><p className="muted">لا نتائج مطابقة.</p></div>
        ) : (
          <AppTreeView
            collection={collection}
            selectedIds={selectedIds}
            expandedIds={filtering ? viewContainers : expandedIds}
            onExpandedChange={filtering ? () => {} : setExpandedIds}
            onSelect={(id, node) => {
              const { type, id: dbId } = parseNid(id);
              setSelectedIds([id]);
              setPlaceSel({ kind: type, id: dbId, name: node.name });
            }}
            icon={(node, open) => {
              const { type } = parseNid(node.id);
              const Ico = ICON[type];
              return <Ico className={`h-4 w-4 ${type === "Box" ? "text-blue-500" : open ? "text-blue-600" : "text-blue-500"}`} />;
            }}
            rowEnd={rowEnd}
            nodeMenu={nodeMenu}
          />
        )}
        </section>
      </div>

      {placeSel && (
        <PlaceContents
          kind={placeSel.kind}
          id={placeSel.id}
          // The full path when we have it, so "رف ٢" says which cabinet in which room it means.
          label={(byType[placeSel.kind] ?? []).find((o) => o.id === placeSel.id)?.label ?? placeSel.name}
          // Held here, not inside the panel: someone reorganising the tree wants the list out of
          // the way for the whole session, not to re-collapse it at every place they click.
          show={showDocs}
          onToggle={() => setShowDocs((v) => !v)}
        />
      )}

      {labelBox && <BoxLabel box={labelBox} onClose={() => setLabelBox(null)} />}

      {editor && (
        <EditorDialog
          editor={editor}
          parentOptions={editor.type !== "Building" ? byType[parentTypeOf(editor.type)] ?? [] : []}
          onChange={(form) => setEditor((e) => e && { ...e, form })}
          onClose={() => setEditor(null)}
          onSave={saveEditor}
        />
      )}
    </div>
  );
}

function parentTypeOf(t: NodeType): NodeType {
  return t === "Room" ? "Building" : t === "Cabinet" ? "Room" : t === "Shelf" ? "Cabinet" : "Shelf";
}

/* ── Compact add/edit dialog ── */
function EditorDialog({
  editor, parentOptions, onChange, onClose, onSave,
}: {
  editor: EditorState;
  parentOptions: { id: number; label: string }[];
  onChange: (f: EditorForm) => void;
  onClose: () => void;
  onSave: () => void;
}) {
  const meta = META[editor.type];
  const f = editor.form;
  const set = (k: keyof EditorForm, v: string | number) => onChange({ ...f, [k]: v });
  const nameLabel = editor.type === "Shelf" ? "رقم الرف" : editor.type === "Box" ? "رمز الصندوق" : "الاسم";
  const codeLabel = editor.type === "Room" ? "رقم الغرفة" : "الرمز";
  const showCode = editor.type === "Building" || editor.type === "Room" || editor.type === "Cabinet";
  const showCapacity = editor.type === "Shelf" || editor.type === "Box";
  const showParent = editor.type !== "Building";
  const ltr = editor.type === "Shelf" || editor.type === "Box";

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="loc-editor">
        <DialogHeader><DialogTitle>{editor.mode === "add" ? "إضافة" : "تعديل"} {meta.label}</DialogTitle></DialogHeader>
        <div className="loc-editor__body">
          <LabeledInput label={nameLabel} required value={f.name} autoFocus dir={ltr ? "ltr" : undefined}
            onChange={(e) => set("name", e.target.value)} onKeyDown={(e) => { if (e.key === "Enter") onSave(); }} />
          {showCode && (
            <LabeledInput label={codeLabel} dir="ltr" value={f.code} onChange={(e) => set("code", e.target.value)} />
          )}
          {showCapacity && (
            <LabeledInput label="السعة" type="number" min={0} dir="ltr" value={f.capacity}
              onChange={(e) => set("capacity", e.target.value)} />
          )}
          {showParent && (
            // Write the place or pick one. No blank "unidentified" option: every room/cabinet/
            // shelf/box belongs somewhere, and the empty entry only ever produced orphans.
            <LabeledCombobox
              label={META[parentTypeOf(editor.type)].label}
              required
              options={parentOptions.map((o) => o.label)}
              value={f.parentText}
              disabled={editor.loading}
              // Only buildings can be conjured from a name — anything deeper would need its own
              // parent picked too, so there is no "create" row to offer there.
              createLabel={parentTypeOf(editor.type) === "Building"
                ? (typed) => `إنشاء مبنى «${typed}»`
                : undefined}
              hint={parentTypeOf(editor.type) === "Building" ? "اكتب اسمًا جديدًا لإنشاء مبنى" : undefined}
              onValueChange={(v) => set("parentText", v)}
              onKeyDown={(e) => { if (e.key === "Enter") onSave(); }}
            />
          )}
        </div>
        <DialogFooter>
          <button className="btn btn-ghost btn-sm" onClick={onClose}>إلغاء</button>
          <button className="btn btn-primary btn-sm" disabled={editor.loading} onClick={onSave}>{editor.loading ? "…" : "حفظ"}</button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/* ── البنود المؤرشفة: the documents kept in the selected place, at whatever level it sits ── */
function PlaceContents({
  kind, id, label, show, onToggle,
}: { kind: NodeType; id: number; label: string; show: boolean; onToggle: () => void }) {
  const { date } = useDateFormat();
  const meta = META[kind];
  const [search, setSearch] = useState("");
  const [query, setQuery] = useState("");
  // Typed text settles before it becomes a query: the search runs against the full-text index
  // server-side, and one round trip per keystroke would be paid for on every letter.
  useEffect(() => {
    const h = setTimeout(() => setQuery(search.trim()), 300);
    return () => clearTimeout(h);
  }, [search]);
  // A place is a place: what you searched in the last one has nothing to do with this one.
  useEffect(() => { setSearch(""); setQuery(""); }, [kind, id]);

  const { data, isLoading } = useQuery({
    queryKey: ["place-documents", kind, id, query],
    // Everything filed underneath this place, not only what is pinned directly to it — the
    // backend resolves the place to its boxes. Searching stays scoped to the place, so a hit
    // is always something that is actually on these shelves.
    queryFn: () => documents.list({ locationKind: kind, locationId: id, search: query || undefined, pageSize: 100 }),
    staleTime: 0,
    refetchOnMount: "always",
    // Nothing to fetch while the list is put away — but the last result stays cached, so
    // opening it again is instant.
    enabled: show,
  });
  const rows = data?.items ?? [];
  // A box's own contents are all in that box; anywhere higher, which box matters.
  const showBox = kind !== "Box";
  const searching = query.length > 0;

  return (
    <motion.section className="doc-card box-contents" initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }}>
      <div className="box-contents__head">
        {/* The path already names the place ("المبنى الرئيسي › غرفة ١"); the level goes in the
            subtitle rather than in front of it, where it read as "مبنى المبنى الرئيسي". */}
        <h3 className="detail-h3"><Package size={15} aria-hidden /> البنود المؤرشفة — {label}</h3>
        <span className="muted mono">
          {meta.label}
          {/* With a search running the count is "how many matched", not "how many are here" —
              say which, or the number reads as the place having shrunk. */}
          {show && data ? ` · ${data.totalCount} ${searching ? "مطابق" : "بند"}` : ""}
        </span>
        <div className="box-contents__tools">
          {show && (
            <div className="box-contents__search">
              <Search size={14} aria-hidden />
              <input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder={`ابحث في وثائق هذا ${meta.label}…`}
                aria-label="بحث في وثائق الموقع"
              />
              {search && (
                <button type="button" className="tree-actbtn" title="مسح البحث" onClick={() => setSearch("")}>
                  <X className="h-3.5 w-3.5" />
                </button>
              )}
            </div>
          )}
          <button type="button" className="btn btn-ghost btn-sm" onClick={onToggle}
            aria-expanded={show} title={show ? "إخفاء البنود" : "إظهار البنود"}>
            {show ? <EyeOff size={14} aria-hidden /> : <Eye size={14} aria-hidden />}
            {show ? "إخفاء البنود" : "إظهار البنود"}
          </button>
        </div>
      </div>
      {!show ? null : isLoading ? (
        <p className="muted">جارِ التحميل…</p>
      ) : rows.length === 0 ? (
        <p className="muted">
          {searching ? `لا توجد وثائق مطابقة لـ «${query}» في هذا الموقع.` : "لا توجد وثائق مؤرشفة في هذا الموقع."}
        </p>
      ) : (
        <div className="box-contents__tablewrap">
          <table className="loc-table">
            <thead><tr>
              <th>الرقم</th><th>العنوان</th><th>النوع</th><th>التاريخ</th>
              {showBox && <th>الصندوق</th>}
              <th>رقم الملف</th><th></th>
            </tr></thead>
            <tbody>
              {rows.map((d) => (
                <tr key={d.id}>
                  <td className="mono">{d.documentNumber}</td>
                  <td>{d.title}</td>
                  <td>{d.documentTypeName}</td>
                  <td className="mono">{d.documentDate ? date(d.documentDate) : "—"}</td>
                  {showBox && <td className="mono">{d.boxCode ?? "—"}</td>}
                  <td className="mono">{d.fileNumber ?? "—"}</td>
                  <td><Link to={`/app/documents/${d.id}`} className="btn btn-ghost btn-sm">عرض <ExternalLink size={12} aria-hidden /></Link></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </motion.section>
  );
}
