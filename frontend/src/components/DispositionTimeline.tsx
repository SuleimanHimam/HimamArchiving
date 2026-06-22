import { DISPOSITION_STATUS } from '../lib/disposition'

/** Horizontal stepper: Verification → Final Approval → Done. Highlights the current stage,
 * and turns red if the request was rejected. */
export default function DispositionTimeline({ status }: { status: number }) {
  const rejected = status === DISPOSITION_STATUS.Rejected
  // step index reached: 0 verification, 1 final approval, 2 done
  const reached =
    status === DISPOSITION_STATUS.PendingVerification ? 0
    : status === DISPOSITION_STATUS.PendingFinalApproval ? 1
    : 2

  const steps = ['التحقق', 'الموافقة النهائية', 'الإنجاز']

  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 0, width: '100%' }}>
      {steps.map((label, i) => {
        const done = !rejected && i < reached
        const active = !rejected && i === reached
        const color = rejected ? '#9B2226' : done ? '#2F5D3A' : active ? '#B0892D' : '#cfc6b4'
        return (
          <div key={label} style={{ display: 'flex', alignItems: 'center', flex: i < steps.length - 1 ? 1 : '0 0 auto' }}>
            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4 }}>
              <span style={{
                width: 28, height: 28, borderRadius: '50%', background: color, color: '#fff',
                display: 'grid', placeItems: 'center', fontSize: '.8rem', fontWeight: 700,
              }}>{rejected && i === reached ? '✕' : done ? '✓' : i + 1}</span>
              <span style={{ fontSize: '.72rem', color: active || done ? 'var(--ink-text,#211d17)' : '#8a8068', whiteSpace: 'nowrap' }}>{label}</span>
            </div>
            {i < steps.length - 1 && (
              <span style={{ flex: 1, height: 3, margin: '0 6px', marginBottom: 18, background: !rejected && i < reached ? '#2F5D3A' : '#e4ddca' }} />
            )}
          </div>
        )
      })}
    </div>
  )
}
