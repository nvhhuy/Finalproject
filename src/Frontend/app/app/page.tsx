"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardDescription, CardTitle } from "@/components/ui/card";
import { PageContent, PageHeader } from "@/components/ui/page-header";
import { CardSkeleton } from "@/components/ui/skeleton";
import { useAuth } from "@/lib/auth/auth-context";
import { billingApi } from "@/lib/features/billing/billing-api";
import { notificationsApi } from "@/lib/features/notifications/notifications-api";
import { supportApi } from "@/lib/features/support/support-api";
import { walletApi } from "@/lib/features/wallet/wallet-api";
import { formatRelativeTime } from "@/lib/shared";
import { getActiveTenantId } from "@/lib/tenancy/tenant-context";

export default function DashboardPage() {
  const { t, i18n } = useTranslation();
  const auth = useAuth();

  const balanceQuery = useQuery({
    queryKey: ["wallet", "balance"],
    queryFn: async () => {
      const res = await walletApi.getBalance();
      if (!res.success || !res.data) return null;
      return res.data.balanceCredits;
    },
  });

  const ticketsQuery = useQuery({
    queryKey: ["support", "open-count"],
    queryFn: async () => {
      const res = await supportApi.list(1, 1, undefined, "Open");
      if (!res.success || !res.data) return 0;
      return res.data.totalCount;
    },
  });

  const unreadQuery = useQuery({
    queryKey: ["notifications", "unread-count"],
    queryFn: async () => {
      const res = await notificationsApi.unreadCount();
      if (!res.success || res.data === undefined) return 0;
      return res.data;
    },
  });

  const recentNotificationsQuery = useQuery({
    queryKey: ["notifications", "recent"],
    queryFn: async () => {
      const res = await notificationsApi.list(1, 5);
      if (!res.success || !res.data) return [];
      return res.data;
    },
  });

  const planQuery = useQuery({
    queryKey: ["billing", "active-plan"],
    queryFn: async () => {
      const tenantsRes = await billingApi.listMyTenants();
      const tenantId = getActiveTenantId() ?? tenantsRes.data?.[0]?.id;
      if (!tenantId) return null;
      const ent = await billingApi.getTenantEntitlements(tenantId);
      return ent.success && ent.data ? ent.data.subscription.planName : null;
    },
  });

  const loading =
    balanceQuery.isLoading ||
    ticketsQuery.isLoading ||
    planQuery.isLoading ||
    unreadQuery.isLoading;

  const quickLinks = [
    { href: "/app/profile", label: t("dashboard.quickProfile") },
    { href: "/app/settings", label: t("nav.settings") },
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
            <CardSkeleton lines={2} />
          </>
        ) : (
          <>
            <Card>
              <CardBody>
                <CardTitle>{t("wallet.balance")}</CardTitle>
                <p className="mt-2 text-2xl font-semibold">{balanceQuery.data ?? 0}</p>
                <Link href="/app/wallet" className="mt-2 inline-block text-sm text-[var(--color-primary)]">
                  {t("dashboard.viewWallet")} →
                </Link>
              </CardBody>
            </Card>
            <Card>
              <CardBody>
                <CardTitle>{t("dashboard.orgSummary")}</CardTitle>
                <p className="mt-2 text-sm text-[var(--color-muted)]">
                  {planQuery.data ? t("dashboard.activePlan", { plan: planQuery.data }) : t("dashboard.noTenant")}
                </p>
                <p className="mt-1 text-sm text-[var(--color-muted)]">
                  {t("dashboard.openTickets", { count: ticketsQuery.data ?? 0 })}
                </p>
                <Link href="/app/tenant" className="mt-2 inline-block text-sm text-[var(--color-primary)]">
                  {t("dashboard.viewTenant")} →
                </Link>
              </CardBody>
            </Card>
            <Card>
              <CardBody>
                <CardTitle>{t("dashboard.notificationsTitle")}</CardTitle>
                <p className="mt-2 text-2xl font-semibold">{unreadQuery.data ?? 0}</p>
                <p className="text-sm text-[var(--color-muted)]">{t("dashboard.unreadLabel")}</p>
                <Link
                  href="/app/notifications"
                  className="mt-2 inline-block text-sm text-[var(--color-primary)]"
                >
                  {t("dashboard.viewNotifications")} →
                </Link>
              </CardBody>
            </Card>
          </>
        )}
      </div>

      {!recentNotificationsQuery.isLoading && (recentNotificationsQuery.data?.length ?? 0) > 0 ? (
        <Card className="mt-4">
          <CardBody>
            <CardTitle>{t("dashboard.recentNotifications")}</CardTitle>
            <ul className="mt-3 space-y-3">
              {recentNotificationsQuery.data?.map((n) => (
                <li key={n.id} className="border-b border-[var(--color-border)] pb-3 last:border-0 last:pb-0">
                  <p className="font-medium text-[var(--color-foreground)]">{n.title}</p>
                  <p className="text-sm text-[var(--color-muted)] line-clamp-2">{n.body}</p>
                  <p className="mt-1 text-xs text-[var(--color-muted)]">
                    {formatRelativeTime(n.createdAtUtc)}
                    {!n.readAtUtc ? ` · ${t("dashboard.unreadBadge")}` : ""}
                  </p>
                </li>
              ))}
            </ul>
          </CardBody>
        </Card>
      ) : null}

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
