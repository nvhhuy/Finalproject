const TENANT_ID_KEY = "app.activeTenantId";
const TENANT_SLUG_KEY = "app.activeTenantSlug";

export function getActiveTenantId(): string | null {
  if (typeof window === "undefined") return null;
  return localStorage.getItem(TENANT_ID_KEY);
}

export function setActiveTenantId(tenantId: string) {
  if (typeof window === "undefined") return;
  localStorage.setItem(TENANT_ID_KEY, tenantId);
}

export function getActiveTenantSlug(): string | null {
  if (typeof window === "undefined") return null;
  return localStorage.getItem(TENANT_SLUG_KEY);
}

export function setActiveTenantSlug(slug: string) {
  if (typeof window === "undefined") return;
  localStorage.setItem(TENANT_SLUG_KEY, slug);
}

export function clearActiveTenantId() {
  if (typeof window === "undefined") return;
  localStorage.removeItem(TENANT_ID_KEY);
  localStorage.removeItem(TENANT_SLUG_KEY);
}

/** Prefer stored id when still a member; otherwise match slug; else first tenant. */
export function resolveActiveTenantId(
  tenants: ReadonlyArray<{ id: string; slug: string }>,
  preferredId: string | null,
  preferredSlug: string | null,
): string | null {
  if (preferredId && tenants.some((tenant) => tenant.id === preferredId)) return preferredId;
  if (preferredSlug) {
    const bySlug = tenants.find((tenant) => tenant.slug === preferredSlug);
    if (bySlug) return bySlug.id;
  }
  return tenants[0]?.id ?? null;
}

export function tenantHeaders(): Record<string, string> {
  const tenantId = getActiveTenantId();
  return tenantId ? { "X-Tenant-Id": tenantId } : {};
}
