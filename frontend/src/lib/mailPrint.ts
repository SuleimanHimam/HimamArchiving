import { auth } from './auth'
import { printWithTemplate, previewDocumentUrl, warmPreviewWindow, failPreviewWindow } from './reportTemplates'
import { isSensitive } from './useScreenGuard'
import { diwan, type DiwanDetail } from './diwan'
import { internalMail, CLASS_KEYS, type InternalDetail } from './internalMail'

/**
 * Which gallery tab an outgoing external letter prints from — «الصادر الخارجي» (`Document`).
 * Internal messages use `Correspondence`, so the two registers never share a design by accident.
 */
const DIWAN_TARGET = 1 as const

/** May send correspondence to a printer. Gate every print affordance on this. */
export const canPrintMail = () => auth.hasPermission('Correspondence.Print')

/**
 * One screen, not two.
 *
 * The platform's print dialog already shows the document beside the printer, the paper and the
 * copies — it *is* a preview. Rendering our own window first and then handing over to that dialog
 * showed the same letter twice in a row, and left the first window behind when the dialog closed.
 *
 * So «معاينة» resolves to whichever single screen the reader is entitled to: the print dialog when
 * they may print, our own read-only window when they may not — and someone who may not print has
 * no dialog to be shown, which is the whole reason that window still exists.
 */
const previewMode = (): 'print' | 'preview' => (canPrintMail() ? 'print' : 'preview')

/**
 * Claim the preview window while the click is still fresh — but only when a window is what the
 * reader is getting. Warming one for someone who is about to meet the print dialog would open the
 * very popup this collapse exists to remove.
 */
export function warmMailPreview() {
  if (previewMode() === 'preview') warmPreviewWindow()
}

/** Report a failed preview in the window it was promised in, if one was ever opened. */
export function failMailPreview(message: string) {
  if (previewMode() === 'preview') failPreviewWindow(message)
}

type TFn = (key: string, opts?: Record<string, unknown>) => string
type DateFn = (iso: string) => string

const plain = (html: string | null) => (html ?? '').replace(/<[^>]+>/g, ' ').trim()
const rows = (atts: { fileName: string; sizeBytes: number }[]) =>
  atts.map((a) => ({ name: a.fileName, size: `${Math.round(a.sizeBytes / 1024)} KB` }))

/* What gets printed depends on where the letter came from:
   ─ صادر خارجي (outgoing external) is a letter WE wrote, so it prints on the letterhead with
     the logo and the editor content, then its attachments.
   ─ everything else is a received/scanned document, so only the scanned attachments print —
     a letterhead there would misrepresent someone else's letter as ours.
   If such a mail has no attachments there is nothing to print, so it falls back to the
   letterhead view rather than sending a blank page to the printer. */

/** Opens the merged-attachments PDF in a hidden frame and fires the print dialog. */
async function printAttachments(fetchUrl: () => Promise<string>): Promise<boolean> {
  let url = ''
  try {
    url = await fetchUrl()
  } catch {
    return false            // no attachments (or unreadable) — caller falls back
  }
  await new Promise<void>((resolve) => {
    const frame = document.createElement('iframe')
    frame.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0;'
    frame.src = url
    frame.onload = () => {
      try { frame.contentWindow?.focus(); frame.contentWindow?.print() } catch { /* blocked */ }
      // Keep the frame alive briefly so the print dialog can read it, then clean up.
      setTimeout(() => { frame.remove(); URL.revokeObjectURL(url) }, 60_000)
      resolve()
    }
    document.body.appendChild(frame)
  })
  return true
}

/**
 * Print a Diwan (external) letter.
 *
 * صادر → the server-rendered PDF (one fixed master layout, QuestPDF) then its attachments.
 * وارد → the scanned original alone; putting our letterhead on someone else's letter would
 * misrepresent it.
 *
 * Outgoing letters no longer render in the browser at all. The PDF the printer receives is the
 * same file the designer previews, so the two cannot drift apart.
 */
export async function printDiwanMail(m: DiwanDetail, t: TFn, dateTime: DateFn) {
  const isOutgoing = m.direction === 'Outgoing'
  if (!isOutgoing) {
    // Incoming: print the scanned original alone. Fall back to the letterhead if nothing is attached.
    if (await printAttachments(() => diwan.fetchCombinedUrl(m.id))) return
  }
  await printDiwanLetterhead(m, t, dateTime)
  // Outgoing letters carry their attachments after the letter itself.
  if (isOutgoing && m.attachments.length > 0) await printAttachments(() => diwan.fetchCombinedUrl(m.id))
}

/**
 * Preview a Diwan (external) letter on screen instead of sending it to the printer.
 *
 * Shows what printing would produce, which is not always the letterhead: a وارد letter prints
 * its scanned original, so that is what the preview shows too. Previewing is not gated on
 * Correspondence.Print — reading a letter you may already open is not printing it — but the
 * preview's own print button is, so "may look, may not print" is a real state.
 */
export async function previewDiwanMail(m: DiwanDetail, t: TFn, dateTime: DateFn) {
  const allowPrint = canPrintMail()
  warmMailPreview()
  if (m.direction !== 'Outgoing' && m.attachments.length > 0) {
    const url = await diwan.fetchCombinedUrl(m.id)
    previewDocumentUrl(url, m.registerNumber, { allowPrint, guard: isSensitive(m.classification) })
    return
  }
  await printDiwanLetterhead(m, t, dateTime, { mode: previewMode(), allowPrint })
}

/** An unsaved Diwan letter, as the compose form holds it. */
export interface DiwanDraft {
  subject: string
  body: string
  /** ISO date strings, as the pickers produce them. */
  mailDate: string
  sentDate: string
  classification: number
  categoryId: number | null
  categoryName: string | null
  externalOrgName: string
  addresseeName: string
  addresseeTitle: string
  fileNumber: string
  keywords: string
  attachments: { name: string; size: number }[]
}

/**
 * Preview a letter that has not been saved yet — the letterhead with whatever is typed so far.
 *
 * The register number does not exist before saving, so its slot shows a placeholder rather than
 * a number that would later turn out to be different. The design resolves against the *current*
 * user and department, which is who the letter will belong to once saved.
 */
export async function previewDiwanDraft(d: DiwanDraft, t: TFn, dateTime: DateFn) {
  const pending = t('diwan.autoNew', { defaultValue: '—' })
  const me = auth.getUser()
  await printWithTemplate({
    target: DIWAN_TARGET,
    mode: previewMode(),
    allowPrint: canPrintMail(),
    guard: isSensitive(CLASS_KEYS[d.classification] ?? ''),
    // The draft belongs to whoever is writing it, so it previews on their chosen design.
    userId: me?.id ?? null,
    orgUnitId: me?.orgUnitId ?? null,
    sealUserId: me?.id ?? null,
    tokens: {
      number: pending,
      subject: d.subject,
      date: d.mailDate ? dateTime(d.mailDate) : '',
      sentDate: d.sentDate ? dateTime(d.sentDate) : '',
      recipient: d.externalOrgName,
      recipientName: [d.addresseeTitle, d.addresseeName].filter(Boolean).join(' — '),
      confidentiality: CLASS_KEYS[d.classification] ?? '',
      classification: d.categoryName ?? '',
      fileReference: d.fileNumber,
      keywords: d.keywords,
      body: plain(d.body),
    },
    htmlTokens: { body: d.body },
    tableRows: d.attachments.map((a) => ({ name: a.name, size: `${Math.round(a.size / 1024)} KB` })),
    title: pending,
  })
}

/**
 * An outgoing letter on its letterhead.
 *
 * The design comes from the gallery — resolved for the letter's **author**, honouring the design
 * that author picked for themselves. Whoever sends it to the printer is irrelevant: a clerk
 * printing someone else's letter must reproduce that person's letterhead and seal, not their own.
 */
function printDiwanLetterhead(
  m: DiwanDetail,
  t: TFn,
  dateTime: DateFn,
  output: { mode?: 'print' | 'preview'; allowPrint?: boolean } = {},
) {
  return printWithTemplate({
    target: DIWAN_TARGET,
    mode: output.mode,
    allowPrint: output.allowPrint,
    guard: isSensitive(m.classification),
    userId: m.createdByUserId ?? auth.getUser()?.id ?? null,
    orgUnitId: m.createdByOrgUnitId,
    sealUserId: m.createdByUserId,
    tokens: {
      number: m.registerNumber,
      subject: m.subject,
      date: dateTime(m.mailDate),
      sentDate: m.sentDate ? dateTime(m.sentDate) : '',
      recipient: m.externalOrgName ?? '',
      recipientName: [m.addresseeTitle, m.addresseeName].filter(Boolean).join(' — '),
      confidentiality: t(`corr.class.${m.classification}`, { defaultValue: m.classification }),
      priority: t(`corr.priority.${m.priority}`, { defaultValue: m.priority }),
      classification: m.categoryName ?? '',
      fileReference: m.fileNumber ?? '',
      keywords: m.keywords ?? '',
      body: plain(m.body),
    },
    htmlTokens: { body: m.body ?? '' },
    tableRows: rows(m.attachments.map((a) => ({ fileName: a.fileName, sizeBytes: a.sizeBytes }))),
    title: m.registerNumber,
  })
}

/** An internal message as it stands on the compose form, before it has been sent. */
export interface InternalDraft {
  subject: string
  body: string
  classification: number
  /** نوع المراسلة, carried through a reply or forward — it decides which letterhead applies. */
  categoryId: number | null
  /** The To line as typed. Names only: a department or group is one line here, not its members. */
  recipientNames: string[]
  attachments: { name: string; size: number }[]
}

/**
 * Preview an internal message that has not been sent yet.
 *
 * Deliberately built from the same call as <see cref="printInternalLetterhead"/> — same target,
 * same design resolution, same tokens — so what the writer sees here is what the recipient will
 * print. The message number is assigned on send, so its slot shows a placeholder rather than a
 * number that would turn out to be a different one.
 */
export async function previewInternalDraft(d: InternalDraft, t: TFn, dateTime: DateFn) {
  const pending = t('diwan.autoNew', { defaultValue: '—' })
  const me = auth.getUser()
  const now = new Date().toISOString()
  const classKey = CLASS_KEYS[d.classification] ?? ''
  await printWithTemplate({
    target: 0,
    mode: previewMode(),
    allowPrint: canPrintMail(),
    guard: isSensitive(classKey),
    // The draft belongs to whoever is writing it, so it previews on their design and seal.
    userId: me?.id ?? null,
    mailCategoryId: d.categoryId,
    sealUserId: me?.id ?? null,
    tokens: {
      number: pending,
      subject: d.subject,
      date: dateTime(now),
      sentDate: dateTime(now),
      recipient: d.recipientNames.join('، '),
      sender: me?.fullName ?? '',
      senderName: me?.fullName ?? '',
      confidentiality: classKey ? t(`corr.class.${classKey}`, { defaultValue: classKey }) : '',
      classification: '',
      fileReference: '',
      keywords: '',
      body: plain(d.body),
    },
    htmlTokens: { body: d.body },
    tableRows: d.attachments.map((a) => ({ name: a.name, size: `${Math.round(a.size / 1024)} KB` })),
    title: pending,
  })
}

/** What an internal message prints. The user picks this before printing. */
export type MailPrintMode = 'both' | 'content' | 'attachments'

/** Whether asking is even worth it — with nothing attached, only the content can print. */
export const mailHasAttachments = (m: { attachments: unknown[] }) => m.attachments.length > 0

type ChooseFn = (opts: {
  title?: string
  message?: string
  options: { key: string; label: string; description?: string }[]
}) => Promise<string | null>

/**
 * Ask what to print. Returns the chosen mode, or null if dismissed.
 * The dialog always shows so the options are discoverable; when nothing is attached the two
 * attachment choices say so instead of quietly disappearing.
 */
export async function askPrintMode(choose: ChooseFn, hasAttachments: boolean): Promise<MailPrintMode | null> {
  const none = 'لا توجد مرفقات لهذه المراسلة'
  const picked = await choose({
    title: 'طباعة المراسلة',
    message: 'اختر ما تريد طباعته',
    options: [
      {
        key: 'both',
        label: 'المحتوى مع المرفقات',
        description: hasAttachments ? 'الترويسة والنص المحرَّر، ثم المرفقات بعده' : none,
      },
      { key: 'content', label: 'المحتوى فقط', description: 'الترويسة والنص المحرَّر بدون المرفقات' },
      {
        key: 'attachments',
        label: 'المرفقات فقط',
        description: hasAttachments ? 'الملفات الممسوحة ضوئيًا فقط' : none,
      },
    ],
  })
  return picked as MailPrintMode | null
}

/**
 * Print an internal message.
 * `both` letterhead + content, then the attachments · `content` letterhead only ·
 * `attachments` the scanned files only (falls back to the content if none are readable).
 */
export async function printInternalMail(
  m: InternalDetail, t: TFn, dateTime: DateFn, mode: MailPrintMode = 'both',
) {
  if (mode === 'attachments') {
    if (await printAttachments(() => internalMail.fetchCombinedUrl(m.id))) return
    // Nothing printable was attached — show the content rather than a blank page.
    await printInternalLetterhead(m, t, dateTime)
    return
  }

  await printInternalLetterhead(m, t, dateTime)
  if (mode === 'both' && m.attachments.length > 0) {
    await printAttachments(() => internalMail.fetchCombinedUrl(m.id))
  }
}

/** The letterhead view: logo, header and the editor content. */
function printInternalLetterhead(m: InternalDetail, t: TFn, dateTime: DateFn) {
  const to = m.recipients.find((r) => r.addressing === 'To') ?? m.recipients[0]
  return printWithTemplate({
    target: 0,
    guard: isSensitive(m.classification),
    // As with Diwan letters, the design belongs to whoever sent the message.
    userId: m.senderUserId ?? auth.getUser()?.id ?? null,
    // The letterhead follows the message's own type (قسيمة راتب، سند قبض …), and the seal
    // belongs to whoever sent it — not to whoever happens to be printing.
    mailCategoryId: m.categoryId,
    sealUserId: m.senderUserId,
    tokens: {
      number: m.mailNumber,
      subject: m.subject,
      date: dateTime(m.mailDate),
      sentDate: dateTime(m.registrationDate),
      recipient: to?.targetName ?? '',
      sender: m.senderName ?? '',
      senderName: m.senderName ?? '',
      confidentiality: t(`corr.class.${m.classification}`, { defaultValue: m.classification }),
      priority: t(`corr.priority.${m.priority}`, { defaultValue: m.priority }),
      classification: m.actionType ? t(`corr.action.${m.actionType}`, { defaultValue: m.actionType }) : '',
      fileReference: m.fileNumber ?? '',
      keywords: m.keywords ?? '',
      body: plain(m.body),
    },
    htmlTokens: { body: m.body ?? '' },
    tableRows: rows(m.attachments),
    title: m.mailNumber,
  })
}
