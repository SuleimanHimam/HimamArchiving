import { useEffect } from 'react'
import { motion } from 'motion/react'
import { useParams, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { auth } from '../../lib/auth'
import { useDateFormat } from '../../lib/useCalendar'
import { diwan } from '../../lib/diwan'
import { CLASS_CLS } from '../../lib/internalMail'
import { canPrintMail, failMailPreview, printDiwanMail, previewDiwanMail } from '../../lib/mailPrint'
import { closePreviewWindow } from '../../lib/reportTemplates'
import { useScreenGuard, isSensitive } from '../../lib/useScreenGuard'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { Trash2, Archive, Eye, Printer, Reply, ArrowRight, ExternalLink } from 'lucide-react'
import ConversationThread from '../../components/ConversationThread'
import MailAttachmentsPanel from '../../components/MailAttachmentsPanel'
import '../../styles/forms.css'
import '../../styles/mail.css'

/** Full-page view of a single Diwan register entry (external letter). */
export default function DiwanDetailPage() {
  const { t } = useTranslation()
  const { dateTime } = useDateFormat()
  const navigate = useNavigate()
  const toast = useToast()
  const { confirm } = useConfirmDialog()
  const id = Number(useParams().id)

  const { data: item, isLoading, refetch } = useQuery({
    queryKey: ['diwan', 'item', id],
    queryFn: () => diwan.get(id),
    enabled: Number.isFinite(id),
  })

  // The register chain: this entry, its replies, and anything citing it as مرجع.
  const { data: thread } = useQuery({
    queryKey: ['diwan', 'thread', id],
    queryFn: () => diwan.thread(id),
    enabled: Number.isFinite(id),
  })

  const canArchive = auth.hasPermission('Correspondence.Archive')
  const canDelete = auth.hasPermission('Correspondence.Delete')
  const canCreate = auth.hasPermission('Correspondence.Create')

  async function doArchive() {
    try { await diwan.archive(id); await refetch(); toast.success(t('common.saved')) }
    catch { toast.error(t('corr.errors.saveFailed')) }
  }
  async function doDelete() {
    if (!(await confirm({ message: t('corr.confirmDelete'), destructive: true }))) return
    try { await diwan.remove(id); toast.success(t('common.saved')); navigate('/app/diwan') }
    catch { toast.error(t('corr.errors.saveFailed')) }
  }
  async function doPrint() {
    if (!item) return
    try { await printDiwanMail(item, t, dateTime) }
    catch { toast.error(t('corr.errors.printFailed')) }
  }
  async function doPreview() {
    if (!item) return
    try { await previewDiwanMail(item, t, dateTime) }
    catch {
      // The preview window is already open and focused, so the message has to go there too —
      // a toast behind it is the one place the user is not looking.
      failMailPreview(t('corr.errors.previewFailed'))
      toast.error(t('corr.errors.previewFailed'))
    }
  }

  // سري / سري للغاية letters get the capture guard.
  useScreenGuard(isSensitive(item?.classification), {
    onViolation: () => toast.error(t('corr.captureBlocked')),
  })
  useEffect(() => closePreviewWindow, [])

  if (isLoading || !item) {
    return (
      <div className="corr-detail">
        <p className="corr__empty">{t('common.loading')}</p>
      </div>
    )
  }

  const overdue = !!item.responseDeadline && new Date(item.responseDeadline) < new Date() &&
    item.status !== 'Replied' && item.status !== 'Closed' && item.status !== 'Archived'

  return (
    <div>
      {/* Same shell as the document detail page: number + title, then one action bar. */}
      <header className="page__head">
        <div>
          <span className="mono">{item.registerNumber}</span>
          <h1>{item.subject}</h1>
        </div>
        <div className="page__headactions">
          {canDelete && <button className="btn btn-seal" onClick={doDelete}><Trash2 size={16} aria-hidden /> {t('corr.delete')}</button>}
          {canArchive && (
            <button className="btn btn-ghost" onClick={doArchive} disabled={item.isArchived}>
              <Archive size={16} aria-hidden /> {item.isArchived ? t('corr.status.Archived') : t('corr.archive')}
            </button>
          )}
          {/* Printing shows the letter in the platform's own dialog, so a second معاينة button
              beside it would only open the same document twice. */}
          {!canPrintMail()
            ? <button className="btn btn-ghost" onClick={doPreview}><Eye size={16} aria-hidden /> {t('corr.preview')}</button>
            : <button className="btn btn-ghost" onClick={doPrint}><Printer size={16} aria-hidden /> {t('corr.print')}</button>}
          {canCreate && (
            <button className="btn btn-primary" onClick={() => navigate(`/app/diwan/compose?replyTo=${id}`)}>
              <Reply size={16} aria-hidden /> {t('corr.reply')}
            </button>
          )}
          <button className="btn btn-ghost" onClick={() => navigate('/app/diwan')}><ArrowRight size={16} className="icon-dir" aria-hidden /> {t('common.back')}</button>
        </div>
      </header>

      <div className="detail-grid">
        <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
          <div className="detail-badges">
            <span className={`badge ${overdue || item.status === 'Overdue' ? 'secret' : 'internal'}`}>{t(`corr.status.${item.status}`, { defaultValue: item.status })}</span>
            <span className={`badge ${CLASS_CLS[item.classification] ?? 'internal'}`}>{t(`corr.class.${item.classification}`, { defaultValue: item.classification })}</span>
            <span className="badge">{t(`corr.priority.${item.priority}`, { defaultValue: item.priority })}</span>
            <span className="corr-dir">{t(`corr.dir.${item.direction}`, { defaultValue: item.direction })}</span>
            {item.actionType && <span className="badge confidential">{t(`corr.action.${item.actionType}`, { defaultValue: item.actionType })}</span>}
          </div>
          <dl className="detail-list">
            <dt>{t('diwan.externalParty')}</dt>
            <dd>{item.externalOrgId
              ? <button className="corr__thread-link" onClick={() => navigate('/app/external')} title={t('diwan.linkedToDirectory')}>{item.externalOrgName} <ExternalLink size={13} aria-hidden /></button>
              : (item.externalOrgName ?? '—')}</dd>
            <dt>{t('corr.date')}</dt><dd className="mono">{dateTime(item.mailDate)}</dd>
            {item.referenceNumber && (<><dt>{t('corr.reference')}</dt><dd className="mono">{item.referenceNumber}</dd></>)}
            {item.addresseeName && (<><dt>{t('corr.addresseeName')}</dt><dd>{item.addresseeName}{item.addresseeTitle ? ` — ${item.addresseeTitle}` : ''}</dd></>)}
            {item.toDiwanOrgUnits.length > 0 && (
              <><dt>{t('diwan.toDiwan')}</dt><dd>{item.toDiwanOrgUnits.map((u) => u.name).join('، ')}</dd></>
            )}
            {item.dueDate && (<><dt>{t('corr.dueDate')}</dt><dd className="mono">{dateTime(item.dueDate)}</dd></>)}
            {item.responseDeadline && (
              <><dt>{t('corr.responseDeadline')}</dt>
                <dd className={`mono ${overdue ? 'corr__od' : ''}`}>
                  {dateTime(item.responseDeadline)}{item.remindOnNoReply ? ` · ${t('corr.remindOn')}` : ''}
                </dd></>
            )}
            <dt>{t('corr.keywords')}</dt><dd>{item.keywords || '—'}</dd>
            {item.archivedDocumentId && (
              <><dt>{t('corr.archive')}</dt>
                <dd><button className="corr__thread-link" onClick={() => navigate(`/app/documents/${item.archivedDocumentId}`)}>{t('diwan.viewArchivedDoc')}</button></dd></>
            )}
          </dl>
        </motion.section>

        {/* Attachments sit beside the metadata, as on the document page. Files can still be
            added here after the entry was registered — a scan often arrives later. */}
        <MailAttachmentsPanel
          attachments={item.attachments}
          onUpload={(f) => diwan.addAttachment(id, f)}
          onChanged={refetch}
          canEdit={!item.isArchived}
        />
      </div>

      <div className="corr-detail__grid" style={{ marginBlockStart: '1.4rem' }}>
        <main className="corr-detail__main">
          <article className="corr-detail__card">
            {item.body ? <div className="corr__body" dangerouslySetInnerHTML={{ __html: item.body }} /> : <p className="muted">{t('corr.noBody')}</p>}
          </article>

          {thread && thread.length > 1 && (
            <section className="corr-detail__card">
              <h3 className="corr-detail__h3">
                {t('corr.conversation')} <span className="corr-detail__count">{thread.length}</span>
              </h3>
              <ConversationThread
                nodes={thread.map((n) => ({
                  id: n.id, correspondenceNumber: n.registerNumber, direction: n.direction,
                  subject: n.subject, status: n.status, linkType: n.relation,
                  correspondenceDate: n.mailDate, senderName: n.externalOrgName, body: n.body,
                }))}
                currentId={id}
                onOpen={(nid) => navigate(`/app/diwan/${nid}`)}
              />
            </section>
          )}
        </main>
      </div>
    </div>
  )
}
