# Email HTML templates

Each transactional email has its own `.html` file. The renderer replaces placeholders:

| Syntax | Example |
|--------|---------|
| `[[tokenName]]` | `[[greetingName]]` → user first name (HTML-encoded) |
| Raw URL tokens | `[[actionUrl]]` — not encoded (see `EmailTemplateTokens.Raw`) |

## Add a new email

1. Copy an existing template (e.g. `welcome.html`) to `your-template.html`.
2. Add a constant in `Application/Common/Email/EmailTemplateNames.cs`.
3. Add token keys in `EmailTemplateTokens.cs` if needed.
4. Build tokens in `EmailTemplateTokenBuilder` (or a feature-specific builder).
5. Call `IEmailTemplateRenderer.Render(EmailTemplateNames.YourTemplate, tokens)` from the sender/handler.

## Token reference (common)

| Token | Description |
|-------|-------------|
| `appName` | Brand from `EmailBranding:AppName` |
| `greetingName` | Display name or `there` |
| `headline` | Email title inside body |
| `actionUrl` | CTA link (raw) |
| `actionLabel` | Button text |
| `footerNote` | Small print |
| `ttlMinutes` | Link expiry (auth emails) |
| `year` | UTC year |
| `fallbackHtml` | Optional extra HTML when no public URL configured |

Templates are copied to the Api/Worker output as `Email/Templates/*.html`.
