import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Shield } from 'lucide-react'
import { login, completeTwoFactorLogin } from '@/shared/api/auth-api'
import { useAuthStore } from '@/shared/stores/auth-store'
import { ApiClientError, formatValidationErrors } from '@/shared/lib/errors'
import { Card, CardBody, CardHeader } from '@/shared/components/ui/Card'
import { Input } from '@/shared/components/ui/Input'
import { Button } from '@/shared/components/ui/Button'
import { Alert } from '@/shared/components/ui/Alert'
import { LanguageSwitcher } from '@/shared/components/layout/LanguageSwitcher'
import { TurnstileWidget } from '@/shared/components/ui/TurnstileWidget'
import { createLoginSchemas, firstZodError } from '@/shared/lib'

export function LoginPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const location = useLocation()
  const setSession = useAuthStore((s) => s.setSession)
  const accessToken = useAuthStore((s) => s.accessToken)

  const schemas = useMemo(() => createLoginSchemas(t), [t])

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [pendingLoginId, setPendingLoginId] = useState<string | null>(null)
  const [twoFaCode, setTwoFaCode] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [turnstileToken, setTurnstileToken] = useState<string | null>(null)

  const from = (location.state as { from?: string } | null)?.from ?? '/'

  useEffect(() => {
    if (accessToken) {
      navigate(from, { replace: true })
    }
  }, [accessToken, from, navigate])

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    const parsed = schemas.login.safeParse({ email, password })
    const zodErr = firstZodError(parsed, t)
    if (zodErr) {
      setError(zodErr)
      return
    }

    setLoading(true)
    try {
      const result = await login({ ...parsed.data!, turnstileToken })
      if (result.kind === 'twoFactor') {
        setPendingLoginId(result.data.pendingLoginId)
        return
      }
      setSession(result.data)
      navigate(from, { replace: true })
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : t('login.signInFailed'))
      setTurnstileToken(null)
    } finally {
      setLoading(false)
    }
  }

  const handleTwoFactor = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!pendingLoginId) return
    setError(null)
    const parsed = schemas.twoFactor.safeParse({ code: twoFaCode })
    const zodErr = firstZodError(parsed, t)
    if (zodErr) {
      setError(zodErr)
      return
    }

    setLoading(true)
    try {
      const auth = await completeTwoFactorLogin(pendingLoginId, parsed.data!.code)
      setSession(auth)
      navigate(from, { replace: true })
    } catch (err) {
      if (err instanceof ApiClientError && err.validationErrors) {
        setError(formatValidationErrors(err.validationErrors) || err.message)
      } else {
        setError(err instanceof ApiClientError ? err.message : t('login.verificationFailed'))
      }
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="relative flex min-h-screen items-center justify-center bg-[var(--color-surface)] p-4">
      <div className="absolute right-4 top-4 z-10 sm:right-6 sm:top-6">
        <LanguageSwitcher variant="compact" />
      </div>
      <Card className="w-full max-w-md">
        <CardHeader className="text-center">
          <div className="mx-auto mb-2 flex size-12 items-center justify-center rounded-full bg-[var(--color-accent)]/20">
            <Shield className="size-6 text-[var(--color-accent)]" />
          </div>
          <h1 className="text-xl font-semibold text-white">{t('login.title')}</h1>
          <p className="mt-1 text-sm text-slate-400">{t('login.subtitle')}</p>
        </CardHeader>
        <CardBody>
          {error ? (
            <Alert variant="error" className="mb-4">
              {error}
            </Alert>
          ) : null}

          {pendingLoginId ? (
            <form onSubmit={handleTwoFactor} className="space-y-4">
              <p className="text-sm text-slate-300">{t('login.twoFaPrompt')}</p>
              <Input
                label={t('login.authCode')}
                value={twoFaCode}
                onChange={(e) => setTwoFaCode(e.target.value)}
                autoComplete="one-time-code"
                inputMode="numeric"
                autoFocus
              />
              <Button type="submit" className="w-full" loading={loading}>
                {t('login.verify')}
              </Button>
              <Button
                type="button"
                variant="ghost"
                className="w-full"
                onClick={() => {
                  setPendingLoginId(null)
                  setTwoFaCode('')
                }}
              >
                {t('common.back')}
              </Button>
            </form>
          ) : (
            <form onSubmit={handleLogin} className="space-y-4">
              <Input
                label={t('login.email')}
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                autoComplete="username"
                required
              />
              <Input
                label={t('login.password')}
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password"
                required
              />
              <TurnstileWidget
                onVerify={setTurnstileToken}
                onExpire={() => setTurnstileToken(null)}
                onError={() => setTurnstileToken(null)}
              />
              <Button type="submit" className="w-full" loading={loading}>
                {t('login.signIn')}
              </Button>
            </form>
          )}
        </CardBody>
      </Card>
    </div>
  )
}
