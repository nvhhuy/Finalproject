"use client";

import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { PageContent, PageHeader } from "@/components/ui/page-header";
import { TenantSkeleton } from "@/components/ui/skeleton";
import {
  billingApi,
  type BillingInvoiceDto,
  type PlanDto,
  type TenantEntitlementsDto,
  type TenantSummaryDto,
} from "@/lib/features/billing/billing-api";
import {
  tenancyApi,
  type TenantDomainDto,
  type TenantSsoSettingsDto,
} from "@/lib/features/tenancy/tenancy-api";
import {
  getActiveTenantId,
  getActiveTenantSlug,
  resolveActiveTenantId,
  setActiveTenantId,
  setActiveTenantSlug,
} from "@/lib/tenancy/tenant-context";
import { getTenantSubdomainBaseHost } from "@/lib/tenancy/host";
import { Input } from "@/components/ui/input";
import { formatDateTimeOrDash } from "@/lib/shared";

function formatMoney(cents: number, currency: string) {
  if (currency === "CREDITS") return `${cents} credits`;
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(cents / 100);
  } catch {
    return `${(cents / 100).toFixed(2)} ${currency}`;
  }
}

function formatBytes(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

export default function TenantUsagePage() {
  const { t } = useTranslation();
  const [tenants, setTenants] = useState<TenantSummaryDto[]>([]);
  const [tenantId, setTenantId] = useState<string | null>(null);
  const [entitlements, setEntitlements] = useState<TenantEntitlementsDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [plans, setPlans] = useState<PlanDto[]>([]);
  const [planCode, setPlanCode] = useState("starter");
  const [portalBusy, setPortalBusy] = useState(false);
  const [invoices, setInvoices] = useState<BillingInvoiceDto[]>([]);
  const [domains, setDomains] = useState<TenantDomainDto[]>([]);
  const [newDomain, setNewDomain] = useState("");
  const [domainBusy, setDomainBusy] = useState(false);
  const [sso, setSso] = useState<TenantSsoSettingsDto | null>(null);
  const [ssoEnabled, setSsoEnabled] = useState(false);
  const [ssoAuthority, setSsoAuthority] = useState("");
  const [ssoClientId, setSsoClientId] = useState("");
  const [ssoClientSecret, setSsoClientSecret] = useState("");
  const [ssoBusy, setSsoBusy] = useState(false);
  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteRole, setInviteRole] = useState("member");
  const [inviteBusy, setInviteBusy] = useState(false);
  const [inviteMsg, setInviteMsg] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      const mine = await billingApi.listMyTenants();
      if (!mine.success || !mine.data) {
        setError(mine.error?.message ?? t("errors.requestFailed"));
        setLoading(false);
        return;
      }
      setTenants(mine.data);
      const active = resolveActiveTenantId(mine.data, getActiveTenantId(), getActiveTenantSlug());
      setTenantId(active);
      if (active) {
        setActiveTenantId(active);
        const slug = mine.data.find((tenant) => tenant.id === active)?.slug;
        if (slug) setActiveTenantSlug(slug);
      }
      const planRes = await billingApi.listPublicPlans();
      if (planRes.success && planRes.data) setPlans(planRes.data.filter((p) => p.isActive));
      setLoading(false);
    })();
  }, [t]);

  useEffect(() => {
    if (!tenantId) return;
    void (async () => {
      setError(null);
      const res = await billingApi.getTenantEntitlements(tenantId);
      if (!res.success || !res.data) {
        setEntitlements(null);
        setError(res.error?.message ?? t("errors.requestFailed"));
        return;
      }
      setEntitlements(res.data);
      setPlanCode(res.data.subscription.planCode);
      const inv = await billingApi.listTenantInvoices(tenantId);
      if (inv.success && inv.data) setInvoices(inv.data);
      else setInvoices([]);

      const dom = await tenancyApi.listDomains(tenantId);
      if (dom.success && dom.data) setDomains(dom.data);
      else setDomains([]);

      const ssoRes = await tenancyApi.getSsoSettings(tenantId);
      if (ssoRes.success && ssoRes.data) {
        setSso(ssoRes.data);
        setSsoEnabled(ssoRes.data.isEnabled);
        setSsoAuthority(ssoRes.data.oidcAuthority);
        setSsoClientId(ssoRes.data.oidcClientId);
        setSsoClientSecret("");
      } else if (ssoRes.error?.code === "NotFoundError") {
        setSso(null);
        setSsoEnabled(false);
        setSsoAuthority("");
        setSsoClientId("");
        setSsoClientSecret("");
      }
    })();
  }, [tenantId, t]);

  const activeTenant = tenants.find((tn) => tn.id === tenantId);
  const subdomainBase = getTenantSubdomainBaseHost();
  const workspaceUrl =
    activeTenant && subdomainBase ? `https://${activeTenant.slug}.${subdomainBase}` : null;

  async function handleAddDomain() {
    if (!tenantId || !newDomain.trim()) return;
    setDomainBusy(true);
    setError(null);
    const res = await tenancyApi.addDomain(tenantId, newDomain.trim());
    setDomainBusy(false);
    if (!res.success || !res.data) {
      setError(res.error?.message ?? t("errors.requestFailed"));
      return;
    }
    setNewDomain("");
    const list = await tenancyApi.listDomains(tenantId);
    if (list.success && list.data) setDomains(list.data);
  }

  async function handleVerifyDomain(domainId: string) {
    if (!tenantId) return;
    setDomainBusy(true);
    setError(null);
    const res = await tenancyApi.verifyDomain(tenantId, domainId);
    setDomainBusy(false);
    if (!res.success) {
      setError(res.error?.message ?? t("errors.requestFailed"));
      return;
    }
    const list = await tenancyApi.listDomains(tenantId);
    if (list.success && list.data) setDomains(list.data);
  }

  async function handleRemoveDomain(domainId: string) {
    if (!tenantId) return;
    setDomainBusy(true);
    setError(null);
    const res = await tenancyApi.removeDomain(tenantId, domainId);
    setDomainBusy(false);
    if (!res.success) {
      setError(res.error?.message ?? t("errors.requestFailed"));
      return;
    }
    const list = await tenancyApi.listDomains(tenantId);
    if (list.success && list.data) setDomains(list.data);
  }

  async function handleChangePlan() {
    if (!tenantId) return;
    setPortalBusy(true);
    setError(null);
    const res = await billingApi.changeSubscriptionPlan(tenantId, planCode);
    setPortalBusy(false);
    if (!res.success) {
      setError(res.error?.message ?? t("errors.requestFailed"));
      return;
    }
    const [ent, inv] = await Promise.all([
      billingApi.getTenantEntitlements(tenantId),
      billingApi.listTenantInvoices(tenantId),
    ]);
    if (ent.success && ent.data) setEntitlements(ent.data);
    if (inv.success && inv.data) setInvoices(inv.data);
    else setInvoices([]);
  }

  async function handleSaveSso() {
    if (!tenantId) return;
    setSsoBusy(true);
    setError(null);
    const res = await tenancyApi.upsertSsoSettings(tenantId, {
      isEnabled: ssoEnabled,
      oidcAuthority: ssoAuthority.trim(),
      oidcClientId: ssoClientId.trim(),
      oidcClientSecret: ssoClientSecret.trim() ? ssoClientSecret.trim() : null,
    });
    setSsoBusy(false);
    if (!res.success || !res.data) {
      setError(res.error?.message ?? t("errors.requestFailed"));
      return;
    }
    setSso(res.data);
    setSsoEnabled(res.data.isEnabled);
    setSsoAuthority(res.data.oidcAuthority);
    setSsoClientId(res.data.oidcClientId);
    setSsoClientSecret("");
  }

  async function handleInviteMember() {
    if (!tenantId || !inviteEmail.trim()) return;
    setInviteBusy(true);
    setError(null);
    setInviteMsg(null);
    const res = await tenancyApi.inviteMember(tenantId, inviteEmail.trim(), inviteRole);
    setInviteBusy(false);
    if (!res.success) {
      setError(res.error?.message ?? t("errors.requestFailed"));
      return;
    }
    setInviteMsg(t("tenantUsage.inviteSent"));
    setInviteEmail("");
    const ent = await billingApi.getTenantEntitlements(tenantId);
    if (ent.success && ent.data) setEntitlements(ent.data);
  }

  async function handleCancel() {
    if (!tenantId || !window.confirm(t("tenantUsage.cancelConfirm"))) return;
    setPortalBusy(true);
    setError(null);
    const res = await billingApi.cancelSubscription(tenantId);
    setPortalBusy(false);
    if (!res.success) {
      setError(res.error?.message ?? t("errors.requestFailed"));
      return;
    }
    const ent = await billingApi.getTenantEntitlements(tenantId);
    if (ent.success && ent.data) setEntitlements(ent.data);
  }

  return (
    <PageContent>
      <PageHeader title={t("tenantUsage.title")} description={t("tenantUsage.subtitle")} />
      {error ? <Alert variant="error">{error}</Alert> : null}
      {loading ? <TenantSkeleton /> : null}

      {!loading && tenants.length > 0 ? (
        <div className="mb-4">
          <label className="text-sm font-medium">{t("tenantUsage.selectTenant")}</label>
          <select
            className="mt-1 h-10 w-full max-w-md rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 text-sm"
            value={tenantId ?? ""}
            onChange={(e) => {
              const id = e.target.value;
              setTenantId(id);
              setActiveTenantId(id);
              const slug = tenants.find((tn) => tn.id === id)?.slug;
              if (slug) setActiveTenantSlug(slug);
            }}
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
                disabled={inviteBusy}
              />
            </div>
            <div className="min-w-[140px]">
              <label className="text-xs font-medium text-[var(--color-muted)]">{t("tenantUsage.inviteRole")}</label>
              <select
                className="mt-1 h-10 w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 text-sm"
                value={inviteRole}
                onChange={(e) => setInviteRole(e.target.value)}
                disabled={inviteBusy}
              >
                <option value="member">{t("tenantUsage.roleMember")}</option>
                <option value="admin">{t("tenantUsage.roleAdmin")}</option>
              </select>
            </div>
            <Button loading={inviteBusy} onClick={() => void handleInviteMember()}>
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
                  {plans.map((p) => (
                    <option key={p.id} value={p.code}>
                      {p.name}
                    </option>
                  ))}
                </select>
              </div>
              <Button
                variant="secondary"
                disabled={portalBusy || entitlements.subscription.status === "Canceled"}
                onClick={() => void handleChangePlan()}
              >
                {t("tenantUsage.changePlanSubmit")}
              </Button>
              <Button
                variant="secondary"
                disabled={portalBusy || entitlements.subscription.status === "Canceled"}
                onClick={() => void handleCancel()}
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
                <Button variant="secondary" disabled={domainBusy} onClick={() => void handleAddDomain()}>
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
                        <Button size="sm" variant="secondary" disabled={domainBusy} onClick={() => void handleVerifyDomain(d.id)}>
                          {t("tenantUsage.verifyDomain")}
                        </Button>
                      ) : null}
                      <Button size="sm" variant="secondary" disabled={domainBusy} onClick={() => void handleRemoveDomain(d.id)}>
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
              {!sso ? (
                <p className="text-[var(--color-muted)]">{t("tenantUsage.ssoNotConfigured")}</p>
              ) : null}
              <label className="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={ssoEnabled}
                  onChange={(e) => setSsoEnabled(e.target.checked)}
                  disabled={ssoBusy}
                />
                {t("tenantUsage.ssoEnabled")}
              </label>
              <div>
                <label className="text-xs font-medium text-[var(--color-muted)]">
                  {t("tenantUsage.ssoAuthority")}
                </label>
                <Input
                  className="mt-1"
                  value={ssoAuthority}
                  onChange={(e) => setSsoAuthority(e.target.value)}
                  disabled={ssoBusy}
                  placeholder="https://login.example.com"
                />
              </div>
              <div>
                <label className="text-xs font-medium text-[var(--color-muted)]">
                  {t("tenantUsage.ssoClientId")}
                </label>
                <Input
                  className="mt-1"
                  value={ssoClientId}
                  onChange={(e) => setSsoClientId(e.target.value)}
                  disabled={ssoBusy}
                />
              </div>
              <div>
                <label className="text-xs font-medium text-[var(--color-muted)]">
                  {t("tenantUsage.ssoClientSecret")}
                </label>
                <Input
                  className="mt-1"
                  type="password"
                  value={ssoClientSecret}
                  onChange={(e) => setSsoClientSecret(e.target.value)}
                  disabled={ssoBusy}
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
              <Button variant="secondary" disabled={ssoBusy} onClick={() => void handleSaveSso()}>
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
                  <div key={inv.id} className="flex flex-wrap justify-between gap-2 border-b border-[var(--color-border)] py-2">
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
