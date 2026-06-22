import { useState, useEffect, useRef } from 'react'
import { brandingApi, applyBranding, getBranding, type BrandingData } from '../lib/branding'
import { auth } from '../lib/auth'
import { useToast } from './toast'
import './brandingsettings.css'

export default function BrandingSettings() {
  const toast   = useToast()
  const canEdit = auth.hasPermission('Organization.Edit')
  const fileRef  = useRef<HTMLInputElement>(null)

  const [form, setForm] = useState<BrandingData>(getBranding)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    brandingApi.get().then((b) => setForm(b)).catch(() => {})
  }, [])

  function set(patch: Partial<BrandingData>) { setForm((f) => ({ ...f, ...patch })) }

  async function handleLogoFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    if (file.size > 500 * 1024) { toast.error('الشعار يجب ألا يتجاوز 500 كيلوبايت'); return }
    const reader = new FileReader()
    reader.onload = () => set({ logoBase64: reader.result as string })
    reader.readAsDataURL(file)
  }

  async function save() {
    if (!form.nameAr.trim()) { toast.error('اسم المؤسسة مطلوب'); return }
    setBusy(true)
    try {
      const updated = await brandingApi.update(form)
      applyBranding(updated)
      setForm(updated)
      toast.success('تم حفظ هوية النظام')
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } } }
      toast.error(ax.response?.data?.error ?? 'تعذّر الحفظ')
    } finally { setBusy(false) }
  }

  function resetColors() {
    set({ colorPrimary: null, colorAccent: null, colorSeal: null, colorBg: null })
  }

  return (
    <div className="branding-wrap">
      <div className="branding-row">
        {/* Logo */}
        <div className="branding-logo-col">
          <p className="branding-label">شعار المؤسسة</p>
          <div className="branding-logo-preview" onClick={() => canEdit && fileRef.current?.click()}>
            {form.logoBase64
              ? <img src={form.logoBase64} alt="logo" className="branding-logo-img" />
              : <span className="branding-logo-empty">◈</span>}
            {canEdit && <span className="branding-logo-hint">انقر للتغيير</span>}
          </div>
          <input ref={fileRef} type="file" accept="image/*" hidden onChange={handleLogoFile} />
          {canEdit && form.logoBase64 && (
            <button className="btn btn-ghost btn-sm branding-remove-logo" onClick={() => set({ logoBase64: null })}>
              ✕ حذف الشعار
            </button>
          )}
        </div>

        {/* Name */}
        <div className="branding-fields">
          <div className="branding-field">
            <label className="branding-label">اسم المؤسسة (عربي) *</label>
            <input
              className="inp"
              value={form.nameAr}
              onChange={(e) => set({ nameAr: e.target.value })}
              disabled={!canEdit}
              placeholder="مؤسسة حِمم"
            />
          </div>
          <div className="branding-field">
            <label className="branding-label">Institution Name (English)</label>
            <input
              className="inp"
              value={form.nameEn ?? ''}
              onChange={(e) => set({ nameEn: e.target.value || null })}
              disabled={!canEdit}
              placeholder="Himam"
              dir="ltr"
            />
          </div>
          <div className="branding-field">
            <label className="branding-label">الرمز التعريفي للمؤسسة</label>
            <input
              className="inp"
              value={form.code ?? ''}
              onChange={(e) => set({ code: e.target.value || null })}
              disabled={!canEdit}
              placeholder="HMM"
              dir="ltr"
            />
          </div>
        </div>
      </div>

      {/* Contact info */}
      <div className="branding-section">
        <div className="branding-section-title">معلومات الاتصال</div>
        <div className="branding-contact-grid">
          <div className="branding-field">
            <label className="branding-label">العنوان</label>
            <input
              className="inp"
              value={form.address ?? ''}
              onChange={(e) => set({ address: e.target.value || null })}
              disabled={!canEdit}
              placeholder="رام الله، فلسطين"
            />
          </div>
          <div className="branding-field">
            <label className="branding-label">رقم الهاتف</label>
            <input
              className="inp"
              value={form.phone ?? ''}
              onChange={(e) => set({ phone: e.target.value || null })}
              disabled={!canEdit}
              placeholder="+970 2 000 0000"
              dir="ltr"
            />
          </div>
          <div className="branding-field">
            <label className="branding-label">البريد الإلكتروني</label>
            <input
              className="inp"
              type="email"
              value={form.email ?? ''}
              onChange={(e) => set({ email: e.target.value || null })}
              disabled={!canEdit}
              placeholder="info@himam.ps"
              dir="ltr"
            />
          </div>
        </div>
      </div>

      {/* Colors */}
      <div className="branding-section">
        <div className="branding-section-title">ألوان النظام</div>
        <div className="branding-colors">
          <div className="branding-color-item">
            <label className="branding-label">اللون الأساسي (خلفية الشريط العلوي)</label>
            <div className="branding-color-row">
              <input
                type="color"
                className="branding-color-picker"
                value={form.colorPrimary ?? '#14213D'}
                onChange={(e) => set({ colorPrimary: e.target.value })}
                disabled={!canEdit}
              />
              <span className="branding-color-hex mono">{form.colorPrimary ?? '#14213D'}</span>
              <div className="branding-color-swatch" style={{ background: form.colorPrimary ?? '#14213D' }} />
            </div>
          </div>
          <div className="branding-color-item">
            <label className="branding-label">لون التمييز (الذهبي)</label>
            <div className="branding-color-row">
              <input
                type="color"
                className="branding-color-picker"
                value={form.colorAccent ?? '#ebe9e6'}
                onChange={(e) => set({ colorAccent: e.target.value })}
                disabled={!canEdit}
              />
              <span className="branding-color-hex mono">{form.colorAccent ?? '#ebe9e6'}</span>
              <div className="branding-color-swatch" style={{ background: form.colorAccent ?? '#ebe9e6' }} />
            </div>
          </div>
          <div className="branding-color-item">
            <label className="branding-label">لون الختم والتنبيهات (الأحمر)</label>
            <div className="branding-color-row">
              <input
                type="color"
                className="branding-color-picker"
                value={form.colorSeal ?? '#9B2226'}
                onChange={(e) => set({ colorSeal: e.target.value })}
                disabled={!canEdit}
              />
              <span className="branding-color-hex mono">{form.colorSeal ?? '#9B2226'}</span>
              <div className="branding-color-swatch" style={{ background: form.colorSeal ?? '#9B2226' }} />
            </div>
          </div>
          <div className="branding-color-item">
            <label className="branding-label">لون خلفية الصفحة</label>
            <div className="branding-color-row">
              <input
                type="color"
                className="branding-color-picker"
                value={form.colorBg ?? '#e6e6e6'}
                onChange={(e) => set({ colorBg: e.target.value })}
                disabled={!canEdit}
              />
              <span className="branding-color-hex mono">{form.colorBg ?? '#e6e6e6'}</span>
              <div className="branding-color-swatch" style={{ background: form.colorBg ?? '#e6e6e6' }} />
            </div>
          </div>
        </div>
        {canEdit && (
          <button className="btn btn-ghost btn-sm" style={{ marginTop: '.5rem' }} onClick={resetColors}>
            ↺ إعادة الألوان الافتراضية
          </button>
        )}
      </div>

      {canEdit && (
        <div className="branding-actions">
          <button className="btn btn-seal" disabled={busy} onClick={save}>
            {busy ? 'جارٍ الحفظ...' : 'حفظ الهوية'}
          </button>
        </div>
      )}
    </div>
  )
}
