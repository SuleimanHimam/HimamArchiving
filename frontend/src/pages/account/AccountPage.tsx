import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { motion } from 'motion/react'
import { ImagePlus, Trash2, KeyRound, ShieldCheck } from 'lucide-react'
import { useCurrentUser, CURRENT_USER_KEY } from '../../lib/useCurrentUser'
import { account, fileToAvatarDataUrl, fileToSealDataUrl, SealKind } from '../../lib/account'
import { roleLabel } from '../../lib/users'
import { groups as groupsApi, type Group } from '../../lib/groups'
import { clearanceLabel, clearanceClass } from '../../lib/shared'
import { isStrongPassword } from '../../lib/passwordStrength'
import { LabeledPassword } from '../../components/ui/labeled-input'
import PasswordStrengthMeter from '../../components/PasswordStrengthMeter'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { useToast } from '../../components/toast'
import '../../styles/forms.css'
import './account.css'

/**
 * Personal settings — the counterpart to /app/settings, which is entirely
 * administrative. Nothing here is permission-gated: every field belongs to the
 * signed-in user and affects nobody else.
 */
export default function AccountPage() {
  const { t } = useTranslation()
  const { data: user } = useCurrentUser()
  const toast = useToast()
  const qc = useQueryClient()
  const { confirm } = useConfirmDialog()

  /* The distribution groups this user belongs to. Worth showing: membership is what decides
     which groups they may address in internal mail, and it is set by an administrator, so
     without this the rule is invisible to the person it applies to. */
  const [myGroups, setMyGroups] = useState<Group[] | null>(null)
  useEffect(() => { groupsApi.list(true, true).then(setMyGroups).catch(() => setMyGroups([])) }, [])
  const fileRef = useRef<HTMLInputElement>(null)
  const signatureRef = useRef<HTMLInputElement>(null)
  const stampRef = useRef<HTMLInputElement>(null)
  const sealRefs = { [SealKind.Signature]: signatureRef, [SealKind.Stamp]: stampRef }

  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [confirmPw, setConfirmPw] = useState('')
  const [busy, setBusy] = useState(false)

  const match = next === confirmPw
  const initial = user?.fullName?.[0] ?? '؟'

  const SEALS = [
    { kind: SealKind.Signature, label: 'التوقيع', url: user?.signatureUrl },
    { kind: SealKind.Stamp, label: 'الختم', url: user?.stampUrl },
  ] as const

  async function pickAvatar(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''   // so re-picking the same file still fires onChange
    if (!file) return
    setBusy(true)
    try {
      await account.setAvatar(await fileToAvatarDataUrl(file))
      await qc.invalidateQueries({ queryKey: CURRENT_USER_KEY })
      toast.success(t('account.avatar.done'))
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } }; message?: string }
      toast.error(ax.response?.data?.error ?? ax.message ?? t('account.avatar.failed'))
    } finally { setBusy(false) }
  }

  async function removeAvatar() {
    if (!(await confirm({ message: t('account.avatar.removeConfirm'), destructive: true }))) return
    setBusy(true)
    try {
      await account.setAvatar(null)
      await qc.invalidateQueries({ queryKey: CURRENT_USER_KEY })
      toast.success(t('account.avatar.removed'))
    } catch { toast.error(t('account.avatar.failed')) }
    finally { setBusy(false) }
  }

  async function pickSeal(kind: SealKind, e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    setBusy(true)
    try {
      await account.setSeal(kind, await fileToSealDataUrl(file))
      await qc.invalidateQueries({ queryKey: CURRENT_USER_KEY })
      toast.success('تم الحفظ')
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } }; message?: string }
      toast.error(ax.response?.data?.error ?? ax.message ?? 'تعذّر حفظ الصورة')
    } finally { setBusy(false) }
  }

  async function removeSeal(kind: SealKind, label: string) {
    if (!(await confirm({ message: `حذف ${label}؟`, destructive: true }))) return
    setBusy(true)
    try {
      await account.setSeal(kind, null)
      await qc.invalidateQueries({ queryKey: CURRENT_USER_KEY })
      toast.success('تم الحذف')
    } catch { toast.error('تعذّر الحذف') }
    finally { setBusy(false) }
  }

  async function submitPassword(e: FormEvent) {
    e.preventDefault()
    if (!current) { toast.error(t('account.pw.currentRequired')); return }
    if (!isStrongPassword(next)) { toast.error(t('account.pw.weak')); return }
    if (!match) { toast.error(t('account.pw.mismatch')); return }
    setBusy(true)
    try {
      await account.changePassword(current, next)
      setCurrent(''); setNext(''); setConfirmPw('')
      toast.success(t('account.pw.done'))
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } } }
      toast.error(ax.response?.data?.error ?? t('account.pw.failed'))
    } finally { setBusy(false) }
  }

  return (
    <div className="acctpage">
      <header className="page__head">
        <div>
          <h1>{t('account.page.title')}</h1>
          <p className="muted">{t('account.page.subtitle')}</p>
        </div>
      </header>

      {/* ── Profile ── */}
      <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
        <h3 className="detail-h3">{t('account.page.profile')}</h3>

        <div className="acctpage__profile">
          <div className="acctpage__avatarwrap">
            <div className="acctpage__avatar">
              {user?.avatarUrl
                ? <img src={user.avatarUrl} alt="" className="acctpage__avatarimg" />
                : <span className="acctpage__avatarinitial">{initial}</span>}
            </div>
            <div className="acctpage__avataracts">
              <button type="button" className="btn btn-ghost btn-sm" disabled={busy}
                onClick={() => fileRef.current?.click()}>
                <ImagePlus size={14} className="ms-1" />
                {user?.avatarUrl ? t('account.avatar.change') : t('account.avatar.add')}
              </button>
              {user?.avatarUrl && (
                <button type="button" className="btn btn-ghost btn-sm text-destructive" disabled={busy}
                  onClick={removeAvatar}>
                  <Trash2 size={14} className="ms-1" />
                  {t('account.avatar.remove')}
                </button>
              )}
              <p className="acctpage__hint">{t('account.avatar.hint')}</p>
            </div>
          </div>

          <dl className="detail-list acctpage__facts">
            <dt>{t('account.page.name')}</dt>
            <dd>{user?.fullName ?? '—'}</dd>
            <dt>{t('account.page.email')}</dt>
            {/* <bdi>, not dir="ltr": the address must render left-to-right, but
                dir on the cell also flips its alignment, throwing the value to
                the far edge away from its label. */}
            <dd className="mono"><bdi>{user?.email ?? '—'}</bdi></dd>
            <dt>{t('account.page.jobTitle')}</dt>
            <dd>{user?.jobTitle || '—'}</dd>
            <dt>{t('account.page.clearance')}</dt>
            <dd>
              <span className={`badge ${clearanceClass(user?.clearance)}`}>{clearanceLabel(user?.clearance)}</span>
            </dd>
            <dt>{t('account.page.roles')}</dt>
            <dd>
              {user?.roles?.length
                ? user.roles.map((r) => <span key={r} className="badge internal acctpage__role">{roleLabel(r)}</span>)
                : '—'}
            </dd>
            <dt>{t('account.page.groups')}</dt>
            <dd>
              {myGroups === null
                ? '…'
                : myGroups.length
                  ? myGroups.map((g) => (
                      <span key={g.id} className="badge public acctpage__role" title={g.description ?? undefined}>{g.name}</span>
                    ))
                  : <span className="muted">{t('account.page.noGroups')}</span>}
            </dd>
          </dl>
        </div>

        {/* These are set by an administrator, not here — saying so beats leaving
            the user hunting for an edit control that does not exist. */}
        <p className="acctpage__note">
          <ShieldCheck size={14} className="ms-1" />
          {t('account.page.adminNote')}
        </p>
      </motion.section>

      {/* ── Signature & stamp ── */}
      <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }}
        animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.03 }}>
        <h3 className="detail-h3">التوقيع والختم</h3>
        <p className="muted">
          تُضاف هذه الصور إلى الكتب التي ترسلها عند طباعتها، إذا كان تصميم الطباعة يحتوي على
          موضع للتوقيع أو الختم. يفضَّل صورة PNG بخلفية شفافة.
        </p>

        <div className="acctpage__seals">
          {SEALS.map(({ kind, label, url }) => (
            <div key={kind} className="acctpage__seal">
              <span className="acctpage__seallabel">{label}</span>
              <div className="acctpage__sealbox">
                {url
                  ? <img src={url} alt={label} className="acctpage__sealimg" />
                  : <span className="acctpage__sealempty">لم يُضف بعد</span>}
              </div>
              <div className="acctpage__avataracts">
                <button type="button" className="btn btn-ghost btn-sm" disabled={busy}
                  onClick={() => sealRefs[kind].current?.click()}>
                  <ImagePlus size={14} className="ms-1" />
                  {url ? 'تغيير' : 'إضافة'}
                </button>
                {url && (
                  <button type="button" className="btn btn-ghost btn-sm text-destructive" disabled={busy}
                    onClick={() => removeSeal(kind, label)}>
                    <Trash2 size={14} className="ms-1" />
                    حذف
                  </button>
                )}
              </div>
              <input ref={sealRefs[kind]} type="file" accept="image/*" style={{ display: 'none' }}
                onChange={(e) => pickSeal(kind, e)} />
            </div>
          ))}
        </div>
      </motion.section>

      {/* ── Password ── */}
      <motion.section id="password" className="doc-card" initial={{ opacity: 0, y: 12 }}
        animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.05 }}>
        <h3 className="detail-h3">{t('account.pw.change')}</h3>
        <p className="muted">{t('account.pw.hint')}</p>

        <form onSubmit={submitPassword} className="acctpage__pwform">
          <LabeledPassword label={t('account.pw.current')} value={current}
            onChange={(e) => setCurrent(e.target.value)} autoComplete="current-password" />
          <LabeledPassword label={t('account.pw.new')} value={next}
            onChange={(e) => setNext(e.target.value)} autoComplete="new-password">
            <PasswordStrengthMeter password={next} />
          </LabeledPassword>
          <LabeledPassword label={t('account.pw.confirm')} value={confirmPw}
            onChange={(e) => setConfirmPw(e.target.value)} autoComplete="new-password">
            {confirmPw.length > 0 && (
              <span className={`pw-match-bar__label ${match ? 'pw-match--ok' : 'pw-match--err'}`}>
                {match ? t('account.pw.matches') : t('account.pw.mismatch')}
              </span>
            )}
          </LabeledPassword>

          <div className="form-actions">
            <button type="submit" className="btn btn-primary" disabled={busy}>
              <KeyRound size={14} className="ms-1" />
              {t('account.pw.change')}
            </button>
          </div>
        </form>
      </motion.section>

      <input ref={fileRef} type="file" accept="image/*" hidden onChange={pickAvatar} />
    </div>
  )
}
