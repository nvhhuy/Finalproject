import { createBrowserRouter, Navigate } from 'react-router-dom'
import { AdminLayout } from '@/shared/components/layout/AdminLayout'
import { ProtectedRoute } from '@/shared/components/layout/ProtectedRoute'
import { LoginPage } from '@/features/auth/LoginPage'
import { DashboardPage } from '@/features/dashboard/DashboardPage'
import { UsersPage } from '@/features/users/UsersPage'
import { UserDetailPage } from '@/features/users/UserDetailPage'
import { SettingsPage } from '@/features/settings/SettingsPage'
import { AuditLogsPage } from '@/features/audit/AuditLogsPage'
import { DlqPage } from '@/features/messaging/DlqPage'

export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    path: '/',
    element: (
      <ProtectedRoute>
        <AdminLayout />
      </ProtectedRoute>
    ),
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'users', element: <UsersPage /> },
      { path: 'users/:userId', element: <UserDetailPage /> },
      { path: 'audit-logs', element: <AuditLogsPage /> },
      { path: 'settings', element: <SettingsPage /> },
      { path: 'messaging', element: <DlqPage /> },
    ],
  },
  { path: '*', element: <Navigate to="/" replace /> },
])
