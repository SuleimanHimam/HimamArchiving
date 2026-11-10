import { motion } from 'motion/react'
import { useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { auth } from '../../lib/auth'
import { useDateFormat } from '../../lib/useCalendar'
import { internalMail, CLASS_CLS } from '../../lib/internalMail'
import { canPrintMail, printInternalMail, askPrintMode, mailHasAttachments } from '../../lib/mailPrint'
import { closePreviewWindow } from '../../lib/reportTemplates'
import { useScreenGuard, isSensitive } from '../../lib/useScreenGuard'
import { Trash2, Archive, Printer, Forward, Reply, ArrowRight } from 'lucide-react'
import ConversationThread from '../../components/ConversationThread'
import WorkflowPanel from '../../components/WorkflowPanel'
import MailAttachmentsPanel from '../../components/MailAttachmentsPanel'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import '../../styles/forms.css'
import '../../styles/mail.css'

/** Full-page view of an internal message: body, recipients, attachments, per-user read matrix, and the
 * Gmail-style conversation of its replies. */
export default function InternalDetailPage() {
  const { t } = useTranslation()
  const { dateTime } = useDateFormat()
  const navigate = useNavigate()
  const toast = useToast()
  const { confirm, choose } = useConfirmDialog()
  const id = Number(useParams().id)

  const { data: item, isLoading, refetch } = useQuery({
    queryKey: ['internal-mail', 'item', id],
    queryFn: () => internalMail.get(id),
    enabled: Number.isFinite(id),
  })
  const { data: reads } = useQuery({
    queryKey: ['internal-mail', 'reads', id],
    queryFn: () => internalMail.readStatus(id),
    enabled: Number.isFinite(id),
  })
  const { data: thread } = useQuery({
    queryKey: ['internal-mail', 'thread', id],
    queryFn: () => internalMail.thread(id),
    enabled: Number.isFinite(id),
  })

  const canArchive = auth.hasPermission('Correspondence.Archive')
  const canDelete = auth.hasPermission('Correspondence.Delete')

  // Mark read on first view.
  useEffect(() => { if (Number.isFinite(id)) internalMail.markRead(id).catch(() => { }) }, [id])

  async function doArchive() {
    try { await internalMail.archive(id); await refetch(); toast.success(t('common.saved')) }
    catch { toast.error(t('corr.errors.saveFailed')) }
  }
  async function doDelete() {
    if (!(await confirm({ message: t('corr.confirmDelete'), destructive: true }))) return
    try { await internalMail.remove(id); toast.success(t('common.saved')); navigate('/app/internal') }
    catch { toast.error(t('corr.errors.saveFailed')) }
  }
  async function doPrint() {
    if (!item) return
    const mode = await askPrintMode(choose, mailHasAttachments(item))
    if (!mode) return
    try { await printInternalMail(item, t, dateTime, mode) }
    catch { toast.error(t('corr.errors.printFailed')) }
  }

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
          <span className="mono">{item.mailNumber}</span>
          <h1>{item.subject}</h1>
        </div>
        <div className="page__headactions">
          {canDelete && <button className="btn btn-seal" onClick={doDelete}><Trash2 size={16} aria-hidden /> {t('corr.delete')}</button>}
          {canArchive && (
            <button className="btn btn-ghost" onClick={doArchive} disabled={item.isArchived}>
              <Archive size={16} aria-hidden /> {item.isArchived ? t('corr.status.Archived') : t('corr.archive')}
            </button>
          )}
          {canPrintMail() && <button className="btn btn-ghost" onClick={doPrint}><Printer size={16} aria-hidden /> {t('corr.print')}</button>}
          <button className="btn btn-ghost" onClick={() => navigate(`/app/internal/compose?forward=${id}`)}><Forward size={16} aria-hidden /> {t('corr.forward')}</button>
          <button className="btn btn-ghost" onClick={() => navigate(`/app/internal/compose?replyTo=${id}&all=1`)}>{t('corr.replyAll')}</button>
          <button className="btn btn-primary" onClick={() => navigate(`/app/internal/compose?replyTo=${id}`)}><Reply size={16} aria-hidden /> {t('corr.reply')}</button>
          <button className="btn btn-ghost" onClick={() => navigate('/app/internal')}><ArrowRight size={16} className="icon-dir" aria-hidden /> {t('common.back')}</button>
        </div>
      </header>

      <div className="detail-grid">
        <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
          <div className="detail-badges">
            <span className={`badge ${overdue || item.status === 'Overdue' ? 'secret' : 'internal'}`}>{t(`corr.status.${item.status}`, { defaultValue: item.status })}</span>
            <span className={`badge ${CLASS_CLS[item.classification] ?? 'internal'}`}>{t(`corr.class.${item.classification}`, { defaultValue: item.classification })}</span>
            <span className="badge">{t(`corr.priority.${item.priority}`, { defaultValue: item.priority })}</span>
            {item.actionType && <span className="badge confidential">{t(`corr.action.${item.actionType}`, { defaultValue: item.actionType })}</span>}
            {item.categoryName && <span className="badge">{item.categoryName}</span>}
          </div>
          <dl className="detail-list">
            <dt>{t('corr.from')}</dt><dd>{item.senderName ?? '—'}</dd>
            <dt>{t('corr.date')}</dt><dd className="mono">{dateTime(item.mailDate)}</dd>
            {item.referenceNumber && (<><dt>{t('corr.reference')}</dt><dd className="mono">{item.referenceNumber}</dd></>)}
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
            added here after the message was sent — compose can only stage them in memory. */}
        <MailAttachmentsPanel
          attachments={item.attachments}
          onUpload={(f) => internalMail.addAttachment(id, f)}
          onChanged={refetch}
          canEdit={!item.isArchived}
        />
      </div>

      <div className="corr-detail__grid" style={{ marginBlockStart: '1.4rem' }}>
        <main className="corr-detail__main">
          {/* Renders nothing unless this message belongs to a run. Placed above the body because a
              request waiting on you is the reason you opened the message. */}
          <WorkflowPanel mailId={id} />

          <article className="corr-detail__card">
            {item.body ? <div className="corr__body" dangerouslySetInnerHTML={{ __html: item.body }} /> : <p className="muted">{t('corr.noBody')}</p>}
          </article>

          {item.recipients.length > 0 && (
            <section className="corr-detail__card">
              <h3 className="corr-detail__h3">{t('corr.recipients')}</h3>
              <ul className="corr__recipients">
                {item.recipients.map((r) => (
                  <li key={r.id}>
                    <span className="badge">{t(`corr.addressing.${r.addressing}`, { defaultValue: r.addressing })}</span>{' '}
                    {r.targetName ?? `${r.targetKind} #${r.targetId}`}
                  </li>
                ))}
              </ul>
            </section>
          )}

          {thread && thread.length > 1 && (
            <section className="corr-detail__card">
              <h3 className="corr-detail__h3">
                {t('corr.conversation')} <span className="corr-detail__count">{thread.length}</span>
              </h3>
              <ConversationThread
                nodes={thread.map((n) => ({
                  id: n.id, correspondenceNumber: n.mailNumber, direction: 'InternalOutgoing',
                  subject: n.subject, status: n.status, linkType: n.linkType,
                  correspondenceDate: n.mailDate, senderName: n.senderName, body: n.body,
                }))}
                currentId={id}
                onOpen={(nid) => navigate(`/app/internal/${nid}`)}
              />
            </section>
          )}
        </main>

        {/* The metadata card moved up beside the attachments, so the side rail carries
            only what is specific to an internal message: who has read it. */}
        <aside className="corr-detail__side">
          {reads && reads.length > 0 && (
            <section className="corr-detail__card">
              <h3 className="corr-detail__h3">{t('corr.readStatus')}</h3>
              <table className="corr__reads">
                <thead><tr><th>{t('corr.user')}</th><th>{t('corr.readState')}</th><th>{t('corr.readAt')}</th></tr></thead>
                <tbody>
                  {reads.map((r) => (
                    <tr key={r.userId}>
                      <td>{r.userName ?? `#${r.userId}`}</td>
                      <td><span className={`badge ${r.readStatus === 'Read' ? 'public' : 'internal'}`}>{t(`corr.read.${r.readStatus}`, { defaultValue: r.readStatus })}</span></td>
                      <td className="mono">{r.lastReadAt ? dateTime(r.lastReadAt) : '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </section>
          )}
        </aside>
      </div>
    </div>
  )
}
