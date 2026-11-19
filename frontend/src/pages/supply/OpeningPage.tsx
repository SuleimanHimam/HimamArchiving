import { useState, type ChangeEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Download, Lock, Upload } from 'lucide-react'
import { auth } from '../../lib/auth'
import { apiError } from '../../lib/hr'
import { day } from '../../lib/hrHooks'
import { supply, type OpeningImportDto } from '../../lib/supply'
import { LabeledInput } from '../../components/ui/labeled-input'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { PageHead } from '../hr/hrShared'
import { DocStatusPill } from './supplyShared'

const STEPS = ['template', 'upload', 'errors', 'document', 'lock'] as const

/** استيراد الأرصدة الافتتاحية (screen 20, §5.3): five steps, may run in stages, then locked for good. */
export default function OpeningPage() {
  const { t } = useTranslation()
  const toast = useToast()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { confirm } = useConfirmDialog()
  const canEdit = auth.hasPermission('SupplyOpening.Edit')
  const { data } = useQuery({ queryKey: ['supply', 'opening'], queryFn: supply.opening })
  const [stage, setStage] = useState('')
  const [last, setLast] = useState<OpeningImportDto | null>(null)
  const [busy, setBusy] = useState(false)

  const step = data?.locked ? 5 : !last ? (stage ? 1 : 0) : last.status === 'Failed' ? 2 : 3
  const refresh = () => qc.invalidateQueries({ queryKey: ['supply'] })

  async function download() {
    const blob = await supply.openingTemplate()
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'opening_balances.csv'
    a.click()
    URL.revokeObjectURL(url)
  }

  async function upload(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    setBusy(true)
    try {
      const r = await supply.uploadOpening({ fileName: file.name, stage, content: await file.text() })
      setLast(r)
      toast[r.status === 'Failed' ? 'error' : 'success'](t(r.status === 'Failed' ? 'sup.open.hasErrors' : 'sup.open.valid'))
      await refresh()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
    finally { setBusy(false) }
  }

  async function lock() {
    if (!await confirm({ message: t('sup.open.confirmLock'), destructive: true })) return
    try {
      await supply.lockOpening()
      toast.success(t('sup.open.locked'))
      await refresh()
    } catch (err) { toast.error(apiError(err, t('hr.common.saveFailed'))) }
  }

  return (
    <div>
      <PageHead title={t('sup.open.title')} />
      <p className="muted" style={{ marginTop: '-.5rem' }}>{t('sup.open.subtitle')}</p>
      <section className="doc-card" style={{ marginBottom: '1rem' }}>
        <ol className="sup-steps">
          {STEPS.map((s, i) => <li key={s} className={i < step ? 'is-done' : i === step ? 'is-current' : ''}>{t(`sup.open.step.${s}`)}</li>)}
        </ol>
        {data?.locked ? (
          <p><Lock size={15} aria-hidden /> {t('sup.open.lockedOn', { date: day(data.lockedOn) })}</p>
        ) : (
          <div style={{ display: 'grid', gap: '.8rem' }}>
            <p style={{ margin: 0 }}>{t('sup.open.help')}</p>
            <div className="row-actions">
              <button className="btn btn-primary" onClick={download}><Download size={15} aria-hidden /> {t('sup.open.download')}</button>
            </div>
            {canEdit && (
              <div className="sup-toolbar">
                <LabeledInput label={t('sup.open.stage')} required value={stage} onChange={(e) => setStage(e.target.value)} containerClassName="grow" />
                <label className={`btn btn-ghost${!stage || busy ? ' is-disabled' : ''}`} aria-disabled={!stage || busy}>
                  <Upload size={15} aria-hidden /> {t('sup.open.upload')}
                  <input type="file" accept=".csv,text/csv" hidden disabled={!stage || busy} onChange={upload} />
                </label>
              </div>
            )}
            {last && last.errors.length > 0 && (
              <div className="table-scroll">
                <table className="reg-table">
                  <thead><tr><th>{t('sup.open.line')}</th><th>{t('sup.open.error')}</th></tr></thead>
                  <tbody>{last.errors.map((e, i) => <tr key={i}><td dir="ltr">{e.line || '—'}</td><td>{e.message}</td></tr>)}</tbody>
                </table>
              </div>
            )}
            {last?.documentId && (
              <p>{t('sup.open.docCreated')} <button className="btn btn-ghost btn-sm" onClick={() => navigate(`/app/supply-documents?open=${last.documentId}`)}>{t('sup.open.openDoc')}</button></p>
            )}
            {canEdit && <div className="row-actions"><button className="btn btn-seal" onClick={lock}><Lock size={15} aria-hidden /> {t('sup.open.lock')}</button></div>}
          </div>
        )}
      </section>
      <section className="doc-card">
        <h3 className="detail-h3" style={{ marginTop: 0 }}>{t('sup.open.history')}</h3>
        <div className="table-scroll">
          <table className="reg-table">
            <thead><tr><th>{t('sup.doc.date')}</th><th>{t('sup.open.stage')}</th><th>{t('sup.open.file')}</th><th className="num">{t('sup.open.rows')}</th><th>{t('sup.open.result')}</th><th>{t('sup.doc.number')}</th></tr></thead>
            <tbody>
              {data?.imports.length === 0 && <tr><td colSpan={6} className="reg-empty">{t('hr.common.empty')}</td></tr>}
              {data?.imports.map((i) => (
                <tr key={i.id} className="reg-row" onClick={() => setLast(i)}>
                  <td dir="ltr">{day(i.createdAt)}</td><td>{i.stage}</td><td dir="ltr">{i.fileName}</td><td className="num">{i.rowCount}</td>
                  <td>{i.status === 'Failed' ? <span className="status-pill s-overdue">{t('sup.open.failed', { count: i.errors.length })}</span>
                    : i.documentStatus ? <DocStatusPill status={i.documentStatus} /> : '—'}</td>
                  <td dir="ltr" className="mono">{i.documentNumber ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  )
}
