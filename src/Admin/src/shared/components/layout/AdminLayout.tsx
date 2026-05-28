import { useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  Activity,
  FileText,
  LayoutDashboard,
  LogOut,
  Menu,
  MessageSquareWarning,
  LifeBuoy,
  Settings,
  Users,
  X,
} from 'lucide-react'
import { cn } from '@/shared/lib/cn'
import { useAuthStore } from '@/shared/stores/auth-store'
import { Button } from '@/shared/components/ui/Button'
import { LanguageSwitcher } from '@/shared/components/layout/LanguageSwitcher'

const navItems = [
  { to: '/', labelKey: 'nav.dashboard', icon: LayoutDashboard, end: true },
  { to: '/users', labelKey: 'nav.users', icon: Users },
  { to: '/audit-logs', labelKey: 'nav.auditLogs', icon: FileText },
  { to: '/settings', labelKey: 'nav.settings', icon: Settings },
  { to: '/messaging', labelKey: 'nav.dlq', icon: MessageSquareWarning },
  { to: '/support', labelKey: 'nav.support', icon: LifeBuoy },
] as const

export function AdminLayout() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const location = useLocation()
  const user = useAuthStore((s) => s.user)
  const clearSession = useAuthStore((s) => s.clearSession)
  const [sidebarOpen, setSidebarOpen] = useState(false)

  const currentNav = navItems.find((item) => {
    if ('end' in item && item.end) {
      return location.pathname === item.to
    }
    return location.pathname === item.to || location.pathname.startsWith(`${item.to}/`)
  })

  useEffect(() => {
    if (!sidebarOpen) return
    const prev = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = prev
    }
  }, [sidebarOpen])

  const handleLogout = () => {
    clearSession()
    navigate('/login', { replace: true })
  }

  const sidebar = (
    <>
      <div className="flex items-center gap-2 border-b border-[var(--color-border)] px-4 py-5">
        <Activity className="size-6 shrink-0 text-[var(--color-accent)]" />
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-white">{t('nav.brandTitle')}</p>
          <p className="text-xs text-slate-400">{t('nav.brandSubtitle')}</p>
        </div>
        <button
          type="button"
          className="ml-auto rounded-lg p-1.5 text-slate-400 hover:bg-[var(--color-surface-muted)] hover:text-slate-100 lg:hidden"
          onClick={() => setSidebarOpen(false)}
          aria-label={t('common.closeMenu')}
        >
          <X className="size-5" />
        </button>
      </div>
      <nav className="flex flex-1 flex-col gap-1 overflow-y-auto p-3">
        {navItems.map(({ to, labelKey, icon: Icon, ...rest }) => (
          <NavLink
            key={to}
            to={to}
            end={'end' in rest ? rest.end : false}
            onClick={() => setSidebarOpen(false)}
            className={({ isActive }) =>
              cn(
                'flex items-center gap-2 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors',
                isActive
                  ? 'bg-[var(--color-accent)]/20 text-blue-300'
                  : 'text-slate-400 hover:bg-[var(--color-surface-muted)] hover:text-slate-100',
              )
            }
          >
            <Icon className="size-4 shrink-0" />
            {t(labelKey)}
          </NavLink>
        ))}
      </nav>
      <div className="space-y-3 border-t border-[var(--color-border)] p-3">
        <LanguageSwitcher className="px-1" />
        {user ? (
          <div className="px-2">
            <p className="truncate text-sm font-medium text-slate-200">{user.userName}</p>
            <p className="truncate text-xs text-slate-500">{user.email}</p>
          </div>
        ) : null}
        <Button variant="ghost" className="w-full justify-start" onClick={handleLogout}>
          <LogOut className="size-4" />
          {t('nav.signOut')}
        </Button>
      </div>
    </>
  )

  return (
    <div className="flex min-h-screen">
      {sidebarOpen ? (
        <button
          type="button"
          className="fixed inset-0 z-40 bg-black/60 lg:hidden"
          aria-label={t('common.closeMenu')}
          onClick={() => setSidebarOpen(false)}
        />
      ) : null}

      <aside
        className={cn(
          'fixed inset-y-0 left-0 z-50 flex w-64 max-w-[85vw] flex-col border-r border-[var(--color-border)] bg-[var(--color-surface-elevated)] transition-transform duration-200 ease-out lg:static lg:z-auto lg:w-60 lg:max-w-none lg:shrink-0 lg:translate-x-0',
          sidebarOpen ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        {sidebar}
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex items-center gap-3 border-b border-[var(--color-border)] bg-[var(--color-surface-elevated)]/80 px-4 py-3 backdrop-blur-sm">
          <button
            type="button"
            className="rounded-lg p-2 text-slate-300 hover:bg-[var(--color-surface-muted)] lg:hidden"
            onClick={() => setSidebarOpen(true)}
            aria-label={t('common.openMenu')}
          >
            <Menu className="size-5" />
          </button>
          <div className="min-w-0 flex-1 lg:hidden">
            <p className="truncate text-sm font-semibold text-white">
              {currentNav ? t(currentNav.labelKey) : t('common.admin')}
            </p>
            {user ? (
              <p className="truncate text-xs text-slate-500">{user.userName}</p>
            ) : null}
          </div>
          <div className="ml-auto shrink-0">
            <LanguageSwitcher variant="compact" />
          </div>
        </header>
        <main className="flex min-w-0 flex-1 flex-col">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
