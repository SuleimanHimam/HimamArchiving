import { useEffect, useState, useCallback, useMemo, useRef } from 'react'
import {
  Search, Plus, Users, Building2, Pencil, Trash2, Copy, Mail, Phone, Smartphone,
  Printer, MapPin, LayoutGrid, Table2, MoreVertical, X, Check, ChevronLeft, ImagePlus,
} from 'lucide-react'
import {
  external, type ExternalOrg, type ExternalContact, type ContactChannel,
  ORG_TYPES, ORG_TYPE_AR, CHANNEL_TYPES, CHANNEL_TYPE_AR,
} from '../../lib/external'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { useExcelTable, type ColumnDef } from '../../components/ui/use-excel-table'
import { ColumnHeaderMenu } from '../../components/ui/column-filter'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '../../components/ui/dialog'
import { LabeledInput, LabeledSelect, LabeledTextarea } from '../../components/ui/labeled-input'
import {
  ContextMenu, ContextMenuContent, ContextMenuItem, ContextMenuLabel, ContextMenuSeparator,
  ContextMenuTrigger,
} from '../../components/ui/context-menu'
import '../../styles/forms.css'
import './external.css'

/* الجهات الخارجية — the address book the registry writes to and receives from.
 *
 * Two things are being done on this page and they are not the same size: keeping the list of
 * organisations tidy (rare, administrative) and finding the person to write to inside one of them
 * (constant, and the reason anyone opens the page). The old layout gave each half the screen, so
 * the half that mattered sat empty behind "اختر جهة" until something was clicked.
 *
 * So: the list gets the whole width and reads as a directory, and the contacts of one organisation
 * open in a panel over it. Cards are the default because an organisation is a handful of facts and
 * a few people — a shape a card carries better than a row — and the table stays one click away for
 * whoever is auditing the list rather than using it.
 */

const errOf = (e: unknown, fallback: string) =>
  (e as { response?: { data?: { error?: string } } })?.response?.data?.error ?? fallback

const CHANNEL_ICON: Record<string, typeof Mail> = {
  Email: Mail, Phone: Phone, Mobile: Smartphone, Fax: Printer, Address: MapPin,
}

/** mailto:/tel: where the channel supports it — the point of holding a number is to call it. */
function channelHref(type: string, value: string): string | null {
  if (type === 'Email') return `mailto:${value}`
  if (type === 'Phone' || type === 'Mobile') return `tel:${value.replace(/[^\d+]/g, '')}`
  return null
}

/** The two or three glyphs that tell this organisation from the next one at a glance.
 *
 *  Not simply the first letters of the first two words: Arabic names lean on «ال» and on a generic
 *  head word («جهة»، «شركة»، «مؤسسة»)، so a directory built that way ends up as a wall of identical
 *  tiles — «جهة خارجية ١٣» and «جهة خارجية ١٧» both reduce to «جخ». So the article is stripped, a
 *  trailing number is kept as itself (it is the whole difference between those two), and the head
 *  word is skipped when something more specific follows it. */
const GENERIC = ['جهة', 'شركة', 'مؤسسة', 'هيئة', 'وزارة', 'بنك', 'مكتب', 'دائرة', 'جمعية']
const initials = (name: string) => {
  const words = name.trim().split(/\s+/).filter(Boolean)
  if (words.length === 0) return '؟'

  const last = words[words.length - 1]
  const trailingNumber = /^[\d٠-٩]{1,3}$/.test(last) ? last : null

  const bare = (w: string) => (w.length > 2 && w.startsWith('ال') ? w.slice(2) : w)
  const meaningful = words
    .filter((w) => w !== trailingNumber)
    .filter((w, i, all) => !(i === 0 && all.length > 1 && GENERIC.includes(bare(w))))
    .map(bare)
    .filter(Boolean)

  if (trailingNumber) return (meaningful[0]?.[0] ?? '') + trailingNumber
  return meaningful.slice(0, 2).map((w) => w[0]).join('') || name[0] || '؟'
}

/** A stable colour per organisation — identity, not decoration, so it must not shift between loads. */
const TINTS = ['t1', 't2', 't3', 't4', 't5', 't6'] as const
const tintOf = (id: number) => TINTS[id % TINTS.length]

/** A logo will be uploaded for the organisations that matter and never for the rest, so the tile
 *  has to work either way: the crest when it exists, the tinted initials when it does not. */
function OrgTile({ org, size }: { org: { id: number; name: string; logoUrl?: string | null }; size?: number }) {
  const style = size ? { inlineSize: size, blockSize: size } : undefined
  return org.logoUrl
    ? <img className="ext__avatar ext__avatar--img" style={style} src={org.logoUrl} alt="" loading="lazy" />
    : <span className={`ext__avatar ext__${tintOf(org.id)}`} style={style} aria-hidden>{initials(org.name)}</span>
}

/** 500 KB before base64 — the same ceiling the branding logo uses, and comfortably under the
 *  column's cap once the encoding adds its third. */
const LOGO_MAX_BYTES = 500 * 1024

type View = 'cards' | 'table'
type Sort = 'name' | 'contacts' | 'type'

const SORTS: { key: Sort; label: string }[] = [
  { key: 'name', label: 'الاسم' },
  { key: 'contacts', label: 'الأكثر جهات اتصال' },
  { key: 'type', label: 'النوع' },
]

interface OrgDraft {
  id: number | null; name: string; nameEn: string; type: number; code: string; notes: string
  logoUrl: string | null
}
const emptyOrg: OrgDraft = { id: null, name: '', nameEn: '', type: 0, code: '', notes: '', logoUrl: null }

interface ContactDraft { id: number | null; name: string; title: string; channels: ContactChannel[] }
const emptyContact: ContactDraft = { id: null, name: '', title: '', channels: [{ type: 0, value: '', isPrimary: true }] }

export default function ExternalDirectoryPage() {
  const toast = useToast()
  const { confirm } = useConfirmDialog()

  const [orgs, setOrgs] = useState<ExternalOrg[]>([])
  const [search, setSearch] = useState('')
  const [typeFilter, setTypeFilter] = useState<string>('')
  const [view, setView] = useState<View>('cards')
  const [sort, setSort] = useState<Sort>('name')
  const [loading, setLoading] = useState(true)

  const [openOrg, setOpenOrg] = useState<ExternalOrg | null>(null)   // whose contacts are showing
  const [contacts, setContacts] = useState<ExternalContact[]>([])
  const [contactSearch, setContactSearch] = useState('')

  const [orgDraft, setOrgDraft] = useState<OrgDraft | null>(null)     // null = dialog closed
  const logoInput = useRef<HTMLInputElement>(null)
  const [contactDraft, setContactDraft] = useState<ContactDraft | null>(null)

  const loadOrgs = useCallback(async () => {
    setLoading(true)
    try {
      const r = await external.orgs(search || undefined)
      setOrgs(r.items)
    } catch (e) { toast.error(errOf(e, 'تعذّر تحميل الجهات')) }
    finally { setLoading(false) }
  }, [search, toast])

  const loadContacts = useCallback(async (orgId: number) => {
    try {
      const r = await external.contacts(orgId)
      setContacts(r.items)
    } catch (e) { toast.error(errOf(e, 'تعذّر تحميل جهات الاتصال')) }
  }, [toast])

  // Debounced: the search box hits the API on every keystroke otherwise, and this list is small
  // enough that the round trip is the only cost worth avoiding.
  useEffect(() => {
    const t = setTimeout(() => { loadOrgs() }, 250)
    return () => clearTimeout(t)
  }, [loadOrgs])

  useEffect(() => { if (openOrg) loadContacts(openOrg.id) }, [openOrg, loadContacts])

  const shown = useMemo(() => {
    const list = typeFilter ? orgs.filter((o) => o.type === typeFilter) : [...orgs]
    // localeCompare with the Arabic collator: a plain sort orders «ال…» by the article, so every
    // name starting with it clumps together in the order it happened to be entered.
    // numeric: «جهة خارجية 2» belongs before «جهة خارجية 13», which a plain string sort reverses.
    const byName = (a: ExternalOrg, b: ExternalOrg) =>
      a.name.localeCompare(b.name, 'ar', { numeric: true })
    return [...list].sort((a, b) =>
      sort === 'contacts' ? b.contactCount - a.contactCount || byName(a, b)
        : sort === 'type' ? (ORG_TYPE_AR[a.type] ?? a.type).localeCompare(ORG_TYPE_AR[b.type] ?? b.type, 'ar') || byName(a, b)
          : byName(a, b))
  }, [orgs, typeFilter, sort])

  const shownContacts = useMemo(() => {
    const q = contactSearch.trim().toLowerCase()
    if (!q) return contacts
    return contacts.filter((c) =>
      c.name.toLowerCase().includes(q)
      || (c.title ?? '').toLowerCase().includes(q)
      || c.channels.some((ch) => ch.value.toLowerCase().includes(q)))
  }, [contacts, contactSearch])

  // ── organisations ──────────────────────────────────────────────────────────

  async function saveOrg() {
    if (!orgDraft) return
    if (!orgDraft.name.trim()) { toast.error('اسم الجهة مطلوب'); return }
    const body = {
      name: orgDraft.name.trim(),
      nameEn: orgDraft.nameEn.trim() || null,
      type: orgDraft.type,
      code: orgDraft.code.trim() || null,
      notes: orgDraft.notes.trim() || null,
      logoUrl: orgDraft.logoUrl,
    }
    try {
      if (orgDraft.id == null) {
        await external.createOrg(body)
        toast.success('تمت إضافة الجهة')
      } else {
        // Editing existed in the API and nowhere in the page: a typo in a name meant deleting the
        // organisation and building it again, contacts and all.
        await external.updateOrg(orgDraft.id, body)
        toast.success('حُفظت التعديلات')
      }
      setOrgDraft(null)
      loadOrgs()
    } catch (e) { toast.error(errOf(e, 'تعذّر الحفظ')) }
  }

  async function removeOrg(o: ExternalOrg) {
    const warn = o.contactCount > 0
      ? `حذف «${o.name}»؟ ستُحذف معها ${o.contactCount} من جهات الاتصال.`
      : `حذف «${o.name}»؟`
    if (!(await confirm({ message: warn, destructive: true }))) return
    try {
      await external.deleteOrg(o.id)
      if (openOrg?.id === o.id) { setOpenOrg(null); setContacts([]) }
      toast.success('تم الحذف')
      loadOrgs()
    } catch (e) { toast.error(errOf(e, 'تعذّر الحذف')) }
  }

  const editOrg = (o: ExternalOrg) => setOrgDraft({
    id: o.id, name: o.name, nameEn: o.nameEn ?? '',
    type: Math.max(0, ORG_TYPES.indexOf(o.type)), code: o.code ?? '', notes: o.notes ?? '',
    logoUrl: o.logoUrl ?? null,
  })

  /** Reads the picked file straight into the draft as a data URI — the same shape the column
   *  stores, so nothing is uploaded until the form is saved and «إلغاء» really cancels. */
  function pickLogo(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''                       // so re-picking the same file fires change again
    if (!file || !orgDraft) return
    if (!file.type.startsWith('image/')) { toast.error('اختر ملف صورة'); return }
    if (file.size > LOGO_MAX_BYTES) { toast.error('حجم الصورة يجب ألا يتجاوز 500 كيلوبايت'); return }
    const reader = new FileReader()
    reader.onload = () => setOrgDraft((d) => (d ? { ...d, logoUrl: reader.result as string } : d))
    reader.onerror = () => toast.error('تعذّرت قراءة الصورة')
    reader.readAsDataURL(file)
  }

  // ── contacts ───────────────────────────────────────────────────────────────

  async function saveContact() {
    if (!openOrg || !contactDraft) return
    if (!contactDraft.name.trim()) { toast.error('اسم جهة الاتصال مطلوب'); return }
    const channels = contactDraft.channels
      .filter((c) => c.value.trim())
      .map((c) => ({ ...c, type: typeof c.type === 'string' ? Math.max(0, CHANNEL_TYPES.indexOf(c.type)) : c.type }))
    const body = {
      organizationId: openOrg.id,
      name: contactDraft.name.trim(),
      title: contactDraft.title.trim() || null,
      channels,
    }
    try {
      if (contactDraft.id == null) {
        await external.createContact(body)
        toast.success('تمت إضافة جهة الاتصال')
      } else {
        await external.updateContact(contactDraft.id, body)
        toast.success('حُفظت التعديلات')
      }
      setContactDraft(null)
      loadContacts(openOrg.id)
      loadOrgs()                       // the card's contact count moved
    } catch (e) { toast.error(errOf(e, 'تعذّر الحفظ')) }
  }

  async function removeContact(c: ExternalContact) {
    if (!(await confirm({ message: `حذف جهة الاتصال «${c.name}»؟`, destructive: true }))) return
    try {
      await external.deleteContact(c.id)
      toast.success('تم الحذف')
      if (openOrg) loadContacts(openOrg.id)
      loadOrgs()
    } catch (e) { toast.error(errOf(e, 'تعذّر الحذف')) }
  }

  const editContact = (c: ExternalContact) => setContactDraft({
    id: c.id, name: c.name, title: c.title ?? '',
    channels: c.channels.length
      ? c.channels.map((ch) => ({ ...ch, type: typeof ch.type === 'string' ? Math.max(0, CHANNEL_TYPES.indexOf(ch.type)) : ch.type }))
      : [{ type: 0, value: '', isPrimary: true }],
  })

  async function copyValue(value: string) {
    try {
      await navigator.clipboard.writeText(value)
      toast.success('نُسخ إلى الحافظة')
    } catch { toast.error('تعذّر النسخ') }
  }

  // ── table view keeps the sortable/filterable columns ───────────────────────

  const columns = useMemo<ColumnDef<ExternalOrg>[]>(() => [
    { key: 'name',     accessor: (o) => o.name },
    { key: 'type',     accessor: (o) => ORG_TYPE_AR[o.type] ?? String(o.type) },
    { key: 'code',     accessor: (o) => o.code ?? '—' },
    { key: 'contacts', accessor: (o) => String(o.contactCount), sortValue: (o) => o.contactCount },
  ], [])
  const { viewRows, getColumnProps } = useExcelTable(shown, columns)

  /** One menu, wherever an organisation is shown — right-click on a card or a row, or the ⋮ button. */
  const orgMenu = (o: ExternalOrg) => (
    <ContextMenuContent className="w-56 ext__menu">
      <ContextMenuLabel className="truncate">{o.name}</ContextMenuLabel>
      <ContextMenuSeparator />
      <ContextMenuItem onSelect={() => setOpenOrg(o)}>
        <Users size={14} aria-hidden /> جهات الاتصال ({o.contactCount})
      </ContextMenuItem>
      <ContextMenuItem onSelect={() => editOrg(o)}>
        <Pencil size={14} aria-hidden /> تعديل بيانات الجهة
      </ContextMenuItem>
      {o.code && (
        <ContextMenuItem onSelect={() => copyValue(o.code!)}>
          <Copy size={14} aria-hidden /> نسخ الرمز
        </ContextMenuItem>
      )}
      <ContextMenuSeparator />
      <ContextMenuItem className="ext__menu--danger" onSelect={() => removeOrg(o)}>
        <Trash2 size={14} aria-hidden /> حذف الجهة
      </ContextMenuItem>
    </ContextMenuContent>
  )

  return (
    <div className="ext">
      <div className="page__head">
        <div>
          <h1>الجهات الخارجية وجهات الاتصال</h1>
        </div>
        <button className="btn btn-seal" onClick={() => setOrgDraft(emptyOrg)}>
          <Plus size={16} aria-hidden /> جهة جديدة
        </button>
      </div>

      {/* One toolbar for the whole directory: find, narrow, and choose how to read it. */}
      <div className="ext__bar">
        <label className="ext__search">
          <Search size={15} aria-hidden />
          <input placeholder="ابحث باسم الجهة أو الرمز…" value={search}
            onChange={(e) => setSearch(e.target.value)} />
          {search && (
            <button type="button" className="ext__clear" title="مسح البحث" onClick={() => setSearch('')}>
              <X size={13} />
            </button>
          )}
        </label>

        <select className="ext__filter" value={typeFilter} aria-label="النوع"
          onChange={(e) => setTypeFilter(e.target.value)}>
          <option value="">كل الأنواع</option>
          {ORG_TYPES.map((t) => <option key={t} value={t}>{ORG_TYPE_AR[t]}</option>)}
        </select>

        <select className="ext__filter" value={sort} aria-label="الترتيب"
          onChange={(e) => setSort(e.target.value as Sort)}>
          {SORTS.map((s) => <option key={s.key} value={s.key}>ترتيب: {s.label}</option>)}
        </select>

        <span className="ext__count">{shown.length} جهة</span>

        <div className="ext__views" role="group" aria-label="طريقة العرض">
          <button type="button" className={view === 'cards' ? 'is-on' : ''} aria-pressed={view === 'cards'}
            onClick={() => setView('cards')} title="بطاقات">
            <LayoutGrid size={15} aria-hidden /> بطاقات
          </button>
          <button type="button" className={view === 'table' ? 'is-on' : ''} aria-pressed={view === 'table'}
            onClick={() => setView('table')} title="جدول">
            <Table2 size={15} aria-hidden /> جدول
          </button>
        </div>
      </div>

      {loading && <div className="ext__grid" aria-hidden>
        {Array.from({ length: 6 }).map((_, i) => <div key={i} className="ext__card ext__card--skeleton" />)}
      </div>}

      {!loading && shown.length === 0 && (
        <div className="ext__empty">
          <Building2 size={28} aria-hidden />
          <p>{search || typeFilter ? 'لا توجد جهة مطابقة لبحثك.' : 'لا توجد جهات خارجية بعد.'}</p>
          {(search || typeFilter)
            ? <button className="btn btn-ghost btn-sm" onClick={() => { setSearch(''); setTypeFilter('') }}>مسح البحث والتصفية</button>
            : <button className="btn btn-seal btn-sm" onClick={() => setOrgDraft(emptyOrg)}>+ أضف أول جهة</button>}
        </div>
      )}

      {!loading && shown.length > 0 && view === 'cards' && (
        <div className="ext__grid">
          {shown.map((o) => (
            <ContextMenu key={o.id} dir="rtl">
              <ContextMenuTrigger asChild>
                <article className="ext__card" onDoubleClick={() => setOpenOrg(o)}>
                  <header className="ext__cardhead">
                    <OrgTile org={o} />
                    <div className="ext__ident">
                      <h3 title={o.name}>{o.name}</h3>
                      {o.nameEn && <span className="ext__sub" title={o.nameEn}>{o.nameEn}</span>}
                    </div>
                    <button type="button" className="ext__more" title="إجراءات"
                      onClick={(e) => {
                        // The ⋮ button and right-click open the same menu: dispatch the event the
                        // trigger already listens for instead of keeping a second menu in state.
                        const t = e.currentTarget.closest('article')
                        t?.dispatchEvent(new MouseEvent('contextmenu', {
                          bubbles: true, clientX: e.clientX, clientY: e.clientY,
                        }))
                      }}>
                      <MoreVertical size={16} />
                    </button>
                  </header>

                  <div className="ext__meta">
                    <span className="ext__type">{ORG_TYPE_AR[o.type] ?? o.type}</span>
                    {o.code && <span className="ext__code mono">{o.code}</span>}
                    {!o.isActive && <span className="ext__off">موقوفة</span>}
                  </div>

                  <footer className="ext__cardfoot">
                    <span className="ext__people">
                      <Users size={14} aria-hidden />
                      {o.contactCount === 0 ? 'لا جهات اتصال'
                        : o.contactCount === 1 ? 'جهة اتصال واحدة'
                          : `${o.contactCount} جهات اتصال`}
                    </span>
                    <button type="button" className="ext__open" onClick={() => setOpenOrg(o)}>
                      فتح <ChevronLeft size={14} aria-hidden />
                    </button>
                  </footer>
                </article>
              </ContextMenuTrigger>
              {orgMenu(o)}
            </ContextMenu>
          ))}
        </div>
      )}

      {!loading && shown.length > 0 && view === 'table' && (
        <div className="card table-card">
          <table className="table reg-table">
            <thead>
              <tr>
                <th><ColumnHeaderMenu label="الجهة" {...getColumnProps('name')} /></th>
                <th><ColumnHeaderMenu label="النوع" {...getColumnProps('type')} /></th>
                <th><ColumnHeaderMenu label="الرمز" {...getColumnProps('code')} /></th>
                <th><ColumnHeaderMenu label="جهات الاتصال" {...getColumnProps('contacts')} /></th>
                <th aria-label="إجراءات" />
              </tr>
            </thead>
            <tbody>
              {viewRows.map((o) => (
                <ContextMenu key={o.id} dir="rtl">
                  <ContextMenuTrigger asChild>
                    <tr className="reg-row ext__row" onClick={() => setOpenOrg(o)}>
                      <td>
                        <div className="ext__rowident">
                          <OrgTile org={o} size={28} />
                          <div>
                            <div style={{ fontWeight: 600 }}>{o.name}</div>
                            {o.nameEn && <div className="text-muted-foreground" style={{ fontSize: 12 }}>{o.nameEn}</div>}
                          </div>
                        </div>
                      </td>
                      <td><span className="ext__type">{ORG_TYPE_AR[o.type] ?? o.type}</span></td>
                      <td className="mono">{o.code ?? '—'}</td>
                      <td className="mono">{o.contactCount}</td>
                      <td className="ext__rowactions">
                        <button type="button" className="btn btn-ghost btn-sm" title="جهات الاتصال"
                          onClick={(e) => { e.stopPropagation(); setOpenOrg(o) }}>
                          <Users size={14} />
                        </button>
                        <button type="button" className="btn btn-ghost btn-sm" title="تعديل"
                          onClick={(e) => { e.stopPropagation(); editOrg(o) }}>
                          <Pencil size={14} />
                        </button>
                        <button type="button" className="btn btn-ghost btn-sm text-destructive" title="حذف"
                          onClick={(e) => { e.stopPropagation(); removeOrg(o) }}>
                          <Trash2 size={14} />
                        </button>
                      </td>
                    </tr>
                  </ContextMenuTrigger>
                  {orgMenu(o)}
                </ContextMenu>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* ── The organisation's people, over the list rather than beside it ── */}
      <Dialog open={!!openOrg} onOpenChange={(v) => { if (!v) { setOpenOrg(null); setContactDraft(null); setContactSearch('') } }}>
        <DialogContent className="ext__sheet" dir="rtl">
          <DialogHeader>
            <DialogTitle className="ext__sheettitle">
              <OrgTile org={{ id: openOrg?.id ?? 0, name: openOrg?.name ?? '', logoUrl: openOrg?.logoUrl }} />
              <span>
                {openOrg?.name}
                <em>{ORG_TYPE_AR[openOrg?.type ?? ''] ?? openOrg?.type}{openOrg?.code ? ` · ${openOrg.code}` : ''}</em>
              </span>
            </DialogTitle>
          </DialogHeader>

          <div className="ext__sheetbar">
            <label className="ext__search ext__search--sm">
              <Search size={14} aria-hidden />
              <input placeholder="ابحث باسم أو رقم أو بريد…" value={contactSearch}
                onChange={(e) => setContactSearch(e.target.value)} />
            </label>
            <button type="button" className="btn btn-seal btn-sm"
              onClick={() => setContactDraft(emptyContact)}>
              <Plus size={14} aria-hidden /> جهة اتصال
            </button>
          </div>

          <div className="ext__contacts">
            {shownContacts.map((c) => (
              <div key={c.id} className="ext__contact">
                <div className="ext__contacthead">
                  <div>
                    <strong>{c.name}</strong>
                    {c.title && <span className="ext__sub"> · {c.title}</span>}
                  </div>
                  <span className="ext__contactacts">
                    <button type="button" className="btn btn-ghost btn-sm" title="تعديل"
                      onClick={() => editContact(c)}><Pencil size={13} /></button>
                    <button type="button" className="btn btn-ghost btn-sm text-destructive" title="حذف"
                      onClick={() => removeContact(c)}><Trash2 size={13} /></button>
                  </span>
                </div>

                <div className="ext__channels">
                  {c.channels.length === 0 && <span className="ext__sub">لا توجد قنوات اتصال</span>}
                  {c.channels.map((ch) => {
                    const type = String(ch.type)
                    const Icon = CHANNEL_ICON[type] ?? Mail
                    const href = channelHref(type, ch.value)
                    return (
                      <span key={ch.id ?? `${type}-${ch.value}`}
                        className={`ext__chan ${ch.isPrimary ? 'is-primary' : ''}`}
                        title={`${CHANNEL_TYPE_AR[type] ?? type}${ch.isPrimary ? ' (رئيسي)' : ''}`}>
                        <Icon size={13} aria-hidden />
                        {href
                          ? <a href={href} className="mono ext__chanval">{ch.value}</a>
                          : <span className="mono ext__chanval">{ch.value}</span>}
                        <button type="button" className="ext__copy" title="نسخ"
                          onClick={() => copyValue(ch.value)}><Copy size={11} /></button>
                      </span>
                    )
                  })}
                </div>
              </div>
            ))}

            {shownContacts.length === 0 && (
              <div className="ext__empty ext__empty--sm">
                <Users size={22} aria-hidden />
                <p>{contactSearch ? 'لا نتيجة لهذا البحث.' : 'لا توجد جهات اتصال لهذه الجهة بعد.'}</p>
                {!contactSearch && (
                  <button className="btn btn-seal btn-sm" onClick={() => setContactDraft(emptyContact)}>
                    + أضف أول جهة اتصال
                  </button>
                )}
              </div>
            )}
          </div>

          <DialogFooter>
            <button className="btn btn-ghost btn-sm" onClick={() => openOrg && editOrg(openOrg)}>
              <Pencil size={14} aria-hidden /> تعديل بيانات الجهة
            </button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Organisation form: one dialog for both adding and editing ── */}
      <Dialog open={!!orgDraft} onOpenChange={(v) => { if (!v) setOrgDraft(null) }}>
        <DialogContent className="ext__form" dir="rtl">
          <DialogHeader>
            <DialogTitle>{orgDraft?.id == null ? 'جهة خارجية جديدة' : 'تعديل بيانات الجهة'}</DialogTitle>
          </DialogHeader>
          {orgDraft && (
            <div className="ext__logorow">
              {orgDraft.logoUrl
                ? <img className="ext__logoprev" src={orgDraft.logoUrl} alt="شعار الجهة" />
                : <span className="ext__logoprev ext__logoprev--empty" aria-hidden><ImagePlus size={20} /></span>}
              <div className="ext__logoacts">
                <span className="ext__chanlabel">شعار الجهة</span>
                <span className="ext__logohint">PNG أو JPG أو SVG · حتى 500 كيلوبايت</span>
                <span>
                  <button type="button" className="btn btn-ghost btn-sm" onClick={() => logoInput.current?.click()}>
                    {orgDraft.logoUrl ? 'تغيير الصورة' : 'رفع صورة'}
                  </button>
                  {orgDraft.logoUrl && (
                    <button type="button" className="btn btn-ghost btn-sm text-destructive"
                      onClick={() => setOrgDraft({ ...orgDraft, logoUrl: null })}>إزالة</button>
                  )}
                </span>
              </div>
              <input ref={logoInput} type="file" accept="image/*" hidden onChange={pickLogo} />
            </div>
          )}
          {orgDraft && (
            <div className="ext__formgrid">
              <LabeledInput label="الاسم" required value={orgDraft.name}
                onChange={(e) => setOrgDraft({ ...orgDraft, name: e.target.value })} />
              <LabeledInput label="الاسم بالإنجليزية" value={orgDraft.nameEn}
                onChange={(e) => setOrgDraft({ ...orgDraft, nameEn: e.target.value })} />
              <LabeledSelect label="النوع" value={String(orgDraft.type)}
                onChange={(e) => setOrgDraft({ ...orgDraft, type: Number(e.target.value) })}>
                {ORG_TYPES.map((t, i) => <option key={t} value={i}>{ORG_TYPE_AR[t]}</option>)}
              </LabeledSelect>
              <LabeledInput label="الرمز" value={orgDraft.code}
                onChange={(e) => setOrgDraft({ ...orgDraft, code: e.target.value })} />
              <LabeledTextarea label="ملاحظات" rows={2} className="ext__wide" value={orgDraft.notes}
                onChange={(e) => setOrgDraft({ ...orgDraft, notes: e.target.value })} />
            </div>
          )}
          <DialogFooter>
            <button className="btn btn-ghost" onClick={() => setOrgDraft(null)}>إلغاء</button>
            <button className="btn btn-seal" onClick={saveOrg}><Check size={15} aria-hidden /> حفظ</button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Contact form ── */}
      <Dialog open={!!contactDraft} onOpenChange={(v) => { if (!v) setContactDraft(null) }}>
        <DialogContent className="ext__form" dir="rtl">
          <DialogHeader>
            <DialogTitle>{contactDraft?.id == null ? 'جهة اتصال جديدة' : 'تعديل جهة الاتصال'}</DialogTitle>
          </DialogHeader>
          {contactDraft && (
            <>
              <div className="ext__formgrid">
                <LabeledInput label="الاسم" required value={contactDraft.name}
                  onChange={(e) => setContactDraft({ ...contactDraft, name: e.target.value })} />
                <LabeledInput label="المسمى الوظيفي" value={contactDraft.title}
                  onChange={(e) => setContactDraft({ ...contactDraft, title: e.target.value })} />
              </div>

              <div className="ext__chanedit">
                <span className="ext__chanlabel">قنوات الاتصال</span>
                {contactDraft.channels.map((ch, i) => (
                  <div key={i} className="ext__chanrow">
                    <select className="select" value={typeof ch.type === 'string' ? Math.max(0, CHANNEL_TYPES.indexOf(ch.type)) : ch.type}
                      onChange={(e) => {
                        const cs = [...contactDraft.channels]
                        cs[i] = { ...ch, type: Number(e.target.value) }
                        setContactDraft({ ...contactDraft, channels: cs })
                      }}>
                      {CHANNEL_TYPES.map((t, ti) => <option key={t} value={ti}>{CHANNEL_TYPE_AR[t]}</option>)}
                    </select>
                    <input className="input" placeholder="القيمة" value={ch.value}
                      onChange={(e) => {
                        const cs = [...contactDraft.channels]
                        cs[i] = { ...ch, value: e.target.value }
                        setContactDraft({ ...contactDraft, channels: cs })
                      }} />
                    <label className="ext__primary" title="القناة الرئيسية">
                      <input type="radio" name="primary-channel" checked={ch.isPrimary}
                        onChange={() => setContactDraft({
                          ...contactDraft,
                          channels: contactDraft.channels.map((c, ci) => ({ ...c, isPrimary: ci === i })),
                        })} />
                      رئيسي
                    </label>
                    <button type="button" className="btn btn-ghost btn-sm" title="حذف القناة"
                      disabled={contactDraft.channels.length === 1}
                      onClick={() => setContactDraft({
                        ...contactDraft,
                        channels: contactDraft.channels.filter((_, ci) => ci !== i),
                      })}><X size={13} /></button>
                  </div>
                ))}
                <button type="button" className="btn btn-ghost btn-sm"
                  onClick={() => setContactDraft({
                    ...contactDraft,
                    channels: [...contactDraft.channels, { type: 0, value: '', isPrimary: false }],
                  })}>
                  <Plus size={13} aria-hidden /> قناة أخرى
                </button>
              </div>
            </>
          )}
          <DialogFooter>
            <button className="btn btn-ghost" onClick={() => setContactDraft(null)}>إلغاء</button>
            <button className="btn btn-seal" onClick={saveContact}><Check size={15} aria-hidden /> حفظ</button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
