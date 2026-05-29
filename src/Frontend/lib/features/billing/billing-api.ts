import { apiRequest } from "@/lib/api/http";

export type PlanLimitsDto = {
  maxUsers: number;
  maxStorageBytes: number;
  modules: string[];
  maxOpenSupportTickets: number;
  adminSeats: number;
  impersonation: boolean;
  backupRestore: boolean;
};

export type PlanDto = {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  priceMonthlyCents: number;
  currency: string;
  trialDays: number;
  limits: PlanLimitsDto;
  isActive: boolean;
  sortOrder: number;
};

export type SubscribeToPlanRequest = {
  planCode: string;
  tenantSlug: string;
  tenantDisplayName: string;
  publicTagline?: string | null;
  publicDescription?: string | null;
};

export type SubscribeToPlanResponse = {
  tenantId: string;
  tenantSlug: string;
  subscriptionId: string;
  subscriptionStatus: string;
};

export type TenantPublicDto = {
  id: string;
  slug: string;
  displayName: string;
  publicTagline?: string | null;
  publicDescription?: string | null;
};

export type TenantUsageDto = {
  memberCount: number;
  storageBytes: number;
  openSupportTicketCount: number;
};

export type SubscriptionDto = {
  id: string;
  tenantId: string;
  planId: string;
  planCode: string;
  planName: string;
  status: string;
  currentPeriodStartUtc: string;
  currentPeriodEndUtc: string;
  allowsWriteAccess: boolean;
};

export type TenantEntitlementsDto = {
  subscription: SubscriptionDto;
  limits: PlanLimitsDto;
  usage: TenantUsageDto;
  allowsWriteAccess: boolean;
};

export type TenantSummaryDto = {
  id: string;
  slug: string;
  displayName: string;
  status: string;
  ownerUserId: string;
  createdAtUtc: string;
};

export type BillingInvoiceDto = {
  id: string;
  tenantId?: string | null;
  userId?: string | null;
  invoiceNumber: string;
  description: string;
  amountCents: number;
  amountCredits?: number | null;
  currency: string;
  status: string;
  provider: string;
  issuedAtUtc: string;
};

export const billingApi = {
  listPublicPlans() {
    return apiRequest<PlanDto[]>({ path: "/api/v1/public/plans" });
  },
  subscribe(body: SubscribeToPlanRequest) {
    return apiRequest<SubscribeToPlanResponse>({
      path: "/api/v1/billing/subscribe",
      method: "POST",
      body,
      auth: true,
      idempotent: true,
    });
  },
  listMyTenants() {
    return apiRequest<TenantSummaryDto[]>({ path: "/api/v1/tenants/mine", auth: true });
  },
  getPublicTenant(slug: string) {
    return apiRequest<TenantPublicDto>({ path: `/api/v1/public/tenants/${encodeURIComponent(slug)}` });
  },
  getTenantEntitlements(tenantId: string) {
    return apiRequest<TenantEntitlementsDto>({
      path: `/api/v1/billing/tenants/${tenantId}/entitlements`,
      auth: true,
    });
  },
  cancelSubscription(tenantId: string) {
    return apiRequest<SubscriptionDto>({
      path: `/api/v1/billing/tenants/${tenantId}/subscription/cancel`,
      method: "POST",
      auth: true,
      idempotent: true,
    });
  },
  changeSubscriptionPlan(tenantId: string, planCode: string) {
    return apiRequest<SubscriptionDto>({
      path: `/api/v1/billing/tenants/${tenantId}/subscription/plan`,
      method: "PUT",
      body: { planCode },
      auth: true,
      idempotent: true,
    });
  },
  listTenantInvoices(tenantId: string, page = 1, pageSize = 20) {
    return apiRequest<BillingInvoiceDto[]>({
      path: `/api/v1/billing/tenants/${tenantId}/invoices`,
      auth: true,
      query: { page, pageSize },
    });
  },
};
