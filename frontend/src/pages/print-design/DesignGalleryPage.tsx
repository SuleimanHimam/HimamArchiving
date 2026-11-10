import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { Pencil, ExternalLink, Copy, Eye, Power, Trash2, Download, Upload, Landmark, User } from 'lucide-react'
import { auth } from '../../lib/auth'
import { organization } from '../../lib/organization'
import {
  downloadDesignFile, parseDesignFile, planImport, safeFilename, DesignFileError,
} from '../../lib/designExchange'
import {
  reportTemplates, renderLayout, SCOPE_LABEL,
  type ReportTemplate, type TemplateScope, type TemplateTarget,
} from '../../lib/reportTemplates'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { Tabs, TabsList, TabsTrigger } from '../../components/ui/tabs'
import {
  ContextMenu, ContextMenuContent, ContextMenuItem, ContextMenuLabel,
  ContextMenuSeparator, ContextMenuTrigger,
} from '../../components/ui/context-menu'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '../../components/ui/dialog'
import './design-gallery.css'

/**
 * The design picker shown before the canvas.
 *
 * Every card renders the design itself, scaled down — not an icon and not a name in a dropdown,
 * which is what this replaced. The thumbnail runs the same `renderLayout` the print path runs, so
 * what a card shows is what that design prints; a mock-up could drift from it silently.
 */

/**
 * Which kind of print job a tab covers.
 *
 * Documents/reports and internal mail are separate galleries because a design belongs to one or
 * the other; a design marked `All` applies to both and appears in both.
 */
type Kind = 'doc' | 'mail'
type GalleryKind = Kind
const GALLERY_TARGET: Record<GalleryKind, string> = { doc: 'Document', mail: 'Correspondence' }
/** The target a design created from this tab starts on (matches `TemplateTarget`). */
const KIND_NEW_TARGET: Record<GalleryKind, number> = { doc: 1, mail: 0 }

const ALL = '__all__'

export default function DesignGalleryPage() {
  const navigate = useNavigate()
  const toast = useToast()
  const { confirm } = useConfirmDialog()

  const canGallery = auth.hasPermission('PrintDesign.View')

  const [templates, setTemplates] = useState<ReportTemplate[] | null>(null)
  const [error, setError] = useState('')
  // The tab lives in the URL so a bookmark reopens the gallery where it was left.
  const [params, setParams] = useSearchParams()
  const kind: GalleryKind = ((): GalleryKind => {
    const t = params.get('tab')
    if ((t === 'doc' || t === 'mail') && canGallery) return t
    return 'doc'
  })()
  const setKind = (k: Kind) => setParams({ tab: k }, { replace: true })
  const [dept, setDept] = useState<string>(ALL)
  const [category, setCategory] = useState<string>(ALL)
  const [preview, setPreview] = useState<ReportTemplate | null>(null)
  const [importing, setImporting] = useState(false)
  const [importNotes, setImportNotes] = useState<string[]>([])
  const fileRef = useRef<HTMLInputElement>(null)

  /* canEdit/canDelete govern the designs that belong to the institution or a department.
     Everyone, permission or not, owns one personal design — their own stationery — and may
     create, edit and delete it; the API applies the same rule (ReportTemplatesController
     .CanManageAsync). Without this an employee could see a gallery they could do nothing in. */
  const canEdit = auth.hasPermission('PrintDesign.Edit') || auth.hasPermission('Organization.Edit')
  const canDelete = auth.hasPermission('PrintDesign.Delete') || auth.hasPermission('Organization.Edit')
  const myUserId = auth.getUser()?.id ?? null
  const isMine = (t: ReportTemplate) => t.scope === 'User' && t.userId === myUserId
  /* The permission says whether this caller edits designs at all; `canEdit` on the row says
     whether they may edit *this* one — the institution defaults are visible to everyone because
     everyone prints under them, but they are the administrator's to change. Both have to hold,
     and the server applies the same pair. */
  const mayEdit = (t: ReportTemplate) => t.canEdit && (canEdit || isMine(t))
  const mayDelete = (t: ReportTemplate) => t.canEdit && (canDelete || isMine(t))

  useEffect(() => { if (canGallery) void load() }, [canGallery])

  async function load() {
    try { setTemplates(await reportTemplates.list()) }
    catch { setError('تعذّر تحميل التصاميم') }
  }

  /* Outgoing Diwan letters moved to the slot-based letterhead, but internal mail still prints
     through this designer — so correspondence designs get their own tab rather than being hidden. */
  const inKind = useMemo(
    () => (templates ?? []).filter((t) => t.target === GALLERY_TARGET[kind] || t.target === 'All'),
    [templates, kind],
  )

  // Filter options come from the designs themselves: an option that matches nothing would only
  // offer the user an empty grid.
  const departments = useMemo(() => {
    const seen = new Map<string, string>()
    inKind.forEach((t) => {
      if (t.scope === 'OrgUnit' && t.orgUnitId != null) seen.set(String(t.orgUnitId), t.orgUnitName ?? `#${t.orgUnitId}`)
    })
    return [...seen.entries()].sort((a, b) => a[1].localeCompare(b[1], 'ar'))
  }, [inKind])

  const categories = useMemo(() => {
    const seen = new Set<string>()
    inKind.forEach((t) => {
      const label = kind === 'doc' ? t.documentTypeName : t.mailCategoryName
      if (label) seen.add(label)
    })
    return [...seen].sort((a, b) => a.localeCompare(b, 'ar'))
  }, [inKind, kind])

  const shown = useMemo(() => inKind.filter((t) => {
    if (dept === 'org' && t.scope !== 'Default') return false
    if (dept === 'mine' && t.scope !== 'User') return false
    if (dept !== ALL && dept !== 'org' && dept !== 'mine' && String(t.orgUnitId ?? '') !== dept) return false
    if (category !== ALL && (kind === 'doc' ? t.documentTypeName : t.mailCategoryName) !== category) return false
    return true
  }), [inKind, dept, category, kind])

  function openNew() {
    // Carry the tab and the department filter into the designer, so «تصميم جديد» while filtered to
    // a department starts scoped to it instead of dropping back to the institution default.
    const p = new URLSearchParams({ mode: 'new', target: String(KIND_NEW_TARGET[kind]) })
    if (!canEdit && myUserId != null) {
      // The only design this user may save is their own, so the designer opens on it rather
      // than on a scope the save would be refused at.
      p.set('scope', '2'); p.set('userId', String(myUserId))
    } else if (dept !== ALL && dept !== 'org' && dept !== 'mine') {
      p.set('scope', '1'); p.set('orgUnitId', dept)
    }
    navigate(`/app/print-design?${p}`)
  }

  const editUrl = (t: ReportTemplate) => `/app/print-design?templateId=${t.id}&mode=edit`

  async function duplicate(t: ReportTemplate) {
    try {
      // The copy starts disabled: two active designs at the same scope would race to win a
      // print job, and which one won would look arbitrary.
      await reportTemplates.create({ ...toSave(t), name: `${t.name} — نسخة`, isActive: false })
      await load(); toast.success('أُنشئت نسخة (معطّلة)')
    } catch { toast.error('تعذّر التكرار') }
  }

  async function toggleActive(t: ReportTemplate) {
    try {
      await reportTemplates.update(t.id, { ...toSave(t), isActive: !t.isActive })
      await load(); toast.success(t.isActive ? 'عُطّل التصميم' : 'فُعّل التصميم')
    } catch { toast.error('تعذّر التغيير') }
  }

  /** Import runs one design at a time so a bad row is reported by name and the rest still land. */
  async function importFile(file: File) {
    setImporting(true)
    try {
      const entries = parseDesignFile(await file.text())
      const units = await organization.orgUnits().catch(() => [])
      const ctx = {
        orgUnitsByName: new Map(units.map((u) => [u.name, u.id])),
        existingNames: new Set((templates ?? []).map((t) => t.name)),
        myUserId: auth.getUser()?.id ?? null,
      }
      const notes: string[] = []
      let ok = 0
      for (const entry of entries) {
        const plan = planImport(entry, ctx)
        try { await reportTemplates.create(plan.body); ok++; notes.push(...plan.notes) }
        catch { notes.push(`«${entry.name}»: تعذّر الحفظ`) }
      }
      await load()
      toast.success(`استُورد ${ok} من ${entries.length} — كلها معطّلة حتى تراجعها`)
      // Caveats stay on the page rather than in a toast: they are what the user must act on,
      // and a 4-second toast per design would scroll the important ones away.
      setImportNotes(notes)
    } catch (e) {
      toast.error(e instanceof DesignFileError ? e.message : 'تعذّر قراءة الملف')
    } finally { setImporting(false) }
  }

  async function remove(t: ReportTemplate) {
    if (!(await confirm({ message: `حذف التصميم «${t.name}»؟`, destructive: true }))) return
    try { await reportTemplates.remove(t.id); await load(); toast.success('حُذف التصميم') }
    catch { toast.error('تعذّر الحذف') }
  }

  return (
    <div className="dg">
      <header className="page__head">
        <div>
          <h1>تصاميم الطباعة</h1>
          <p className="muted dg__lede">
            اختر تصميمًا لتعديله أو ابدأ تصميمًا جديدًا. كل بطاقة تعرض التصميم نفسه مصغّرًا،
            لا اسمه فقط — وبزر الفأرة الأيمن تظهر بقية الإجراءات.
          </p>
        </div>
        <div className="dg__headactions">
          {canEdit && (
            <button className="btn btn-ghost" disabled={importing} onClick={() => fileRef.current?.click()}>
              <Upload className="h-4 w-4" />{importing ? 'جارٍ الاستيراد…' : 'استيراد'}
            </button>
          )}
          <button className="btn btn-ghost" disabled={shown.length === 0}
            onClick={() => downloadDesignFile(shown, `تصاميم-${kind === 'doc' ? 'الصادر-الخارجي' : 'البريد-الداخلي'}`)}>
            <Download className="h-4 w-4" />تصدير المعروض
          </button>
        </div>
        <input ref={fileRef} type="file" accept="application/json,.json" hidden
          onChange={(e) => { const f = e.target.files?.[0]; e.target.value = ''; if (f) void importFile(f) }} />
      </header>

      {error && <p className="cx-error">{error}</p>}

      {importNotes.length > 0 && (
        <div className="dg__notes">
          <div className="dg__noteshead">
            <strong>ملاحظات الاستيراد</strong>
            <button className="btn btn-ghost btn-sm" onClick={() => setImportNotes([])}>إخفاء</button>
          </div>
          <ul>{importNotes.map((n, i) => <li key={i}>{n}</li>)}</ul>
        </div>
      )}

      <div className="dg__bar">
        <Tabs value={kind} onValueChange={(v) => { setKind(v as Kind); setCategory(ALL) }}>
          <TabsList>
            <TabsTrigger value="doc">الصادر الخارجي</TabsTrigger>
            <TabsTrigger value="mail">البريد الداخلي</TabsTrigger>
          </TabsList>
        </Tabs>

        <div className="dg__filters">
          <label className="dg__filter">
            <span>القسم</span>
            <select value={dept} onChange={(e) => setDept(e.target.value)}>
              <option value={ALL}>كل الأقسام</option>
              <option value="org">عام (كل المؤسسة)</option>
              <option value="mine">تصاميم المستخدمين</option>
              {departments.map(([id, name]) => <option key={id} value={id}>{name}</option>)}
            </select>
          </label>

          <label className="dg__filter">
            <span>{kind === 'doc' ? 'نوع الوثيقة' : 'تصنيف البريد'}</span>
            <select value={category} onChange={(e) => setCategory(e.target.value)}
              disabled={categories.length === 0}>
              <option value={ALL}>الكل</option>
              {categories.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
          </label>
        </div>
      </div>

      <div className="dg__grid">
        {/* Start-new card first: the common action should not sit at the end of a long list. */}
        {(canEdit || myUserId != null) && (
          <button className="dg__card dg__card--new" onClick={openNew}>
            <span className="dg__plus" aria-hidden>＋</span>
            <span className="dg__newlabel">{canEdit ? 'تصميم جديد' : 'تصميمي الخاص'}</span>
            <span className="muted">{kind === 'doc' ? 'للصادر الخارجي' : 'للبريد الداخلي'}</span>
          </button>
        )}

        {templates === null && <p className="muted">جارٍ التحميل…</p>}
        {templates !== null && shown.length === 0 && (
          <p className="muted">لا توجد تصاميم مطابقة.</p>
        )}

        {shown.map((t) => (
          <ContextMenu key={t.id}>
            <ContextMenuTrigger asChild>
              <article className={`dg__card ${t.isActive ? '' : 'is-off'}`}>
                <button
                  className="dg__thumbwrap"
                  title={`فتح «${t.name}»`}
                  onClick={() => navigate(editUrl(t))}
                >
                  <DesignThumb template={t} />
                </button>

                <div className="dg__meta">
                  <h3 className="dg__name">
                    {t.name}
                    {!t.isActive && <span className="dg__off">معطّل</span>}
                  </h3>
                  <p className="dg__scope">
                    {SCOPE_LABEL[t.scope] ?? t.scope}
                    {t.documentTypeName ? ` — ${t.documentTypeName}` : t.mailCategoryName ? ` — ${t.mailCategoryName}` : ''}
                  </p>
                  {/* Owner line: department and user are shown together, not one instead of the
                      other — "whose design is this" is the question the list is scanned for. */}
                  {(t.orgUnitName || t.userName) && (
                    <p className="dg__owner">
                      {t.orgUnitName && <span className="dg__tag" title="القسم/الإدارة"><Landmark size={11} aria-hidden /> {t.orgUnitName}</span>}
                      {t.userName && <span className="dg__tag" title="المستخدم"><User size={11} aria-hidden /> {t.userName}</span>}
                    </p>
                  )}
                  <div className="dg__actions">
                    <button className="btn btn-ghost btn-sm" onClick={() => navigate(editUrl(t))}>
                      {mayEdit(t) ? 'تعديل' : 'عرض'}
                    </button>
                    {mayDelete(t) && (
                      <button className="btn btn-ghost btn-sm dg__del" onClick={() => remove(t)}>حذف</button>
                    )}
                  </div>
                </div>
              </article>
            </ContextMenuTrigger>

            <ContextMenuContent className="w-56">
              <ContextMenuLabel className="text-xs text-muted-foreground truncate max-w-[220px]">
                {t.name}
              </ContextMenuLabel>
              <ContextMenuSeparator />

              <ContextMenuItem onSelect={() => navigate(editUrl(t))}>
                <Pencil className="ml-2 h-4 w-4 opacity-60" />تعديل
              </ContextMenuItem>
              <ContextMenuItem onSelect={() => window.open(editUrl(t), '_blank')}>
                <ExternalLink className="ml-2 h-4 w-4 opacity-60" />فتح في تبويب جديد
              </ContextMenuItem>
              <ContextMenuItem onSelect={() => setPreview(t)}>
                <Eye className="ml-2 h-4 w-4 opacity-60" />معاينة بالحجم الكامل
              </ContextMenuItem>

              <ContextMenuSeparator />
              <ContextMenuItem onSelect={() => downloadDesignFile([t], safeFilename(t.name))}>
                <Download className="ml-2 h-4 w-4 opacity-60" />تصدير كملف
              </ContextMenuItem>
              <ContextMenuItem disabled={!canEdit} onSelect={() => void duplicate(t)}>
                <Copy className="ml-2 h-4 w-4 opacity-60" />تكرار
              </ContextMenuItem>
              <ContextMenuItem disabled={!mayEdit(t)} onSelect={() => void toggleActive(t)}>
                <Power className="ml-2 h-4 w-4 opacity-60" />{t.isActive ? 'تعطيل' : 'تفعيل'}
              </ContextMenuItem>

              <ContextMenuSeparator />
              <ContextMenuItem disabled={!mayDelete(t)} className="text-destructive"
                onSelect={() => void remove(t)}>
                <Trash2 className="ml-2 h-4 w-4 opacity-60" />حذف
              </ContextMenuItem>
            </ContextMenuContent>
          </ContextMenu>
        ))}
      </div>

      <Dialog open={preview !== null} onOpenChange={(o) => { if (!o) setPreview(null) }}>
        <DialogContent className="dg__previewbox">
          <DialogHeader><DialogTitle>{preview?.name}</DialogTitle></DialogHeader>
          {preview && (
            <div className="dg__previewscroll">
              {/* Same renderer as the thumbnail and the print path — just not scaled down. */}
              <DesignThumb template={preview} full />
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  )
}

/** List row → the save shape, so duplicate/enable can round-trip a design without re-fetching it. */
function toSave(t: ReportTemplate) {
  return {
    scope: (t.scope === 'User' ? 2 : t.scope === 'OrgUnit' ? 1 : 0) as TemplateScope,
    target: (t.target === 'Correspondence' ? 0 : t.target === 'Document' ? 1 : 2) as TemplateTarget,
    orgUnitId: t.orgUnitId, userId: t.userId,
    name: t.name, logoBase64: t.logoBase64, layoutJson: t.layoutJson,
    documentTypeId: t.documentTypeId, mailCategoryId: t.mailCategoryId,
    isActive: t.isActive,
  }
}

/**
 * A design rendered at full A4 width and then scaled down with a transform.
 *
 * Scaling rather than re-laying-out at thumbnail size matters: the design is authored against a
 * fixed page width, so re-flowing it narrow would rewrap every line and show a layout that does
 * not exist. `transform: scale` keeps the proportions the printer will produce.
 */
function DesignThumb({ template, full = false }: { template: ReportTemplate; full?: boolean }) {
  const html = useMemo(() => {
    try {
      // Sample values: the tokens are placeholders, and a thumbnail full of {{number}} would
      // read as broken rather than as a design.
      return renderLayout(template.layoutJson, {
        tokens: {
          institutionName: 'مؤسسة همم',
          number: 'DOC-2026-0001',
          subject: 'نموذج للمعاينة',
          date: '2026-08-02',
          recipient: 'الجهة المستلمة',
          sender: 'الجهة المرسِلة',
        },
        logo: template.logoBase64,
        tableRows: [{ name: 'مرفق تجريبي', size: '120 KB' }],
      })
    } catch {
      return null   // a malformed design shows a placeholder, it does not break the gallery
    }
  }, [template.layoutJson, template.logoBase64])

  if (!html) return <div className="dg__thumb dg__thumb--bad"><span>تعذّر عرض هذا التصميم</span></div>

  return (
    <div className={full ? 'dg__full' : 'dg__thumb'}>
      {/* Inert: pointer-events are off so clicks reach the card, and the content is decorative. */}
      <div className={full ? 'dg__fullpage' : 'dg__page'} aria-hidden dangerouslySetInnerHTML={{ __html: html }} />
    </div>
  )
}
