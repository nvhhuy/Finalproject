"use client";

import Link from "next/link";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { PageContent, PageHeader } from "@/components/ui/page-header";
import { formatDateTimeOrDash } from "@/lib/shared";
import { useNotifications } from "@/lib/features/notifications/notifications-context";

export default function NotificationsPage() {
  const { t } = useTranslation();
  const { notifications, markRead, markAllRead } = useNotifications();

  return (
    <PageContent>
      <PageHeader title={t("notifications.pageTitle")} description={t("notifications.pageDescription")} />
      <Card>
        <CardHeader className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
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
