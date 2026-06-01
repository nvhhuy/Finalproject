"use client";

import type { HubConnection } from "@microsoft/signalr";
import Link from "next/link";
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/lib/auth/auth-context";
import { loadTokens } from "@/lib/auth/tokens";
import { notificationsApi, type NotificationDto } from "./notifications-api";
import { startNotificationsHub, type LiveNotificationPayload } from "./notifications-realtime";

type NotificationsContextValue = {
  notifications: NotificationDto[];
  unreadCount: number;
  markRead: (id: string) => Promise<void>;
  markAllRead: () => Promise<void>;
};

const NotificationsContext = createContext<NotificationsContextValue | null>(null);

type NotificationToast = {
  id: string;
  title: string;
  body: string;
};

const TODAY_SENTINEL = "__today__";

function formatGroupDate(value: string, locale: string) {
  const date = new Date(value);
  const now = new Date();
  const sameDay = date.toDateString() === now.toDateString();
  if (sameDay) return TODAY_SENTINEL;
  return new Intl.DateTimeFormat(locale, { day: "2-digit", month: "short", year: "numeric" }).format(date);
}

export function NotificationsProvider({ children }: { children: React.ReactNode }) {
  const { t, i18n } = useTranslation();
  const auth = useAuth();
  const [notifications, setNotifications] = useState<NotificationDto[]>([]);
  const [open, setOpen] = useState(false);
  const [toasts, setToasts] = useState<NotificationToast[]>([]);
  const connectionRef = useRef<HubConnection | null>(null);

  const unreadCount = useMemo(() => notifications.filter((n) => !n.readAtUtc).length, [notifications]);
  const groupedNotifications = useMemo(() => {
    const groups = new Map<string, NotificationDto[]>();
    for (const item of notifications) {
      const key = formatGroupDate(item.createdAtUtc, i18n.language || "en");
      const existing = groups.get(key) ?? [];
      existing.push(item);
      groups.set(key, existing);
    }
    return Array.from(groups.entries());
  }, [notifications, i18n.language]);

  const markRead = useCallback(async (id: string) => {
    const res = await notificationsApi.markRead(id);
    if (!res.success) return;
    setNotifications((prev) => prev.map((n) => (n.id === id ? { ...n, readAtUtc: new Date().toISOString() } : n)));
  }, []);

  const markAllRead = useCallback(async () => {
    const unread = notifications.filter((n) => !n.readAtUtc).map((n) => n.id);
    if (unread.length === 0) return;
    await Promise.all(unread.map((id) => notificationsApi.markRead(id)));
    setNotifications((prev) => prev.map((n) => ({ ...n, readAtUtc: n.readAtUtc ?? new Date().toISOString() })));
  }, [notifications]);

  const handleLiveNotification = useCallback((payload: LiveNotificationPayload) => {
    setNotifications((prev) => {
      const item: NotificationDto = {
        id: payload.id,
        title: payload.title,
        body: payload.body,
        linkUrl: payload.linkUrl ?? null,
        createdAtUtc: payload.createdAtUtc,
        readAtUtc: null,
      };
      const next = [item, ...prev.filter((n) => n.id !== item.id)];
      return next.slice(0, 50);
    });
    setToasts((prev) => [{ id: payload.id, title: payload.title, body: payload.body }, ...prev].slice(0, 3));
  }, []);

  useEffect(() => {
    if (!auth.hydrated || !auth.isAuthenticated) return;

    let cancelled = false;
    let activeConn: HubConnection | null = null;
    let pollTimer: ReturnType<typeof setInterval> | undefined;

    async function refreshList() {
      const list = await notificationsApi.list(1, 20);
      if (!cancelled && list.success && list.data) setNotifications(list.data);
    }

    async function boot() {
      const tokens = loadTokens();
      if (!tokens?.accessToken) return;

      await refreshList();

      // Defer past React Strict Mode's first effect cleanup (avoids aborted negotiate / console noise).
      await new Promise<void>((resolve) => {
        requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
      });
      if (cancelled) return;

      const conn = await startNotificationsHub(tokens.accessToken, handleLiveNotification);
      if (cancelled) {
        if (conn) void conn.stop();
        return;
      }
      if (conn) {
        activeConn = conn;
        connectionRef.current = conn;
        return;
      }

      // Realtime unavailable — poll REST so the bell still updates.
      pollTimer = setInterval(() => {
        void refreshList();
      }, 60_000);
    }

    void boot();
    return () => {
      cancelled = true;
      if (pollTimer) clearInterval(pollTimer);
      const conn = activeConn ?? connectionRef.current;
      connectionRef.current = null;
      activeConn = null;
      if (conn) void conn.stop();
    };
  }, [auth.hydrated, auth.isAuthenticated, handleLiveNotification]);

  useEffect(() => {
    if (toasts.length === 0) return;
    const timer = window.setTimeout(() => {
      setToasts((prev) => prev.slice(0, -1));
    }, 4500);
    return () => window.clearTimeout(timer);
  }, [toasts]);

  const value = useMemo(
    () => ({
      notifications,
      unreadCount,
      markRead,
      markAllRead,
    }),
    [notifications, unreadCount, markRead, markAllRead],
  );

  return (
    <NotificationsContext.Provider value={value}>
      {children}
      {auth.hydrated && auth.isAuthenticated ? (
        <div className="fixed right-4 bottom-4 z-50">
          <div className="mb-2 flex max-w-80 flex-col gap-2">
            {toasts.map((toast) => (
              <div key={toast.id} className="rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 shadow-[var(--shadow-elevated)]">
                <p className="truncate text-sm font-semibold">{toast.title}</p>
                <p className="truncate text-xs text-[var(--color-muted)]">{toast.body}</p>
              </div>
            ))}
          </div>
          <Button variant="secondary" size="sm" onClick={() => setOpen((v) => !v)} aria-label={t("notifications.toggleAria")}>
            <span className="mr-2">🔔</span>
            <span>{t("notifications.title")}</span>
            {unreadCount > 0 ? (
              <span className="ml-2 inline-flex min-w-5 items-center justify-center rounded-full bg-[var(--color-primary)] px-1.5 py-0.5 text-xs font-semibold text-white">
                {unreadCount > 99 ? "99+" : unreadCount}
              </span>
            ) : null}
          </Button>
          {open ? (
            <div className="mt-2 w-80 max-h-96 overflow-auto rounded-xl border border-[var(--color-border)] bg-[var(--color-surface)] p-2 shadow-[var(--shadow-elevated)]">
              <div className="mb-2 flex items-center justify-between px-1">
                <Link href="/app/notifications" className="text-sm font-semibold hover:text-[var(--color-primary)]">
                  {t("notifications.title")}
                </Link>
                <button
                  type="button"
                  className="text-xs text-[var(--color-primary)] disabled:text-[var(--color-muted)]"
                  onClick={() => void markAllRead()}
                  disabled={unreadCount === 0}
                >
                  {t("notifications.markAllRead")}
                </button>
              </div>
              {notifications.length === 0 ? (
                <p className="px-2 py-3 text-sm text-[var(--color-muted)]">{t("notifications.empty")}</p>
              ) : (
                groupedNotifications.map(([group, items]) => (
                  <div key={group} className="mb-2">
                    <p className="px-2 pb-1 text-xs font-semibold uppercase tracking-wide text-[var(--color-muted)]">
                      {group === TODAY_SENTINEL ? t("notifications.today") : group}
                    </p>
                    {items.map((n) => (
                      <div key={n.id} className="mb-1 rounded-lg px-2 py-2 hover:bg-[var(--color-surface-muted)]">
                        <div className="flex items-start gap-2">
                          {!n.readAtUtc ? <span className="mt-1 inline-block h-2 w-2 shrink-0 rounded-full bg-[var(--color-primary)]" /> : null}
                          <div className="min-w-0 flex-1">
                            <p className="truncate text-sm font-semibold">{n.title}</p>
                            <p className="text-sm text-[var(--color-muted)]">{n.body}</p>
                            {n.linkUrl ? (
                              <Link href={n.linkUrl} className="text-xs text-[var(--color-primary)]">
                                {t("notifications.open")}
                              </Link>
                            ) : null}
                          </div>
                          {!n.readAtUtc ? (
                            <button
                              type="button"
                              className="text-xs text-[var(--color-muted)] hover:text-[var(--color-foreground)]"
                              onClick={() => void markRead(n.id)}
                            >
                              {t("notifications.markRead")}
                            </button>
                          ) : null}
                        </div>
                      </div>
                    ))}
                  </div>
                ))
              )}
            </div>
          ) : null}
        </div>
      ) : null}
    </NotificationsContext.Provider>
  );
}

export function useNotifications() {
  const ctx = useContext(NotificationsContext);
  if (!ctx) throw new Error("useNotifications must be used within NotificationsProvider");
  return ctx;
}
