"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { PageContent, PageHeader } from "@/components/ui/page-header";
import { PageSkeleton } from "@/components/ui/skeleton";
import { formatDateTimeOrDash } from "@/lib/shared";
import { notificationsApi, type NotificationDto } from "@/lib/features/notifications/notifications-api";

export default function NotificationsPage() {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(true);
  const [notifications, setNotifications] = useState<NotificationDto[]>([]);

  const load = useCallback(async () => {
    setLoading(true);
    const res = await notificationsApi.list(1, 50);
    if (res.success && res.data) setNotifications(res.data);
    setLoading(false);
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function markRead(id: string) {
    await notificationsApi.markRead(id);
    setNotifications((prev) => prev.map((n) => (n.id === id ? { ...n, readAtUtc: new Date().toISOString() } : n)));
  }

  async function markAllRead() {
    const unread = notifications.filter((n) => !n.readAtUtc);
    await Promise.all(unread.map((n) => notificationsApi.markRead(n.id)));
    setNotifications((prev) => prev.map((n) => ({ ...n, readAtUtc: n.readAtUtc ?? new Date().toISOString() })));
  }

  if (loading) {
    return (
      <PageContent>
        <PageHeader title={t("notifications.pageTitle")} description={t("notifications.pageDescription")} />
        <PageSkeleton />
      </PageContent>
    );
  }

  return (
    <PageContent>
      <PageHeader title={t("notifications.pageTitle")} description={t("notifications.pageDescription")} />
      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-2">
          <CardTitle>{t("notifications.title")}</CardTitle>
          <Button
            variant="secondary"
            size="sm"
            disabled={notifications.every((n) => n.readAtUtc)}
            onClick={() => void markAllRead()}
          >
            {t("notifications.markAllRead")}
          </Button>
        </CardHeader>
        <CardBody className="space-y-2">
          {notifications.length === 0 ? (
            <p className="text-sm text-[var(--color-muted)]">{t("notifications.empty")}</p>
          ) : (
            notifications.map((n) => (
              <div
                key={n.id}
                className="flex items-start justify-between gap-3 rounded-lg border border-[var(--color-border)] px-3 py-2"
              >
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-semibold">{n.title}</p>
                  <p className="text-sm text-[var(--color-muted)]">{n.body}</p>
                  <p className="mt-1 text-xs text-[var(--color-muted)]">{formatDateTimeOrDash(n.createdAtUtc)}</p>
                  {n.linkUrl ? (
                    <Link href={n.linkUrl} className="mt-1 inline-block text-xs text-[var(--color-primary)]">
                      {t("notifications.open")}
                    </Link>
                  ) : null}
                </div>
                {!n.readAtUtc ? (
                  <Button variant="ghost" size="sm" onClick={() => void markRead(n.id)}>
                    {t("notifications.markRead")}
                  </Button>
                ) : null}
              </div>
            ))
          )}
        </CardBody>
      </Card>
    </PageContent>
  );
}
