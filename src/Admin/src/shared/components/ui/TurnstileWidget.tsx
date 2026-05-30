import { Turnstile } from '@marsidev/react-turnstile'

type TurnstileWidgetProps = {
  onVerify: (token: string) => void
  onExpire?: () => void
  onError?: () => void
  className?: string
}

const SITE_KEY = (import.meta.env.VITE_TURNSTILE_SITE_KEY as string | undefined) ?? ''

/**
 * Renders the Cloudflare Turnstile challenge widget.
 * When VITE_TURNSTILE_SITE_KEY is not set the widget is not rendered.
 */
export function TurnstileWidget({ onVerify, onExpire, onError, className }: TurnstileWidgetProps) {
  if (!SITE_KEY) return null

  return (
    <Turnstile
      siteKey={SITE_KEY}
      onSuccess={onVerify}
      onExpire={onExpire}
      onError={onError}
      className={className}
      options={{ theme: 'auto' }}
    />
  )
}
