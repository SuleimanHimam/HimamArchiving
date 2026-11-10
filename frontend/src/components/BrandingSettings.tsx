import { useState, useEffect, useRef } from 'react'
import { RotateCcw, X, Landmark, User } from 'lucide-react'
import {
  brandingApi, applyBranding, getBranding, shiftHue,
  BRANDING_DEFAULTS, type BrandingData,
} from '../lib/branding'
import { contrastRatio, grade, isHex, readableOn } from '../lib/color'
import { auth } from '../lib/auth'
import { useToast } from './toast'
import { LabeledInput } from './ui/labeled-input'
import './brandingsettings.css'

/** Colour keys of BrandingData — the only ones the pickers may address. */
type ColorKey = keyof typeof BRANDING_DEFAULTS

const ALL_COLORS = Object.keys(BRANDING_DEFAULTS) as ColorKey[]

/* The design system's own palette (styles/hds.css), offered as presets so a rebrand
   lands on colours that already coexist in the app instead of arbitrary hexes. */
const SYSTEM_PALETTE: { hex: string; name: string }[] = [
  { hex: '#1B4C9E', name: 'أزرق العلامة — اللون الأساسي' },
  { hex: '#2C6FD6', name: 'أزرق فاتح — لمعة العلامة' },
  { hex: '#F5A623', name: 'كهرماني — لمسة العلامة الدافئة' },
  { hex: '#0E2A5C', name: 'أزرق داكن — الشريط الجانبي وشاشة الدخول' },
  { hex: '#2E96CC', name: 'أزرق سماوي — HDS primary' },
  { hex: '#24759F', name: 'أزرق داكن — primary ink' },
  { hex: '#1B5776', name: 'أزرق عميق — primary deep' },
  { hex: '#7B2D6E', name: 'أرجواني — HDS purple' },
  { hex: '#1FA8B8', name: 'تركوازي — HDS teal' },
  { hex: '#177D89', name: 'تركوازي داكن — teal ink' },
  { hex: '#1A1A2E', name: 'حبري — لون الشريط الجانبي' },
  { hex: '#252540', name: 'حبري مرتفع — rail elevated' },
  { hex: '#14213D', name: 'كحلي — اللون الأساسي الكلاسيكي' },
  { hex: '#9B2226', name: 'أحمر الختم — seal' },
  { hex: '#C8A24B', name: 'ذهبي — brass' },
  { hex: '#F4ECD8', name: 'كريمي — نص الشريط الجانبي' },
  { hex: '#FFFFFF', name: 'أبيض' },
  { hex: '#222634', name: 'رمادي حبري — نص النظام' },
]

const GRADE_LABEL: Record<string, string> = {
  AAA: 'AAA — تباين ممتاز',
  AA: 'AA — تباين مقبول',
  'AA-large': 'للنص الكبير فقط',
  fail: 'تباين ضعيف — النص قد لا يُقرأ',
}

/** Raw stored value, or the system default when unset — may be mid-typing and invalid. */
const raw = (form: BrandingData, k: ColorKey) => (form[k] as string | null) ?? BRANDING_DEFAULTS[k]
/** Same, but guaranteed to be a usable hex: what previews, pickers and contrast read. */
const val = (form: BrandingData, k: ColorKey) => {
  const v = raw(form, k)
  return isHex(v) ? v : BRANDING_DEFAULTS[k]
}

// ── One colour row: swatch + hex field + presets + contrast readout ────────────
// Defined at module level, not inside BrandingSettings: a component re-created on
// every render is a new element type each time, so React would unmount and remount
// the inputs and the hex field would lose focus after a single keystroke.
function ColorField({ k, label, hint, cssVar, against, form, canEdit, onSet }: {
  k: ColorKey
  label: string
  hint: string
  /** The CSS custom property this colour drives — shown so the mapping is inspectable. */
  cssVar: string
  /** Key of the colour this one sits on, for the contrast reading. */
  against?: ColorKey
  form: BrandingData
  canEdit: boolean
  onSet: (patch: Partial<BrandingData>) => void
}) {
  const typed = raw(form, k)
  const safe = val(form, k)
  const overridden = form[k] != null
  const ratio = against ? contrastRatio(safe, val(form, against)) : null
  const g = ratio ? grade(ratio) : null
  const put = (v: string | null) => onSet({ [k]: v } as Partial<BrandingData>)

  return (
    <div className="bset-color">
      <div className="bset-color__head">
        <label className="bset-color__label" htmlFor={`bc-${k}`}>{label}</label>
        {overridden && canEdit && (
          <button className="bset-color__reset" title="إعادة إلى قيمة النظام" onClick={() => put(null)}><RotateCcw size={13} aria-hidden /></button>
        )}
      </div>
      <p className="bset-color__hint">{hint}</p>

      <div className="bset-color__row">
        <input
          id={`bc-${k}`}
          type="color"
          className="bset-color__picker"
          value={safe}
          onChange={(e) => put(e.target.value)}
          disabled={!canEdit}
          aria-label={label}
        />
        {/* Typed hex as well as the OS picker: an institution's colours arrive as hex codes
            in a brand manual, and re-finding them by eye in a colour wheel is lossy. */}
        <input
          type="text"
          className={`bset-color__hex mono ${isHex(typed) ? '' : 'is-bad'}`}
          value={typed}
          dir="ltr"
          spellCheck={false}
          maxLength={7}
          onChange={(e) => {
            const v = e.target.value.trim()
            // Emptying the field means "use the system value", not "store an empty colour".
            if (v === '' || v === '#') { put(null); return }
            put(v.startsWith('#') ? v : `#${v}`)
          }}
          disabled={!canEdit}
        />
        <code className="bset-color__var" title="متغيّر CSS الذي يتحكم به هذا اللون">{cssVar}</code>
      </div>

      {ratio && g && (
        <div className={`bset-contrast is-${g}`}>
          <span className="bset-contrast__ratio mono">{ratio.toFixed(2)}:1</span>
          <span>{GRADE_LABEL[g]}</span>
        </div>
      )}

      {canEdit && (
        <div className="bset-presets" role="group" aria-label={`ألوان النظام لـ ${label}`}>
          {SYSTEM_PALETTE.map((sw) => (
            <button
              key={sw.hex}
              type="button"
              className={`bset-presets__sw ${safe.toLowerCase() === sw.hex.toLowerCase() ? 'is-on' : ''}`}
              style={{ background: sw.hex }}
              title={`${sw.name} · ${sw.hex}`}
              onClick={() => put(sw.hex)}
            />
          ))}
        </div>
      )}
    </div>
  )
}

function Section({ title, desc, onReset, children }: {
  title: string
  desc: string
  /** Provided only for sections that own colours. */
  onReset?: () => void
  children: React.ReactNode
}) {
  return (
    <section className="bset-section">
      <header className="bset-section__head">
        <div>
          <h4 className="bset-section__title">{title}</h4>
          <p className="bset-section__desc">{desc}</p>
        </div>
        {onReset && (
          <button className="btn btn-ghost btn-sm" onClick={onReset}><RotateCcw size={14} aria-hidden /> افتراضي هذا القسم</button>
        )}
      </header>
      {children}
    </section>
  )
}

export default function BrandingSettings() {
  const toast   = useToast()
  const canEdit = auth.hasPermission('Organization.Edit')
  const fileRef = useRef<HTMLInputElement>(null)

  const [form, setForm] = useState<BrandingData>(getBranding)
  const [busy, setBusy] = useState(false)
  const [dirty, setDirty] = useState(false)

  useEffect(() => {
    brandingApi.get().then((b) => { setForm(b); setDirty(false) }).catch(() => {})
  }, [])

  function set(patch: Partial<BrandingData>) {
    setForm((f) => ({ ...f, ...patch }))
    setDirty(true)
  }

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
    // A half-typed hex is fine while editing but must not reach the server, which
    // rejects non-hex outright (the values become CSS custom properties).
    const bad = ALL_COLORS.filter((k) => form[k] != null && !isHex(form[k] as string))
    if (bad.length > 0) { toast.error(`قيمة لون غير مكتملة: ${(form[bad[0]] as string) || '—'}`); return }

    setBusy(true)
    try {
      const updated = await brandingApi.update(form)
      applyBranding(updated)
      setForm(updated)
      setDirty(false)
      toast.success('تم حفظ هوية النظام — طُبِّقت على جميع المستخدمين')
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } } }
      toast.error(ax.response?.data?.error ?? 'تعذّر الحفظ')
    } finally { setBusy(false) }
  }

  /** Clears one group of colours back to the system defaults. */
  function resetGroup(keys: ColorKey[]) {
    const patch: Partial<BrandingData> = {}
    for (const k of keys) (patch as Record<string, null>)[k] = null
    set(patch)
  }

  const field = (k: ColorKey, label: string, hint: string, cssVar: string, against?: ColorKey) => (
    <ColorField key={k} k={k} label={label} hint={hint} cssVar={cssVar} against={against}
      form={form} canEdit={canEdit} onSet={set} />
  )

  const railBg = val(form, 'colorRailBg'), railText = val(form, 'colorRailText'), railAccent = val(form, 'colorRailAccent')
  const navBg = val(form, 'colorNavBg'), navText = val(form, 'colorNavText')
  const loginBg = val(form, 'colorLoginBg'), loginAccent = val(form, 'colorLoginAccent')
  const loginAccent2 = shiftHue(loginAccent, 55)
  const navPrimary = val(form, 'colorPrimary')

  return (
    <div className="bset">
      {/* Scrolls on its own so the save bar below stays reachable without scrolling the
          whole settings page — and its section tabs — past the top of the viewport. */}
      <div className="bset__scroll">
        {!canEdit && (
          <p className="bset-readonly">
            العرض فقط — تعديل هوية المؤسسة يتطلّب صلاحية «الهيكل التنظيمي · تعديل».
          </p>
        )}

        {/* ── Identity ── */}
        <Section title="هوية المؤسسة" desc="الشعار والاسم كما يظهران في الشريط العلوي وشاشة الدخول والتقارير المطبوعة.">
          <div className="bset-identity">
            <div className="bset-logo">
              <div className="bset-logo__box" onClick={() => canEdit && fileRef.current?.click()}>
                {form.logoBase64
                  ? <img src={form.logoBase64} alt="" className="bset-logo__img" />
                  : <span className="bset-logo__empty"><Landmark size={22} aria-hidden /></span>}
                {canEdit && <span className="bset-logo__hint">انقر للتغيير</span>}
              </div>
              <input ref={fileRef} type="file" accept="image/*" hidden onChange={handleLogoFile} />
              <p className="bset-logo__meta">PNG أو SVG · حتى 500 كيلوبايت</p>
              {canEdit && form.logoBase64 && (
                <button className="btn btn-ghost btn-sm bset-logo__del" onClick={() => set({ logoBase64: null })}>
                  <X size={15} aria-hidden /> حذف الشعار
                </button>
              )}
            </div>

            <div className="bset-fields">
              <LabeledInput label="اسم المؤسسة (عربي)" required value={form.nameAr}
                onChange={(e) => set({ nameAr: e.target.value })} disabled={!canEdit} />
              <LabeledInput label="Institution Name (English)" dir="ltr" value={form.nameEn ?? ''}
                onChange={(e) => set({ nameEn: e.target.value || null })} disabled={!canEdit} />
              <LabeledInput label="الرمز التعريفي للمؤسسة" dir="ltr" value={form.code ?? ''}
                onChange={(e) => set({ code: e.target.value || null })} disabled={!canEdit} />
            </div>
          </div>
        </Section>

        {/* ── Contact ── */}
        <Section title="معلومات الاتصال" desc="تُطبع في ترويسة التقارير والمراسلات الصادرة.">
          <div className="bset-grid">
            <LabeledInput label="العنوان" value={form.address ?? ''}
              onChange={(e) => set({ address: e.target.value || null })} disabled={!canEdit} />
            <LabeledInput label="رقم الهاتف" dir="ltr" value={form.phone ?? ''}
              onChange={(e) => set({ phone: e.target.value || null })} disabled={!canEdit} />
            <LabeledInput label="البريد الإلكتروني" type="email" dir="ltr" value={form.email ?? ''}
              onChange={(e) => set({ email: e.target.value || null })} disabled={!canEdit} />
          </div>
        </Section>

        {/* ── Sidebar ── */}
        <Section
          title="الشريط الجانبي"
          desc="قائمة التنقّل على حافة الشاشة. الأصل داكن — النص الفاتح عليه هو ما يحقّق التباين، فتحقّق من القراءة أدناه بعد أي تغيير."
          onReset={canEdit ? () => resetGroup(['colorRailBg', 'colorRailText', 'colorRailAccent']) : undefined}
        >
          <div className="bset-split">
            <div className="bset-colors">
              {field('colorRailBg', 'خلفية الشريط', 'الخلفية الكاملة للشريط الجانبي.', '--brand-rail-bg')}
              {field('colorRailText', 'نص الشريط', 'عناوين عناصر التنقّل.', '--brand-rail-text', 'colorRailBg')}
              {field('colorRailAccent', 'لون التمييز', 'الأيقونات والعلامة الرأسية على العنصر النشط.', '--brand-rail-accent', 'colorRailBg')}
            </div>

            {/* Faithful mock of the real rail: same paddings, active marker and 18% tint. */}
            <div className="bset-preview">
              <span className="bset-preview__cap">معاينة</span>
              <div className="bset-mock-rail" style={{ background: railBg, color: railText }}>
                <div className="bset-mock-rail__group"
                  style={{ color: `color-mix(in srgb, ${railText} 62%, transparent)` }}>
                  السجلات والمراسلات
                </div>
                {['الرئيسية', 'الديوان', 'الوثائق'].map((n, i) => (
                  <div
                    key={n}
                    className="bset-mock-rail__item"
                    style={i === 1
                      ? { background: `color-mix(in srgb, ${railAccent} 18%, transparent)`, color: '#fff' }
                      : undefined}
                  >
                    <Landmark size={12} style={{ color: i === 1 ? '#fff' : railAccent }} aria-hidden />
                    <span>{n}</span>
                    {i === 1 && <i className="bset-mock-rail__mark" style={{ background: railAccent }} />}
                  </div>
                ))}
              </div>
            </div>
          </div>
        </Section>

        {/* ── Top bar ── */}
        <Section
          title="الشريط العلوي"
          desc="شريط البحث والإشعارات وحساب المستخدم. الحدود والنص الثانوي وحالات المرور تُشتق من لون النص تلقائيًا، فلا تبقى فاتحة على شريط داكن."
          onReset={canEdit ? () => resetGroup(['colorNavBg', 'colorNavText']) : undefined}
        >
          <div className="bset-split">
            <div className="bset-colors">
              {field('colorNavBg', 'خلفية الشريط العلوي', 'اتركه افتراضيًا ليتبع سطح النظام الفاتح/الداكن.', '--brand-nav-bg')}
              {field('colorNavText', 'نص وأيقونات الشريط', 'يُشتق منه لون الحدود والنص الثانوي وخلفية المرور.', '--brand-nav-text', 'colorNavBg')}
            </div>

            <div className="bset-preview">
              <span className="bset-preview__cap">معاينة</span>
              <div className="bset-mock-nav" style={{
                background: navBg, color: navText,
                borderColor: `color-mix(in srgb, ${navText} 16%, transparent)`,
              }}>
                <span className="bset-mock-nav__logo" style={{ background: navPrimary, color: readableOn(navPrimary) }}><Landmark size={13} aria-hidden /></span>
                <span className="bset-mock-nav__name">{form.nameAr || 'اسم المؤسسة'}</span>
                <span className="bset-mock-nav__search" style={{
                  background: `color-mix(in srgb, ${navText} 9%, transparent)`,
                  borderColor: `color-mix(in srgb, ${navText} 16%, transparent)`,
                  color: `color-mix(in srgb, ${navText} 48%, transparent)`,
                }}>بحث في الوثائق…</span>
                <span className="bset-mock-nav__dot"
                  style={{ borderColor: `color-mix(in srgb, ${navText} 16%, transparent)` }}><User size={11} aria-hidden /></span>
              </div>
            </div>
          </div>
        </Section>

        {/* ── Login ── */}
        <Section
          title="شاشة الدخول"
          desc="أول ما يراه المستخدم. تُقرأ ألوانها قبل تسجيل الدخول، لذلك تُحمَّل من الخادم دون مصادقة."
          onReset={canEdit ? () => resetGroup(['colorLoginBg', 'colorLoginAccent']) : undefined}
        >
          <div className="bset-split">
            <div className="bset-colors">
              {field('colorLoginBg', 'لون الخلفية الأساسي',
                'تُشتق منه تدرّجات الخلفية الأربعة — اختر لونًا داكنًا؛ نص البطاقة أبيض.', '--brand-login-bg')}
              {field('colorLoginAccent', 'لون التمييز',
                'زر الدخول وحلقة التركيز. اللون الثاني في التدرّج يُشتق بإزاحة لونية 55°.', '--brand-login-accent')}
            </div>

            <div className="bset-preview">
              <span className="bset-preview__cap">معاينة</span>
              <div className="bset-mock-login" style={{
                background: `
                  radial-gradient(120% 90% at 50% -10%, color-mix(in srgb, ${loginAccent2} 28%, transparent) 0%, transparent 55%),
                  radial-gradient(90% 70% at 85% 100%, color-mix(in srgb, ${loginAccent} 22%, transparent) 0%, transparent 60%),
                  linear-gradient(160deg,
                    color-mix(in srgb, ${loginBg} 82%, #000) 0%,
                    ${loginBg} 45%,
                    color-mix(in srgb, ${loginBg} 84%, #fff) 72%,
                    color-mix(in srgb, ${loginBg} 82%, #000) 100%)`,
              }}>
                <div className="bset-mock-login__card">
                  <span className="bset-mock-login__logo"
                    style={{ background: `linear-gradient(135deg, ${loginAccent}, ${loginAccent2})` }}><Landmark size={15} aria-hidden /></span>
                  <span className="bset-mock-login__title">{form.nameAr || 'اسم المؤسسة'}</span>
                  <span className="bset-mock-login__input" />
                  <span className="bset-mock-login__input" />
                  <span className="bset-mock-login__btn"
                    style={{ background: `linear-gradient(135deg, ${loginAccent}, ${loginAccent2})` }}>
                    الدخول
                  </span>
                </div>
              </div>
            </div>
          </div>
        </Section>

        {/* ── Brand tokens ── */}
        <Section
          title="ألوان النظام"
          desc="الرموز اللونية التي تقود الأزرار والتنبيهات وخلفية الصفحة في كل الشاشات."
          onReset={canEdit ? () => resetGroup(['colorPrimary', 'colorAccent', 'colorSeal', 'colorBg']) : undefined}
        >
          <div className="bset-colors bset-colors--wide">
            {field('colorPrimary', 'اللون الأساسي', 'الأزرار الرئيسية والعناوين والروابط.', '--ink')}
            {field('colorSeal', 'لون الختم والتنبيهات', 'أزرار الإرسال والختم ورسائل الخطأ والحذف.', '--seal')}
            {field('colorAccent', 'لون التمييز', 'لمسات ثانوية. يبقى تابعًا للنظام في الوضع الداكن حفاظًا على التباين.', '--brass')}
            {field('colorBg', 'خلفية الصفحة', 'خلفية مساحة العمل خلف البطاقات.', '--parchment')}
          </div>
        </Section>

      </div>

      {canEdit && (
        <div className="bset__foot">
          <div className="bset__footnote">
            <span>{dirty ? '● تغييرات غير محفوظة' : 'محفوظ'}</span>
            <span className="bset__foothint">تُطبَّق ألوان المؤسسة على جميع المستخدمين بعد الحفظ.</span>
          </div>
          <div className="bset__footactions">
            <button className="btn btn-ghost btn-sm" onClick={() => resetGroup(ALL_COLORS)}>
              <RotateCcw size={15} aria-hidden /> إعادة كل الألوان
            </button>
            <button className="btn btn-seal" disabled={busy || !dirty} onClick={save}>
              {busy ? 'جارٍ الحفظ...' : 'حفظ الهوية'}
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
