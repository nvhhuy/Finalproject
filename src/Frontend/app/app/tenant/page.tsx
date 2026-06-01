"use client";

import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { PageContent, PageHeader } from "@/components/ui/page-header";
import { TenantSkeleton } from "@/components/ui/skeleton";
import { formatDateTimeOrDash, formatBytes } from "@/lib/shared";
import { billingApi, type BillingInvoiceDto, type PlanDto, type TenantSummaryDto } from "@/lib/features/billing/billing-api";
import { tenancyApi } from "@/lib/features/tenancy/tenancy-api";
import {
  getActiveTenantId,
  getActiveTenantSlug,
  resolveActiveTenantId,
  setActiveTenantId,
  setActiveTenantSlug,
} from "@/lib/tenancy/tenant-context";
import { getTenantSubdomainBaseHost } from "@/lib/tenancy/host";

function formatMoney(cents: number, currency: string) {
  if (currency === "CREDITS") return `${cents} credits`;
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(cents / 100);
  } catch {
    return `${(cents / 100).toFixed(2)} ${currency}`;
  }
}

export default function TenantUsagePage() {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const [tenantId, setTenantIdState] = useState<string | null>(null);
  const [mutationError, setMutationError] = useState<string | null>(null);

  // Form state
  const [planCode, setPlanCode] = useState("starter");
  const [newDomain, setNewDomain] = useState("");
  const [ssoEnabled, setSsoEnabled] = useState(false);
  const [ssoAuthority, setSsoAuthority] = useState("");
  const [ssoClientId, setSsoClientId] = useState("");
  const [ssoClientSecret, setSsoClientSecret] = useState("");
  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteRole, setInviteRole] = useState("member");
  const [inviteMsg, setInviteMsg] = useState<string | null>(null);

  // ── Queries ──────────────────────────────────────────────────────────────

  const tenantsQuery = useQuery({
    queryKey: ["tenants", "mine"],
    queryFn: async () => {
      const res = await billingApi.listMyTenants();
      if (!res.success || !res.data) throw new Error(res.error?.message ?? t("errors.requestFailed"));
      return res.data as TenantSummaryDto[];
    },
  });

  const plansQuery = useQuery({
    queryKey: ["billing", "public-plans"],
    queryFn: async () => {
      const res = await billingApi.listPublicPlans();
      if (!res.success || !res.data) return [] as PlanDto[];
      return res.data.filter((p) => p.isActive);
    },
  });

  const entitlementsQuery = useQuery({
    queryKey: ["billing", "entitlements", tenantId],
    enabled: !!tenantId,
    queryFn: async () => {
      const res = await billingApi.getTenantEntitlements(tenantId!);
      if (!res.success || !res.data) throw new Error(res.error?.message ?? t("errors.requestFailed"));
      return res.data;
    },
  });

  const invoicesQuery = useQuery({
    queryKey: ["billing", "invoices", tenantId],
    enabled: !!tenantId,
    queryFn: async () => {
      const res = await billingApi.listTenantInvoices(tenantId!);
      if (!res.success || !res.data) return [] as BillingInvoiceDto[];
      return res.data;
    },
  });

  const domainsQuery = useQuery({
    queryKey: ["tenants", "domains", tenantId],
    enabled: !!tenantId,
    queryFn: async () => {
      const res = await tenancyApi.listDomains(tenantId!);
      if (!res.success || !res.data) return [];
      return res.data;
    },
  });

  const ssoQuery = useQuery({
    queryKey: ["tenants", "sso", tenantId],
    enabled: !!tenantId,
    queryFn: async () => {
      const res = await tenancyApi.getSsoSettings(tenantId!);
      if (res.error?.code === "NotFoundError") return null;
      if (!res.success || !res.data) return null;
      return res.data;
    },
  });

  // Resolve active tenant once tenants load
  useEffect(() => {
    if (!tenantsQuery.data) return;
    const active = resolveActiveTenantId(tenantsQuery.data, getActiveTenantId(), getActiveTenantSlug());
    if (active) {
      setTenantIdState(active);
      setActiveTenantId(active);
      const slug = tenantsQuery.data.find((tn) => tn.id === active)?.slug;
      if (slug) setActiveTenantSlug(slug);
    }
  }, [tenantsQuery.data]);

  // Sync planCode when entitlements load
  useEffect(() => {
    if (entitlementsQuery.data) setPlanCode(entitlementsQuery.data.subscription.planCode);
  }, [entitlementsQuery.data]);

  // Sync SSO form when sso data loads
  useEffect(() => {
    if (ssoQuery.data) {
      setSsoEnabled(ssoQuery.data.isEnabled);
      setSsoAuthority(ssoQuery.data.oidcAuthority);
      setSsoClientId(ssoQuery.data.oidcClientId);
      setSsoClientSecret("");
    }
  }, [ssoQuery.data]);

  // ── Helpers ───────────────────────────────────────────────────────────────

  const invalidateTenant = () => {
    void qc.invalidateQueries({ queryKey: ["tenants", "domains", tenantId] });
    void qc.invalidateQueries({ queryKey: ["billing", "entitlements", tenantId] });
    void qc.invalidateQueries({ queryKey: ["billing", "invoices", tenantId] });
  };

  const handleTenantChange = (id: string) => {
    setTenantIdState(id);
    setActiveTenantId(id);
    const slug = tenantsQuery.data?.find((tn) => tn.id === id)?.slug;
    if (slug) setActiveTenantSlug(slug);
  };

  // ── Mutations ─────────────────────────────────────────────────────────────

  const addDomainMutation = useMutation({
    mutationFn: () => tenancyApi.addDomain(tenantId!, newDomain.trim()),
    onSuccess: (res) => {
      if (!res.success) { setMutationError(res.error?.message ?? t("errors.requestFailed")); return; }
      setMutationError(null);
      setNewDomain("");
      void qc.invalidateQueries({ queryKey: ["tenants", "domains", tenantId] });
    },
    onError: () => setMutationError(t("errors.requestFailed")),
  });

  const verifyDomainMutation = useMutation({
    mutationFn: (domainId: string) => tenancyApi.verifyDomain(tenantId!, domainId),
    onSuccess: (res) => {
      if (!res.success) { setMutationError(res.error?.message ?? t("errors.requestFailed")); return; }
      setMutationError(null);
      void qc.invalidateQueries({ queryKey: ["tenants", "domains", tenantId] });
    },
    onError: () => setMutationError(t("errors.requestFailed")),
  });

  const removeDomainMutation = useMutation({
    mutationFn: (domainId: string) => tenancyApi.removeDomain(tenantId!, domainId),
    onSuccess: (res) => {
      if (!res.success) { setMutationError(res.error?.message ?? t("errors.requestFailed")); return; }
      setMutationError(null);
      void qc.invalidateQueries({ queryKey: ["tenants", "domains", tenantId] });
    },
    onError: () => setMutationError(t("errors.requestFailed")),
  });

  const changePlanMutation = useMutation({
    mutationFn: () => billingApi.changeSubscriptionPlan(tenantId!, planCode),
    onSuccess: (res) => {
      if (!res.success) { setMutationError(res.error?.message ?? t("errors.requestFailed")); return; }
      setMutationError(null);
      invalidateTenant();
    },
    onError: () => setMutationError(t("errors.requestFailed")),
  });

  const cancelMutation = useMutation({
    mutationFn: () => billingApi.cancelSubscription(tenantId!),
    onSuccess: (res) => {
      if (!res.success) { setMutationError(res.error?.message ?? t("errors.requestFailed")); return; }
      setMutationError(null);
      void qc.invalidateQueries({ queryKey: ["billing", "entitlements", tenantId] });
    },
    onError: () => setMutationError(t("errors.requestFailed")),
  });

  const saveSsoMutation = useMutation({
    mutationFn: () =>
      tenancyApi.upsertSsoSettings(tenantId!, {
        isEnabled: ssoEnabled,
        oidcAuthority: ssoAuthority.trim(),
        oidcClientId: ssoClientId.trim(),
        oidcClientSecret: ssoClientSecret.trim() || null,
      }),
    onSuccess: (res) => {
      if (!res.success || !res.data) { setMutationError(res.error?.message ?? t("errors.requestFailed")); return; }
      setMutationError(null);
      setSsoClientSecret("");
      void qc.invalidateQueries({ queryKey: ["tenants", "sso", tenantId] });
    },
    onError: () => setMutationError(t("errors.requestFailed")),
  });

  const inviteMutation = useMutation({
    mutationFn: () => tenancyApi.inviteMember(tenantId!, inviteEmail.trim(), inviteRole),
    onSuccess: (res) => {
      if (!res.success) { setMutationError(res.error?.message ?? t("errors.requestFailed")); return; }
      setMutationError(null);
      setInviteEmail("");
      setInviteMsg(t("tenantUsage.inviteSent"));
      void qc.invalidateQueries({ queryKey: ["billing", "entitlements", tenantId] });
    },
    onError: () => setMutationError(t("errors.requestFailed")),
  });

  // ── Derived ───────────────────────────────────────────────────────────────

  const tenants = tenantsQuery.data ?? [];
  const entitlements = entitlementsQuery.data ?? null;
  const invoices = invoicesQuery.data ?? [];
  const domains = domainsQuery.data ?? [];
  const sso = ssoQuery.data ?? null;
  const activeTenant = tenants.find((tn) => tn.id === tenantId);
  const subdomainBase = getTenantSubdomainBaseHost();
  const workspaceUrl = activeTenant && subdomainBase ? `https://${activeTenant.slug}.${subdomainBase}` : null;
  const domainBusy = addDomainMutation.isPending || verifyDomainMutation.isPending || removeDomainMutation.isPending;
  const portalBusy = changePlanMutation.isPending || cancelMutation.isPending;
  const loading = tenantsQuery.isLoading;

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <PageContent>
      <PageHeader title={t("tenantUsage.title")} description={t("tenantUsage.subtitle")} />
      {(mutationError ?? tenantsQuery.error) ? (
        <Alert variant="error">{mutationError ?? (tenantsQuery.error as Error)?.message}</Alert>
      ) : null}
      {loading ? <TenantSkeleton /> : null}

      {!loading && tenants.length > 0 ? (
        <div className="mb-4">
          <label className="text-sm font-medium">{t("tenantUsage.selectTenant")}</label>
          <select
            className="mt-1 h-10 w-full max-w-md rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 text-sm"
            value={tenantId ?? ""}
            onChange={(e) => handleTenantChange(e.target.value)}
          >
            {tenants.map((tn) => (
              <option key={tn.id} value={tn.id}>
                {tn.displayName} ({tn.slug})
              </option>
            ))}
          </select>
        </div>
      ) : null}

      {!loading && tenantId ? (
        <Card className="mb-4">
          <CardHeader>
            <CardTitle>{t("tenantUsage.inviteMember")}</CardTitle>
          </CardHeader>
          <CardBody className="flex flex-wrap items-end gap-3">
            <div className="min-w-[200px] flex-1">
              <label className="text-xs font-medium text-[var(--color-muted)]">{t("common.email")}</label>
              <Input
                className="mt-1"
                type="email"
                value={inviteEmail}
                onChange={(e) => setInviteEmail(e.target.value)}
                disabled={inviteMutation.isPending}
              />
            </div>
            <div className="min-w-[140px]">
              <label className="text-xs font-medium text-[var(--color-muted)]">{t("tenantUsage.inviteRole")}</label>
              <select
                className="mt-1 h-10 w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 text-sm"
                value={inviteRole}
                onChange={(e) => setInviteRole(e.target.value)}
                disabled={inviteMutation.isPending}
              >
                <option value="member">{t("tenantUsage.roleMember")}</option>
                <option value="admin">{t("tenantUsage.roleAdmin")}</option>
              </select>
            </div>
            <Button loading={inviteMutation.isPending} onClick={() => inviteMutation.mutate()}>
              {t("tenantUsage.inviteSubmit")}
            </Button>
            {inviteMsg ? <p className="w-full text-sm text-[var(--color-muted)]">{inviteMsg}</p> : null}
          </CardBody>
        </Card>
      ) : null}

      {!loading && entitlements ? (
        <div className="grid gap-4 md:grid-cols-2">
          <Card>
            <CardHeader>
              <CardTitle>{t("tenantUsage.subscription")}</CardTitle>
            </CardHeader>
            <CardBody className="space-y-1 text-sm">
              <p>
                <strong>{entitlements.subscription.planName}</strong> ({entitlements.subscription.planCode})
              </p>
              <p className="text-[var(--color-muted)]">
                {t("tenantUsage.status")}: {entitlements.subscription.status}
              </p>
              <p className="text-[var(--color-muted)]">
                {entitlements.allowsWriteAccess ? t("tenantUsage.writeEnabled") : t("tenantUsage.readOnly")}
              </p>
            </CardBody>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>{t("tenantUsage.usage")}</CardTitle>
            </CardHeader>
            <CardBody className="space-y-1 text-sm">
              <p>
                {t("tenantUsage.members")}: {entitlements.usage.memberCount} / {entitlements.limits.maxUsers}
              </p>
              <p>
                {t("tenantUsage.storage")}: {formatBytes(entitlements.usage.storageBytes)} /{" "}
                {formatBytes(entitlements.limits.maxStorageBytes)}
              </p>
              <p>
                {t("tenantUsage.openTickets")}: {entitlements.usage.openSupportTicketCount} /{" "}
                {entitlements.limits.maxOpenSupportTickets}
              </p>
            </CardBody>
          </Card>

          <Card className="md:col-span-2">
            <CardHeader>
              <CardTitle>{t("tenantUsage.portal")}</CardTitle>
            </CardHeader>
            <CardBody className="flex flex-wrap items-end gap-3">
              <div className="min-w-[200px] flex-1">
                <label className="text-xs font-medium text-[var(--color-muted)]">{t("tenantUsage.changePlan")}</label>
                <select
                  className="mt-1 h-10 w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 text-sm"
                  value={planCode}
                  onChange={(e) => setPlanCode(e.target.value)}
                  disabled={entitlements.subscription.status === "Canceled"}
                >
                  {(plansQuery.data ?? []).map((p) => (
                    <option key={p.id} value={p.code}>
                      {p.name}
                    </option>
                  ))}
                </select>
              </div>
              <Button
                variant="secondary"
                loading={changePlanMutation.isPending}
                disabled={portalBusy || entitlements.subscription.status === "Canceled"}
                onClick={() => changePlanMutation.mutate()}
              >
                {t("tenantUsage.changePlanSubmit")}
              </Button>
              <Button
                variant="secondary"
                loading={cancelMutation.isPending}
                disabled={portalBusy || entitlements.subscription.status === "Canceled"}
                onClick={() => {
                  if (window.confirm(t("tenantUsage.cancelConfirm"))) cancelMutation.mutate();
                }}
              >
                {t("tenantUsage.cancelSubscription")}
              </Button>
            </CardBody>
          </Card>

          <Card className="md:col-span-2">
            <CardHeader>
              <CardTitle>{t("tenantUsage.domains")}</CardTitle>
            </CardHeader>
            <CardBody className="space-y-3 text-sm">
              {workspaceUrl ? (
                <p className="text-[var(--color-muted)]">
                  {t("tenantUsage.subdomainUrl")}:{" "}
                  <a className="text-[var(--color-primary)] underline" href={workspaceUrl}>
                    {workspaceUrl}
                  </a>
                </p>
              ) : null}
              <div className="flex flex-wrap gap-2">
                <Input
                  className="min-w-[220px] flex-1"
                  placeholder={t("tenantUsage.customDomainPlaceholder")}
                  value={newDomain}
                  onChange={(e) => setNewDomain(e.target.value)}
                  disabled={domainBusy}
                />
                <Button
                  variant="secondary"
                  loading={addDomainMutation.isPending}
                  disabled={domainBusy || !newDomain.trim()}
                  onClick={() => addDomainMutation.mutate()}
                >
                  {t("tenantUsage.addDomain")}
                </Button>
              </div>
              {domains.length === 0 ? (
                <p className="text-[var(--color-muted)]">{t("tenantUsage.noDomains")}</p>
              ) : (
                domains.map((d) => (
                  <div key={d.id} className="rounded-lg border border-[var(--color-border)] p-3">
                    <p className="font-medium">{d.hostname}</p>
                    <p className="text-[var(--color-muted)]">
                      {t("tenantUsage.domainStatus", { status: d.status })}
                    </p>
                    {d.status === "PendingVerification" ? (
                      <p className="mt-1 text-xs text-[var(--color-muted)]">
                        TXT {d.txtRecordName} = {d.txtRecordValue}
                        {d.cnameTarget ? ` · CNAME → ${d.cnameTarget}` : null}
                      </p>
                    ) : null}
                    <div className="mt-2 flex flex-wrap gap-2">
                      {d.status === "PendingVerification" ? (
                        <Button
                          size="sm"
                          variant="secondary"
                          loading={verifyDomainMutation.isPending && verifyDomainMutation.variables === d.id}
                          disabled={domainBusy}
                          onClick={() => verifyDomainMutation.mutate(d.id)}
                        >
                          {t("tenantUsage.verifyDomain")}
                        </Button>
                      ) : null}
                      <Button
                        size="sm"
                        variant="secondary"
                        loading={removeDomainMutation.isPending && removeDomainMutation.variables === d.id}
                        disabled={domainBusy}
                        onClick={() => removeDomainMutation.mutate(d.id)}
                      >
                        {t("tenantUsage.removeDomain")}
                      </Button>
                    </div>
                  </div>
                ))
              )}
            </CardBody>
          </Card>

          <Card className="md:col-span-2">
            <CardHeader>
              <CardTitle>{t("tenantUsage.sso")}</CardTitle>
            </CardHeader>
            <CardBody className="space-y-3 text-sm">
              <p className="text-[var(--color-muted)]">{t("tenantUsage.ssoHint")}</p>
              {!sso ? <p className="text-[var(--color-muted)]">{t("tenantUsage.ssoNotConfigured")}</p> : null}
              <label className="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={ssoEnabled}
                  onChange={(e) => setSsoEnabled(e.target.checked)}
                  disabled={saveSsoMutation.isPending}
                />
                {t("tenantUsage.ssoEnabled")}
              </label>
              <div>
                <label className="text-xs font-medium text-[var(--color-muted)]">{t("tenantUsage.ssoAuthority")}</label>
                <Input
                  className="mt-1"
                  value={ssoAuthority}
                  onChange={(e) => setSsoAuthority(e.target.value)}
                  disabled={saveSsoMutation.isPending}
                  placeholder="https://login.example.com"
                />
              </div>
              <div>
                <label className="text-xs font-medium text-[var(--color-muted)]">{t("tenantUsage.ssoClientId")}</label>
                <Input
                  className="mt-1"
                  value={ssoClientId}
                  onChange={(e) => setSsoClientId(e.target.value)}
                  disabled={saveSsoMutation.isPending}
                />
              </div>
              <div>
                <label className="text-xs font-medium text-[var(--color-muted)]">{t("tenantUsage.ssoClientSecret")}</label>
                <Input
                  className="mt-1"
                  type="password"
                  value={ssoClientSecret}
                  onChange={(e) => setSsoClientSecret(e.target.value)}
                  disabled={saveSsoMutation.isPending}
                  placeholder={t("tenantUsage.ssoClientSecretPlaceholder")}
                />
                {sso?.hasClientSecret ? (
                  <p className="mt-1 text-xs text-[var(--color-muted)]">{t("tenantUsage.ssoHasSecret")}</p>
                ) : null}
              </div>
              {activeTenant ? (
                <p className="text-xs text-[var(--color-muted)]">
                  {t("tenantUsage.ssoPublicEndpoint")}: GET /api/v1/public/tenants/{activeTenant.slug}/sso
                </p>
              ) : null}
              <p className="text-xs text-[var(--color-muted)]">{t("tenantUsage.ssoSignInHint")}</p>
              <Button
                variant="secondary"
                loading={saveSsoMutation.isPending}
                onClick={() => saveSsoMutation.mutate()}
              >
                {t("tenantUsage.ssoSave")}
              </Button>
            </CardBody>
          </Card>

          <Card className="md:col-span-2">
            <CardHeader>
              <CardTitle>{t("tenantUsage.invoices")}</CardTitle>
            </CardHeader>
            <CardBody className="space-y-2 text-sm">
              {invoices.length === 0 ? (
                <p className="text-[var(--color-muted)]">{t("tenantUsage.noInvoices")}</p>
              ) : (
                invoices.map((inv) => (
                  <div
                    key={inv.id}
                    className="flex flex-wrap justify-between gap-2 border-b border-[var(--color-border)] py-2"
                  >
                    <div>
                      <p className="font-medium">{inv.invoiceNumber}</p>
                      <p className="text-[var(--color-muted)]">{inv.description}</p>
                    </div>
                    <div className="text-right">
                      <p>{formatMoney(inv.amountCents, inv.currency)}</p>
                      <p className="text-[var(--color-muted)]">
                        {formatDateTimeOrDash(inv.issuedAtUtc)} · {inv.provider}
                      </p>
                    </div>
                  </div>
                ))
              )}
            </CardBody>
          </Card>
        </div>
      ) : null}
    </PageContent>
  );
}
