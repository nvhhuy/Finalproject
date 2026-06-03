"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardDescription, CardTitle } from "@/components/ui/card";
import { PageContent, PageHeader } from "@/components/ui/page-header";
import { notificationsApi } from "@/lib/features/notifications/notifications-api";
import { usePwaInstall, registerServiceWorker } from "@/lib/pwa/install-prompt";
import {
  pushSubscriptionToPayload,
  subscribeToWebPush,
  unsubscribeFromWebPush,
} from "@/lib/pwa/push-subscription";

export default function SettingsPage() {
  const { t } = useTranslation();
  const { canPromptInstall, isStandalone, promptInstall } = usePwaInstall();
  const [pushEnabled, setPushEnabled] = useState(false);
  const [pushBusy, setPushBusy] = useState(false);
  const [pushMessage, setPushMessage] = useState<string | null>(null);
  const [swReady, setSwReady] = useState(false);

  useEffect(() => {
    registerServiceWorker();
    if ("serviceWorker" in navigator) {
      void navigator.serviceWorker.ready.then(() => setSwReady(true));
    }
  }, []);

  useEffect(() => {
    async function detectPush() {
      if (!("serviceWorker" in navigator)) return;
      const reg = await navigator.serviceWorker.ready;
      const sub = await reg.pushManager.getSubscription();
      setPushEnabled(Boolean(sub));
    }
    void detectPush();
  }, []);

  const enablePush = useCallback(async () => {
    setPushBusy(true);
    setPushMessage(null);
    try {
      const perm = await Notification.requestPermission();
      if (perm !== "granted") {
        setPushMessage(t("settings.pushDenied"));
        return;
      }

      const keyRes = await notificationsApi.vapidPublicKey();
      if (!keyRes.success || !keyRes.data) {
        setPushMessage(t("settings.pushUnavailable"));
        return;
      }

      const sub = await subscribeToWebPush(keyRes.data);
      if (!sub) {
        setPushMessage(t("settings.pushUnavailable"));
        return;
      }

      const payload = pushSubscriptionToPayload(sub);
      const res = await notificationsApi.subscribePush(payload);
      if (!res.success) {
        setPushMessage(t("settings.pushError"));
        return;
      }

      setPushEnabled(true);
      setPushMessage(t("settings.pushEnabled"));
    } catch {
      setPushMessage(t("settings.pushError"));
    } finally {
      setPushBusy(false);
    }
  }, [t]);

  const disablePush = useCallback(async () => {
    setPushBusy(true);
    setPushMessage(null);
    try {
      const sub = await unsubscribeFromWebPush();
      if (sub) {
        const endpoint = sub.endpoint;
        await notificationsApi.unsubscribePush(endpoint);
      }
      setPushEnabled(false);
      setPushMessage(t("settings.pushDisabled"));
    } catch {
      setPushMessage(t("settings.pushError"));
    } finally {
      setPushBusy(false);
    }
  }, [t]);

  return (
    <PageContent>
      <PageHeader title={t("settings.title")} description={t("settings.subtitle")} />

      <div className="grid gap-4">
        <Card>
          <CardBody>
            <CardTitle>{t("settings.installTitle")}</CardTitle>
            <CardDescription className="mt-2">{t("settings.installHint")}</CardDescription>
            {isStandalone ? (
              <p className="mt-4 text-sm text-[var(--color-primary)]">{t("settings.alreadyInstalled")}</p>
            ) : (
              <div className="mt-4 flex flex-wrap gap-2">
                {canPromptInstall ? (
                  <Button type="button" onClick={() => void promptInstall()}>
                    {t("settings.installButton")}
                  </Button>
                ) : (
                  <p className="text-sm text-[var(--color-muted)]">{t("settings.installManual")}</p>
                )}
              </div>
            )}
            <details className="mt-4 text-sm text-[var(--color-muted)]">
              <summary className="cursor-pointer font-medium text-[var(--color-foreground)]">
                {t("settings.installIosTitle")}
              </summary>
              <ol className="mt-2 list-decimal space-y-1 pl-5">
                <li>{t("settings.installIosStep1")}</li>
                <li>{t("settings.installIosStep2")}</li>
              </ol>
            </details>
          </CardBody>
        </Card>

        <Card>
          <CardBody>
            <CardTitle>{t("settings.pushTitle")}</CardTitle>
            <CardDescription className="mt-2">{t("settings.pushHint")}</CardDescription>
            {!swReady ? (
              <p className="mt-4 text-sm text-[var(--color-muted)]">{t("common.loading")}</p>
            ) : (
              <div className="mt-4 flex flex-wrap items-center gap-2">
                {pushEnabled ? (
                  <Button type="button" variant="secondary" disabled={pushBusy} onClick={() => void disablePush()}>
                    {t("settings.pushDisable")}
                  </Button>
                ) : (
                  <Button type="button" disabled={pushBusy} onClick={() => void enablePush()}>
                    {t("settings.pushEnable")}
                  </Button>
                )}
              </div>
            )}
            {pushMessage ? <p className="mt-3 text-sm text-[var(--color-muted)]">{pushMessage}</p> : null}
          </CardBody>
        </Card>
      </div>
    </PageContent>
  );
}
