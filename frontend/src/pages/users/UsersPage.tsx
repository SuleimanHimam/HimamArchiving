import { useTranslation } from 'react-i18next'
import UsersAdmin from '../../components/UsersAdmin'
import RolesAdmin from '../../components/RolesAdmin'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../components/ui/tabs'
import { auth } from '../../lib/auth'
import '../settings/settings.css'

/** إدارة المستخدمين — all user administration in one place: accounts and roles/permissions.
 * Moved out of Settings into its own top-level section reachable from the sidebar.
 *
 * Uses the shared shadcn/Radix <Tabs> — the same morphing-pill control the
 * الأقسام page uses — rather than a page-local strip, so the two admin screens
 * stay identical and the ARIA roles, roving tabindex and RTL-aware arrow keys
 * come from Radix instead of being reimplemented here. */
export default function UsersPage() {
  const { t, i18n } = useTranslation()
  const ar = i18n.language.startsWith('ar')

  /* Roles and their permissions are institution-wide: editing one changes what every department
     can do, so the server refuses it for anyone but the System Administrator. A department head
     holds Users.Edit — that is what lets them staff their own department — and would otherwise be
     shown a matrix where every switch they flick is rejected on save. */
  const canManageRoles = auth.isSystemAdmin()

  return (
    <div>
      <header className="page__head">
        <div>
          {/* Reads the nav key rather than repeating the words, so the sidebar entry and
              the page title cannot drift apart when either is renamed. */}
          <h1>{t('nav.users')}</h1>
        </div>
      </header>

      {/* dir drives which way Radix's arrow keys travel, so it follows the
          active language rather than being pinned to rtl. */}
      <Tabs defaultValue="users" dir={ar ? 'rtl' : 'ltr'}>
        <TabsList>
          <TabsTrigger value="users">{t('settings.sections.users')}</TabsTrigger>
          {canManageRoles && <TabsTrigger value="roles">{t('settings.sections.roles')}</TabsTrigger>}
        </TabsList>
        <TabsContent value="users">
          <UsersAdmin />
        </TabsContent>
        {canManageRoles && (
          <TabsContent value="roles">
            <RolesAdmin />
          </TabsContent>
        )}
      </Tabs>
    </div>
  )
}
