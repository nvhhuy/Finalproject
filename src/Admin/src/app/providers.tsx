import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useEffect, type ReactNode } from 'react'
import { Toaster } from '@/shared/components/ui/Toaster'
import { useAuthStore } from '@/shared/stores/auth-store'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
      refetchOnWindowFocus: false,
    },
    mutations: {
      retry: 0,
    },
  },
})

function AuthHydrator({ children }: { children: ReactNode }) {
  const hydrate = useAuthStore((s) => s.hydrate)

  useEffect(() => {
    hydrate()
  }, [hydrate])

  return children
}

export function AppProviders({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthHydrator>
        {children}
        <Toaster />
      </AuthHydrator>
    </QueryClientProvider>
  )
}
