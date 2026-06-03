import { useCallback, useEffect, useMemo, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  Activity,
  Building2,
  ChevronDown,
  FileText,
  LayoutDashboard,
  LogOut,
  Mail,
  Menu,
  MessageSquareWarning,
  LifeBuoy,
  Coins,
  CreditCard,
  Receipt,
  Settings,
  Users,
  Bell,
  X,
  type LucideIcon,
} from 'lucide-react'
import { cn } from '@/shared/lib/cn'
import { useAuthStore } from '@/shared/stores/auth-store'
import { Button } from '@/shared/components/ui/Button'
import { LanguageSwitcher } from '@/shared/components/layout/LanguageSwitcher'

type NavItem = {
  to: string
  labelKey: string
  icon: LucideIcon
  end?: boolean
}

type NavGroup = {
  labelKey: string
  items: NavItem[]
}

const NAV_COLLAPSED_STORAGE_KEY = 'admin-nav-collapsed'

const navGroups: NavGroup[] = [
  {
    labelKey: 'nav.group.overview',
    items: [{ to: '/', labelKey: 'nav.dashboard', icon: LayoutDashboard, end: true }],
  },
  {
    labelKey: 'nav.group.users',
    items: [
      { to: '/users', labelKey: 'nav.users', icon: Users },
      { to: '/tenants', labelKey: 'nav.tenants', icon: Building2 },
      { to: '/notifications', labelKey: 'nav.notifications', icon: Bell },
      { to: '/contact-messages', labelKey: 'nav.contactMessages', icon: Mail },
      { to: '/support', labelKey: 'nav.support', icon: LifeBuoy },
    ],
  },
  {
    labelKey: 'nav.group.billing',
    items: [
      { to: '/plans', labelKey: 'nav.plans', icon: CreditCard },
      { to: '/credit-packages', labelKey: 'nav.creditPackages', icon: Coins },
      { to: '/invoices', labelKey: 'nav.invoices', icon: Receipt },
    ],
  },
  {
    labelKey: 'nav.group.platform',
    items: [
      { to: '/audit-logs', labelKey: 'nav.auditLogs', icon: FileText },
      { to: '/settings', labelKey: 'nav.settings', icon: Settings },
      { to: '/messaging', labelKey: 'nav.dlq', icon: MessageSquareWarning },
    ],
  },
]

const allNavItems = navGroups.flatMap((g) => g.items)

function isNavItemActive(pathname: string, item: NavItem): boolean {
  if (item.end) {
    return pathname === item.to
  }
  return pathname === item.to || pathname.startsWith(`${item.to}/`)
}

function readCollapsedState(): Record<string, boolean> {
  try {
    const raw = localStorage.getItem(NAV_COLLAPSED_STORAGE_KEY)
    if (!raw) return {}
    const parsed = JSON.parse(raw) as unknown
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return parsed as Record<string, boolean>
    }
  } catch {
    /* ignore */
  }
  return {}
}

function groupHasActiveItem(pathname: string, group: NavGroup): boolean {
  return group.items.some((item) => isNavItemActive(pathname, item))
}

export function AdminLayout() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const location = useLocation()
  const user = useAuthStore((s) => s.user)
  const clearSession = useAuthStore((s) => s.clearSession)
  const [sidebarOpen, setSidebarOpen] = useState(false)
  const [collapsedGroups, setCollapsedGroups] = useState<Record<string, boolean>>(readCollapsedState)

  const persistCollapsed = useCallback((next: Record<string, boolean>) => {
    setCollapsedGroups(next)
    try {
      localStorage.setItem(NAV_COLLAPSED_STORAGE_KEY, JSON.stringify(next))
    } catch {
      /* ignore */
    }
  }, [])

  const toggleGroup = useCallback(
    (labelKey: string) => {
      persistCollapsed({
        ...collapsedGroups,
        [labelKey]: !collapsedGroups[labelKey],
      })
    },
    [collapsedGroups, persistCollapsed],
  )

  const effectiveCollapsedGroups = useMemo(() => {
    const next = { ...collapsedGroups }
    for (const group of navGroups) {
      if (group.items.length > 1 && groupHasActiveItem(location.pathname, group)) {
        next[group.labelKey] = false
      }
    }
    return next
  }, [collapsedGroups, location.pathname])

  const currentNav = allNavItems.find((item) => isNavItemActive(location.pathname, item))

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
      <nav className="flex flex-1 flex-col gap-3 overflow-y-auto p-3">
        {navGroups.map((group) => {
          const collapsible = group.items.length > 1
          const isCollapsed = collapsible && Boolean(effectiveCollapsedGroups[group.labelKey])
          const groupActive = groupHasActiveItem(location.pathname, group)

          return (
            <div key={group.labelKey}>
              {collapsible ? (
                <button
                  type="button"
                  onClick={() => toggleGroup(group.labelKey)}
                  aria-expanded={!isCollapsed}
                  className={cn(
                    'mb-0.5 flex w-full items-center gap-1 rounded-lg px-3 py-1.5 text-left text-[10px] font-semibold uppercase tracking-wider transition-colors',
                    groupActive ? 'text-slate-300' : 'text-slate-500 hover:text-slate-300',
                  )}
                >
                  <span className="min-w-0 flex-1 truncate">{t(group.labelKey)}</span>
                  <ChevronDown
                    className={cn(
                      'size-3.5 shrink-0 transition-transform',
                      isCollapsed && '-rotate-90',
                    )}
                    aria-hidden
                  />
                  <span className="sr-only">
                    {isCollapsed ? t('nav.expandGroup') : t('nav.collapseGroup')}
                  </span>
                </button>
              ) : (
                <p className="mb-1 px-3 text-[10px] font-semibold uppercase tracking-wider text-slate-500">
                  {t(group.labelKey)}
                </p>
              )}
              {!isCollapsed ? (
                <div className="flex flex-col gap-0.5">
                  {group.items.map(({ to, labelKey, icon: Icon, end }) => (
                    <NavLink
                      key={to}
                      to={to}
                      end={end ?? false}
                      onClick={() => setSidebarOpen(false)}
                      className={({ isActive }) =>
                        cn(
                          'flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
                          isActive
                            ? 'bg-[var(--color-accent)]/20 text-blue-300'
                            : 'text-slate-400 hover:bg-[var(--color-surface-muted)] hover:text-slate-100',
                        )
                      }
                    >
                      <Icon className="size-4 shrink-0" />
                      <span className="truncate">{t(labelKey)}</span>
                    </NavLink>
                  ))}
                </div>
              ) : null}
            </div>
          )
        })}
      </nav>
      <div className="space-y-3 border-t border-[var(--color-border)] p-3">
        <LanguageSwitcher className="px-1" />
        {user ? (
          <div className="px-2">
            <p className="truncate text-sm font-medium text-slate-200" title={user.userName}>
              {user.userName}
            </p>
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
    <div className="flex h-dvh overflow-hidden">
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
          'fixed inset-y-0 left-0 z-50 flex h-dvh w-64 max-w-[85vw] flex-col border-r border-[var(--color-border)] bg-[var(--color-surface-elevated)] transition-transform duration-200 ease-out lg:static lg:z-auto lg:h-dvh lg:w-60 lg:max-w-none lg:shrink-0 lg:translate-x-0',
          sidebarOpen ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        {sidebar}
      </aside>

      <div className="flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
        <header className="flex shrink-0 items-center gap-3 border-b border-[var(--color-border)] bg-[var(--color-surface-elevated)]/80 px-4 py-3 backdrop-blur-sm">
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
        <main className="min-h-0 flex-1 overflow-y-auto">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
