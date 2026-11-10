import type { ReportTemplate, SaveReportTemplate, TemplateScope, TemplateTarget } from './reportTemplates'

/**
 * Moving print designs between installations (and out of one as a backup).
 *
 * The file carries the design itself — layout, logo, what it is for — but deliberately not the
 * database ids it was bound to. An `orgUnitId` of 3 means "المالية" here and something else, or
 * nothing at all, in the system it lands in; importing it blindly would silently attach a design
 * to the wrong department. Names travel instead, and the importer re-binds what it can resolve.
 */

const KIND = 'archiving.print-design'
const VERSION = 1

export interface DesignFileEntry {
  name: string
  target: string                 // 'Correspondence' | 'Document' | 'All'
  scope: string                  // 'Default' | 'OrgUnit' | 'User'
  orgUnitName: string | null     // resolved by name on import, never by id
  documentTypeName: string | null
  mailCategoryName: string | null
  logoBase64: string | null
  layoutJson: string
}

export interface DesignFile {
  kind: typeof KIND
  version: number
  exportedAt: string
  designs: DesignFileEntry[]
}

const TARGET_NUM: Record<string, TemplateTarget> = { Correspondence: 0, Document: 1, All: 2 }

export function toFileEntry(t: ReportTemplate): DesignFileEntry {
  return {
    name: t.name, target: t.target, scope: t.scope,
    orgUnitName: t.orgUnitName, documentTypeName: t.documentTypeName,
    mailCategoryName: t.mailCategoryName,
    logoBase64: t.logoBase64, layoutJson: t.layoutJson,
  }
}

export function buildDesignFile(templates: ReportTemplate[]): DesignFile {
  return { kind: KIND, version: VERSION, exportedAt: new Date().toISOString(), designs: templates.map(toFileEntry) }
}

/** Save the designs as a .json file the browser downloads. */
export function downloadDesignFile(templates: ReportTemplate[], filename: string) {
  const blob = new Blob([JSON.stringify(buildDesignFile(templates), null, 2)], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename.endsWith('.json') ? filename : `${filename}.json`
  document.body.appendChild(a)
  a.click()
  a.remove()
  // Revoke late: Firefox cancels an in-flight download if the URL dies too soon.
  setTimeout(() => URL.revokeObjectURL(url), 30_000)
}

/** A filename-safe version of a design name — Arabic is kept, path and shell characters are not. */
export function safeFilename(name: string) {
  return name.replace(/[\\/:*?"<>|]+/g, '-').trim().slice(0, 60) || 'design'
}

export class DesignFileError extends Error {}

/**
 * Read an exported file, rejecting anything that is not one.
 *
 * Every field is checked before it is trusted: this content came from a file on disk, so a
 * partial or hand-edited one must fail loudly rather than create half a design.
 */
export function parseDesignFile(text: string): DesignFileEntry[] {
  let raw: unknown
  try { raw = JSON.parse(text) } catch { throw new DesignFileError('الملف ليس بصيغة JSON صالحة') }

  const f = raw as Partial<DesignFile>
  if (!f || typeof f !== 'object' || f.kind !== KIND) throw new DesignFileError('هذا ليس ملف تصاميم طباعة')
  if (typeof f.version !== 'number' || f.version > VERSION) throw new DesignFileError('الملف من إصدار أحدث من هذا النظام')
  if (!Array.isArray(f.designs) || f.designs.length === 0) throw new DesignFileError('الملف لا يحتوي أي تصميم')

  return f.designs.map((d, i) => {
    const at = `التصميم رقم ${i + 1}`
    if (!d || typeof d !== 'object') throw new DesignFileError(`${at}: بيانات غير صالحة`)
    if (typeof d.name !== 'string' || !d.name.trim()) throw new DesignFileError(`${at}: بلا اسم`)
    if (typeof d.layoutJson !== 'string') throw new DesignFileError(`${at}: بلا تخطيط`)
    try { JSON.parse(d.layoutJson) } catch { throw new DesignFileError(`«${d.name}»: التخطيط تالف`) }
    if (typeof d.target !== 'string' || !(d.target in TARGET_NUM)) throw new DesignFileError(`«${d.name}»: نوع غير معروف`)
    return {
      name: d.name.trim(),
      target: d.target,
      scope: typeof d.scope === 'string' ? d.scope : 'Default',
      orgUnitName: typeof d.orgUnitName === 'string' ? d.orgUnitName : null,
      documentTypeName: typeof d.documentTypeName === 'string' ? d.documentTypeName : null,
      mailCategoryName: typeof d.mailCategoryName === 'string' ? d.mailCategoryName : null,
      logoBase64: typeof d.logoBase64 === 'string' ? d.logoBase64 : null,
      layoutJson: d.layoutJson,
    }
  })
}

export interface ImportContext {
  /** Department name → id in *this* system. A design whose department is absent lands unbound. */
  orgUnitsByName: Map<string, number>
  /** Names already taken, so an import never collides with an existing design. */
  existingNames: Set<string>
  myUserId: number | null
}

export interface ImportPlan {
  body: SaveReportTemplate
  /** What could not be carried over, shown to the user instead of being applied silently. */
  notes: string[]
}

/**
 * Turn a file entry into something this system can save.
 *
 * Always inactive: an imported design that arrived active could immediately win a print job it
 * was never reviewed for. Making it live is one click in the designer, and that click is a
 * decision someone made here.
 */
export function planImport(d: DesignFileEntry, ctx: ImportContext): ImportPlan {
  const notes: string[] = []

  let scope: TemplateScope = 0
  let orgUnitId: number | null = null
  let userId: number | null = null

  if (d.scope === 'OrgUnit') {
    const id = d.orgUnitName ? ctx.orgUnitsByName.get(d.orgUnitName) : undefined
    if (id != null) { scope = 1; orgUnitId = id }
    else notes.push(`«${d.name}»: القسم «${d.orgUnitName ?? '—'}» غير موجود — استُورد كتصميم عام`)
  } else if (d.scope === 'User') {
    // A personal design belongs to whoever imported it; the original owner is an id we cannot
    // meaningfully map, and guessing by name would attach it to the wrong person.
    if (ctx.myUserId != null) { scope = 2; userId = ctx.myUserId; notes.push(`«${d.name}»: استُورد كتصميم شخصي باسمك`) }
    else notes.push(`«${d.name}»: استُورد كتصميم عام`)
  }

  if (d.documentTypeName || d.mailCategoryName) {
    notes.push(`«${d.name}»: التخصيص لـ«${d.documentTypeName ?? d.mailCategoryName}» لم يُنقل — اضبطه بعد الاستيراد`)
  }

  return {
    body: {
      scope, orgUnitId, userId,
      target: TARGET_NUM[d.target],
      name: uniqueName(d.name, ctx.existingNames),
      logoBase64: d.logoBase64,
      layoutJson: d.layoutJson,
      // Narrowing is dropped: a document-type id from another system points at the wrong type here.
      documentTypeId: null,
      mailCategoryId: null,
      isActive: false,
    },
    notes,
  }
}

/** «ترويسة» → «ترويسة (مستورد)» → «ترويسة (مستورد 2)» … so an import never overwrites by name. */
export function uniqueName(name: string, taken: Set<string>) {
  if (!taken.has(name)) { taken.add(name); return name }
  const base = `${name} (مستورد)`
  if (!taken.has(base)) { taken.add(base); return base }
  for (let n = 2; ; n++) {
    const candidate = `${name} (مستورد ${n})`
    if (!taken.has(candidate)) { taken.add(candidate); return candidate }
  }
}
