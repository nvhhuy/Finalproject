import { apiRequest } from "@/lib/api/http";

export type TenantHostResolutionDto = {
  tenantId: string;
  slug: string;
  displayName: string;
  source: string;
};

export type TenantDomainDto = {
  id: string;
  tenantId: string;
  hostname: string;
  status: string;
  verificationToken: string;
  txtRecordName: string;
  txtRecordValue: string;
  cnameTarget?: string | null;
  createdAtUtc: string;
  verifiedAtUtc?: string | null;
};

export const tenancyApi = {
  resolveHost(host: string) {
    return apiRequest<TenantHostResolutionDto>({
      path: "/api/v1/public/tenants/resolve-host",
      query: { host },
    });
  },
  inviteMember(tenantId: string, email: string, role: string) {
    return apiRequest<{ id: string; tenantId: string; userId: string; role: string; joinedAtUtc: string }>({
      path: `/api/v1/tenants/${tenantId}/members`,
      method: "POST",
      body: { email, role },
      auth: true,
      idempotent: true,
    });
  },
  listDomains(tenantId: string) {
    return apiRequest<TenantDomainDto[]>({
      path: `/api/v1/tenants/${tenantId}/domains`,
      auth: true,
    });
  },
  addDomain(tenantId: string, hostname: string) {
    return apiRequest<TenantDomainDto>({
      path: `/api/v1/tenants/${tenantId}/domains`,
      method: "POST",
      body: { hostname },
      auth: true,
      idempotent: true,
    });
  },
  verifyDomain(tenantId: string, domainId: string) {
    return apiRequest<TenantDomainDto>({
      path: `/api/v1/tenants/${tenantId}/domains/${domainId}/verify`,
      method: "POST",
      auth: true,
      idempotent: true,
    });
  },
  removeDomain(tenantId: string, domainId: string) {
    return apiRequest<null>({
      path: `/api/v1/tenants/${tenantId}/domains/${domainId}`,
      method: "DELETE",
      auth: true,
      idempotent: true,
    });
  },
  getSsoSettings(tenantId: string) {
    return apiRequest<TenantSsoSettingsDto>({
      path: `/api/v1/tenants/${tenantId}/sso`,
      auth: true,
    });
  },
  upsertSsoSettings(
    tenantId: string,
    body: { isEnabled: boolean; oidcAuthority: string; oidcClientId: string; oidcClientSecret?: string | null },
  ) {
    return apiRequest<TenantSsoSettingsDto>({
      path: `/api/v1/tenants/${tenantId}/sso`,
      method: "PUT",
      body,
      auth: true,
      idempotent: true,
    });
  },
  getPublicSso(slug: string) {
    return apiRequest<TenantSsoPublicDto>({
      path: `/api/v1/public/tenants/${encodeURIComponent(slug)}/sso`,
    });
  },
};

export type TenantSsoSettingsDto = {
  id: string;
  tenantId: string;
  isEnabled: boolean;
  oidcAuthority: string;
  oidcClientId: string;
  hasClientSecret: boolean;
  updatedAtUtc: string;
};

export type TenantSsoPublicDto = {
  tenantId: string;
  tenantSlug: string;
  isEnabled: boolean;
  oidcAuthority: string;
  oidcClientId: string;
};
