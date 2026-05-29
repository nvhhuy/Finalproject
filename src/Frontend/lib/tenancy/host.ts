const RESERVED = new Set(["www", "api", "admin", "app", "mail", "static"]);

export function getTenantSubdomainBaseHost(): string | null {
  const base =
    process.env.TENANT_SUBDOMAIN_BASE_HOST ??
    process.env.NEXT_PUBLIC_TENANT_SUBDOMAIN_BASE_HOST ??
    "";
  return base.trim().toLowerCase() || null;
}

export function extractTenantSlugFromHost(host: string, baseHost: string): string | null {
  const normalizedHost = host.split(":")[0]?.trim().toLowerCase() ?? "";
  const normalizedBase = baseHost.trim().toLowerCase();
  if (!normalizedHost || !normalizedBase) return null;
  if (normalizedHost === normalizedBase || normalizedHost === `www.${normalizedBase}`) return null;

  const suffix = `.${normalizedBase}`;
  if (!normalizedHost.endsWith(suffix)) return null;

  const subdomain = normalizedHost.slice(0, -suffix.length);
  if (!subdomain || subdomain.includes(".") || RESERVED.has(subdomain)) return null;
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(subdomain)) return null;
  return subdomain;
}
