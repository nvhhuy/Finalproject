import { Suspense, type ReactNode } from 'react'
import { createBrowserRouter, Navigate } from 'react-router-dom'
import { AdminLayout } from '@/shared/components/layout/AdminLayout'
import { ProtectedRoute } from '@/shared/components/layout/ProtectedRoute'
import { RouteFallback } from '@/shared/components/layout/RouteFallback'
import { lazyNamedRoute } from '@/shared/lib/lazy-route'

const LoginPage = lazyNamedRoute(
  () => import('@/features/auth/LoginPage'),
  'LoginPage',
)
const DashboardPage = lazyNamedRoute(
  () => import('@/features/dashboard/DashboardPage'),
  'DashboardPage',
)
const UsersPage = lazyNamedRoute(() => import('@/features/users/UsersPage'), 'UsersPage')
const UserDetailPage = lazyNamedRoute(
  () => import('@/features/users/UserDetailPage'),
  'UserDetailPage',
)
const SettingsPage = lazyNamedRoute(
  () => import('@/features/settings/SettingsPage'),
  'SettingsPage',
)
const AuditLogsPage = lazyNamedRoute(
  () => import('@/features/audit/AuditLogsPage'),
  'AuditLogsPage',
)
const DlqPage = lazyNamedRoute(() => import('@/features/messaging/DlqPage'), 'DlqPage')

function suspense(element: ReactNode) {
  return <Suspense fallback={<RouteFallback />}>{element}</Suspense>
}

export const router = createBrowserRouter([
  {
    path: '/login',
    element: suspense(<LoginPage />),
  },
  {
    path: '/',
    element: (
      <ProtectedRoute>
        <AdminLayout />
      </ProtectedRoute>
    ),
    children: [
      { index: true, element: suspense(<DashboardPage />) },
      { path: 'users', element: suspense(<UsersPage />) },
      { path: 'users/:userId', element: suspense(<UserDetailPage />) },
      { path: 'audit-logs', element: suspense(<AuditLogsPage />) },
      { path: 'settings', element: suspense(<SettingsPage />) },
      { path: 'messaging', element: suspense(<DlqPage />) },
    ],
  },
  { path: '*', element: <Navigate to="/" replace /> },
])
