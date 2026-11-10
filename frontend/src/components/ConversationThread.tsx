import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useDateFormat } from '../lib/useCalendar'
/** Shape a conversation message must provide (module-agnostic: internal mail today, any thread later). */
export interface ThreadNode {
  id: number
  correspondenceNumber: string
  direction: string
  subject: string
  status: string
  linkType: string
  correspondenceDate: string
  senderName: string | null
  body: string | null
}

const DIR_CLS: Record<string, string> = {
  Incoming: 'corr-dir--in', InternalIncoming: 'corr-dir--in',
  Outgoing: 'corr-dir--out', InternalOutgoing: 'corr-dir--int',
}

/** A Gmail-style conversation: each message in the thread is a collapsible card stacked in date
 * order. The current message and the latest one open by default; the rest collapse to a one-line
 * header (from · direction · date) you can expand. Used for internal correspondence threads. */
export default function ConversationThread({
  nodes, currentId, onOpen,
}: { nodes: ThreadNode[]; currentId: number; onOpen: (id: number) => void }) {
  return (
    <div className="conv">
      {nodes.map((n, i) => (
        <ConvMessage key={n.id} node={n} current={n.id === currentId} last={i === nodes.length - 1} onOpen={onOpen} />
      ))}
    </div>
  )
}

function ConvMessage({
  node, current, last, onOpen,
}: { node: ThreadNode; current: boolean; last: boolean; onOpen: (id: number) => void }) {
  const { t } = useTranslation()
  const { dateTime } = useDateFormat()
  const [open, setOpen] = useState(current || last)
  const who = (node.senderName && node.senderName.trim())
    || t(`corr.dir.${node.direction}`, { defaultValue: node.direction })

  return (
    <div className={`conv__msg ${current ? 'is-current' : ''} ${open ? 'is-open' : ''}`}>
      <button type="button" className="conv__head" onClick={() => setOpen((o) => !o)} aria-expanded={open}>
        <span className="conv__avatar" aria-hidden>{who.charAt(0)}</span>
        <span className="conv__who">{who}</span>
        <span className={`corr-dir ${DIR_CLS[node.direction] ?? ''}`}>{t(`corr.dir.${node.direction}`, { defaultValue: node.direction })}</span>
        {!open && node.body && <span className="conv__snippet">{node.body.replace(/<[^>]+>/g, ' ').trim().slice(0, 90)}</span>}
        <span className="conv__date mono">{dateTime(node.correspondenceDate)}</span>
        <span className="conv__caret" aria-hidden>{open ? '▴' : '▾'}</span>
      </button>
      {open && (
        <div className="conv__body">
          {node.body
            ? <div className="corr__body" dangerouslySetInnerHTML={{ __html: node.body }} />
            : <p className="muted">—</p>}
          <div className="conv__foot">
            <span className="mono muted">{node.correspondenceNumber}</span>
            {!current && <button type="button" className="btn btn-ghost btn-sm" onClick={() => onOpen(node.id)}>{t('corr.open')}</button>}
          </div>
        </div>
      )}
    </div>
  )
}
