import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { SlidersHorizontal, LogOut, ChevronDown } from 'lucide-react'
import {
  DropdownMenu, DropdownMenuTrigger, DropdownMenuContent, DropdownMenuItem,
  DropdownMenuLabel, DropdownMenuSeparator,
} from './ui/dropdown-menu'
import { roleLabel } from '../lib/users'
import type { AuthUser } from '../lib/auth'
import './accountmenu.css'

/**
 * The rail's user chip, turned into the account menu: identity, a link to the
 * account settings page, and sign-out. Everything that changes the account —
 * including the profile picture — lives on /app/account, so there is one
 * implementation rather than a menu copy and a page copy that drift apart.
 */
export default function AccountMenu({ user, onLogout }: {
  user: AuthUser | null | undefined
  onLogout: () => void
}) {
  const { t } = useTranslation()
  const navigate = useNavigate()

  const initial = user?.fullName?.[0] ?? '؟'

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <button type="button" className="acct__trigger" aria-label={t('account.menu')}>
            <span className="acct__avatar">
              {user?.avatarUrl
                ? <img src={user.avatarUrl} alt="" className="acct__avatarimg" />
                : initial}
            </span>
            <span className="acct__who">
              <span className="acct__name">{user?.fullName}</span>
              <span className="acct__role">{roleLabel(user?.roles?.[0])}</span>
            </span>
            <ChevronDown size={14} className="acct__caret" aria-hidden />
          </button>
        </DropdownMenuTrigger>

        <DropdownMenuContent align="start" className="w-60">
          <DropdownMenuLabel className="acct__label">
            <span className="acct__labelname">{user?.fullName}</span>
            <span className="acct__labelmail" dir="ltr">{user?.email}</span>
          </DropdownMenuLabel>
          <DropdownMenuSeparator />

          <DropdownMenuItem onSelect={() => navigate('/app/account')}>
            <SlidersHorizontal className="ms-2 h-4 w-4 opacity-60" />
            {t('account.page.settings')}
          </DropdownMenuItem>

          <DropdownMenuSeparator />

          <DropdownMenuItem onSelect={onLogout} className="text-destructive focus:text-destructive">
            <LogOut className="ms-2 h-4 w-4 opacity-60" />
            {t('nav.logout')}
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </>
  )
}
