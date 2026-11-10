import {
  Palette, UsersRound, PanelLeft, ShieldCheck, Table2,
  SlidersHorizontal, Mail, Flame, Info, BadgeCheck, type LucideIcon,
} from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { auth } from '../../lib/auth'

/**
 * The Settings sections, in render order — the single source of truth for the list and the panels.
 *
 * Every section opens in place. تصاميم الطباعة used to be a route of its own, which meant a click
 * on it navigated away and swapped the whole screen while a click on any other section merely
 * switched a panel — two behaviours behind one identical-looking list. It is a panel now like the
 * rest; its old URLs redirect here.
 */
export interface SettingsSection {
  key: string
  label: string
  Icon: LucideIcon
  show: boolean
}

export function useSettingsSections(): SettingsSection[] {
  const { t } = useTranslation()
  return [
    { key: 'branding', label: 'هوية المؤسسة', Icon: Palette, show: auth.hasPermission('Organization.View') },
    { key: 'groups', label: 'مجموعات المراسلات', Icon: UsersRound, show: auth.hasPermission('Organization.View') },
    { key: 'navbar', label: 'القائمة الجانبية', Icon: PanelLeft, show: auth.hasPermission('Organization.Edit') },
    { key: 'classification', label: t('settings.sections.classification'), Icon: ShieldCheck, show: auth.hasPermission('Classification.View') },
    { key: 'tableColumns', label: 'أعمدة الجداول', Icon: Table2, show: auth.hasPermission('TableColumns.Edit') },
    { key: 'customFields', label: 'الحقول المخصصة', Icon: SlidersHorizontal, show: auth.hasPermission('CustomFields.Edit') },
    { key: 'mailTypes', label: 'تصنيفات البريد', Icon: Mail, show: auth.hasPermission('Organization.Edit') },
    { key: 'destructionMethods', label: 'طرق الإتلاف', Icon: Flame, show: auth.hasPermission('Destruction.Approve') },
    { key: 'license', label: 'الترخيص', Icon: BadgeCheck, show: auth.hasPermission('Settings.View') },
    { key: 'about', label: t('settings.sections.about'), Icon: Info, show: true },
  ].filter((s) => s.show)
}
