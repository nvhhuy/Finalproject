import { Navigate, useLocation } from 'react-router-dom'
import { useAuthStore } from '@/shared/stores/auth-store'

export function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const location = useLocation()
  const accessToken = useAuthStore((s) => s.accessToken)
  const isHydrated = useAuthStore((s) => s.isHydrated)
  const getRefreshToken = useAuthStore((s) => s.getRefreshToken)

  if (!isHydrated) {
    return (
      <div className="flex min-h-screen items-center justify-center text-slate-400">
        Loading…
      </div>
    )
  }

  const hasSession = Boolean(accessToken || getRefreshToken())
  if (!hasSession) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return children
}
