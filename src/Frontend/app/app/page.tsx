"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardDescription, CardTitle } from "@/components/ui/card";
import { PageContent, PageHeader } from "@/components/ui/page-header";
import { CardSkeleton } from "@/components/ui/skeleton";
import { useAuth } from "@/lib/auth/auth-context";
import { billingApi } from "@/lib/features/billing/billing-api";
import { supportApi } from "@/lib/features/support/support-api";
import { walletApi } from "@/lib/features/wallet/wallet-api";
import { getActiveTenantId } from "@/lib/tenancy/tenant-context";

export default function DashboardPage() {
  const { t } = useTranslation();
  const auth = useAuth();
  const [loading, setLoading] = useState(true);
  const [balance, setBalance] = useState<number | null>(null);
  const [openTickets, setOpenTickets] = useState<number | null>(null);
  const [planName, setPlanName] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      const [balRes, ticketsRes, tenantsRes] = await Promise.all([
        walletApi.getBalance(),
        supportApi.list(1, 1, undefined, "Open"),
        billingApi.listMyTenants(),
      ]);
      if (balRes.success && balRes.data) setBalance(balRes.data.balanceCredits);
      if (ticketsRes.success && ticketsRes.data) setOpenTickets(ticketsRes.data.totalCount);

      const tenantId = getActiveTenantId() ?? tenantsRes.data?.[0]?.id;
      if (tenantId) {
        const ent = await billingApi.getTenantEntitlements(tenantId);
        if (ent.success && ent.data) setPlanName(ent.data.subscription.planName);
      }
      setLoading(false);
    })();
  }, []);

  const quickLinks = [
    { href: "/app/profile", label: t("dashboard.quickProfile") },
    { href: "/app/security/2fa", label: t("dashboard.quick2fa") },
    { href: "/app/media", label: t("dashboard.quickMedia") },
    { href: "/app/support", label: t("nav.support") },
    { href: "/app/notifications", label: t("nav.notifications") },
  ];

  return (
    <PageContent>
      <PageHeader title={t("dashboard.title")} description={t("dashboard.subtitle")} />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Card>
          <CardBody>
            <CardTitle>{t("dashboard.signedInAs")}</CardTitle>
            <CardDescription className="mt-3">
              <span className="text-base font-semibold text-[var(--color-foreground)]">
                {auth.user?.firstName} {auth.user?.lastName}
              </span>
              <span> ({auth.user?.userName})</span>
            </CardDescription>
            <p className="mt-2 text-sm text-[var(--color-muted)]">{auth.user?.email}</p>
            <Link href="/forgot-password" className="mt-2 inline-block text-sm text-[var(--color-primary)]">
              {t("profile.changePassword")}
            </Link>
          </CardBody>
        </Card>

        {loading ? (
          <>
            <CardSkeleton lines={2} />
            <CardSkeleton lines={2} />
          </>
        ) : (
          <>
            <Card>
              <CardBody>
                <CardTitle>{t("wallet.balance")}</CardTitle>
                <p className="mt-2 text-2xl font-semibold">{balance ?? 0}</p>
                <Link href="/app/wallet" className="mt-2 inline-block text-sm text-[var(--color-primary)]">
                  {t("dashboard.viewWallet")} →
                </Link>
              </CardBody>
            </Card>
            <Card>
              <CardBody>
                <CardTitle>{t("dashboard.orgSummary")}</CardTitle>
                <p className="mt-2 text-sm text-[var(--color-muted)]">
                  {planName ? t("dashboard.activePlan", { plan: planName }) : t("dashboard.noTenant")}
                </p>
                <p className="mt-1 text-sm text-[var(--color-muted)]">
                  {t("dashboard.openTickets", { count: openTickets ?? 0 })}
                </p>
                <Link href="/app/tenant" className="mt-2 inline-block text-sm text-[var(--color-primary)]">
                  {t("dashboard.viewTenant")} →
                </Link>
              </CardBody>
            </Card>
          </>
        )}
      </div>

      <Card className="mt-4">
        <CardBody>
          <CardTitle>{t("landing.featuresTitle")}</CardTitle>
          <CardDescription className="mt-3">{t("dashboard.tip")}</CardDescription>
          <div className="mt-4 grid gap-2 sm:grid-cols-2">
            {quickLinks.map((link) => (
              <Link key={link.href} href={link.href}>
                <Button variant="secondary" className="w-full justify-start">
                  {link.label} →
                </Button>
              </Link>
            ))}
          </div>
        </CardBody>
      </Card>
    </PageContent>
  );
}
