"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Suspense, useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { LazyQrCode } from "@/lib/ui/lazy-components";
import { useAuth } from "@/lib/auth/auth-context";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PageContent, PageHeader } from "@/components/ui/page-header";
import { ProfileSkeleton, Skeleton } from "@/components/ui/skeleton";
import { authApi } from "@/lib/features/auth/auth-api";
import { userApi } from "@/lib/features/users/user-api";
import { mediaApi } from "@/lib/features/media/media-api";
import { notificationsApi } from "@/lib/features/notifications/notifications-api";
import { usePwaInstall, registerServiceWorker } from "@/lib/pwa/install-prompt";
import {
  pushSubscriptionToPayload,
  subscribeToWebPush,
  unsubscribeFromWebPush,
} from "@/lib/pwa/push-subscription";
import { getApiErrorMessageFromPayload, getNetworkErrorMessage, isConcurrencyError } from "@/lib/errors";
import { AvatarImage } from "@/components/media/avatar-image";
import { cn } from "@/lib/ui/cn";

type AccountTab = "profile" | "security" | "preferences";

const TABS: AccountTab[] = ["profile", "security", "preferences"];

function AccountPageContent() {
  const { t } = useTranslation();
  const router = useRouter();
  const params = useSearchParams();
  const tab = (TABS.includes(params.get("tab") as AccountTab) ? params.get("tab") : "profile") as AccountTab;
  const { reloadProfile } = useAuth();
  const qc = useQueryClient();

  const profileQuery = useQuery({
    queryKey: ["me", "profile"],
    queryFn: async () => {
      const res = await userApi.getMyProfile();
      if (!res.success || !res.data) throw new Error(getApiErrorMessageFromPayload(res.error));
      return res.data;
    },
  });

  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [resendMsg, setResendMsg] = useState<string | null>(null);
  const [avatarBusy, setAvatarBusy] = useState(false);
  const [avatarError, setAvatarError] = useState<string | null>(null);

  const [setup, setSetup] = useState<{ sharedKey: string; otpAuthUri: string } | null>(null);
  const [twoFaCode, setTwoFaCode] = useState("");
  const [disableCode, setDisableCode] = useState("");
  const [securityMsg, setSecurityMsg] = useState<string | null>(null);
  const [securityOk, setSecurityOk] = useState(false);

  const { canPromptInstall, isStandalone, promptInstall } = usePwaInstall();
  const [pushEnabled, setPushEnabled] = useState(false);
  const [pushBusy, setPushBusy] = useState(false);
  const [pushMessage, setPushMessage] = useState<string | null>(null);
  const [swReady, setSwReady] = useState(false);

  const profile = profileQuery.data;

  useEffect(() => {
    if (profile) {
      setFirstName(profile.firstName);
      setLastName(profile.lastName);
    }
  }, [profile]);

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

  const setTab = (next: AccountTab) => {
    router.replace(`/app/account?tab=${next}`);
  };

  const updateMutation = useMutation({
    mutationFn: async () => {
      if (!profile) return;
      const res = await userApi.updateMyProfile({
        firstName: firstName.trim() || profile.firstName,
        lastName: lastName.trim() || profile.lastName,
        rowVersion: profile.rowVersion,
      });
      if (!res.success) throw new Error(getApiErrorMessageFromPayload(res.error));
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ["me", "profile"] });
      await reloadProfile();
    },
  });

  async function uploadAvatar(file: File) {
    if (!profile) return;
    setAvatarBusy(true);
    setAvatarError(null);
    try {
      const init = await mediaApi.initiate(file);
      const uploaded = await mediaApi.putToPresignedUrl(init.uploadUrl, file, file.type);
      if (!uploaded) throw new Error(t("errors.requestFailed"));
      await mediaApi.complete(init.mediaAssetId);
      const res = await userApi.setMyAvatar({
        mediaAssetId: init.mediaAssetId,
        rowVersion: profile.rowVersion,
      });
      if (!res.success) throw new Error(getApiErrorMessageFromPayload(res.error));
      await qc.invalidateQueries({ queryKey: ["me", "profile"] });
      await reloadProfile();
    } catch (e) {
      setAvatarError(e instanceof Error ? e.message : t("errors.requestFailed"));
    } finally {
      setAvatarBusy(false);
    }
  }

  async function resendVerification() {
    if (!profile?.email) return;
    const res = await authApi.resendVerification(profile.email);
    setResendMsg(res.success ? t("profile.resendSent") : getApiErrorMessageFromPayload(res.error));
  }

  async function beginTwoFactor() {
    setSecurityMsg(null);
    setSecurityOk(false);
    const res = await authApi.beginTwoFactor();
    if (!res.success || !res.data) {
      setSecurityMsg(getApiErrorMessageFromPayload(res.error, "twoFa.beginFailed"));
      return;
    }
    setSetup(res.data);
  }

  async function confirmTwoFactor() {
    setSecurityMsg(null);
    const res = await authApi.confirmTwoFactor(twoFaCode);
    if (!res.success) {
      setSecurityMsg(getApiErrorMessageFromPayload(res.error, "twoFa.confirmFailed"));
      setSecurityOk(false);
      return;
    }
    setSecurityMsg(t("twoFa.enabledMsg"));
    setSecurityOk(true);
    setSetup(null);
    setTwoFaCode("");
    await profileQuery.refetch();
  }

  async function disableTwoFactor() {
    setSecurityMsg(null);
    const res = await authApi.disableTwoFactor(disableCode);
    if (!res.success) {
      setSecurityMsg(getApiErrorMessageFromPayload(res.error, "twoFa.disableFailed"));
      setSecurityOk(false);
      return;
    }
    setSecurityMsg(t("twoFa.disabledMsg"));
    setSecurityOk(true);
    setDisableCode("");
    await profileQuery.refetch();
  }

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
      const res = await notificationsApi.subscribePush(pushSubscriptionToPayload(sub));
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
      if (sub) await notificationsApi.unsubscribePush(sub.endpoint);
      setPushEnabled(false);
      setPushMessage(t("settings.pushDisabled"));
    } catch {
      setPushMessage(t("settings.pushError"));
    } finally {
      setPushBusy(false);
    }
  }, [t]);

  if (profileQuery.isLoading) {
    return (
      <PageContent>
        <PageHeader title={t("account.title")} />
        <ProfileSkeleton />
      </PageContent>
    );
  }

  const concurrency = updateMutation.error && isConcurrencyError(updateMutation.error);

  return (
    <PageContent>
      <PageHeader title={t("account.title")} description={t("account.subtitle")} />

      <nav className="mb-6 flex flex-wrap gap-2 border-b border-[var(--color-border)] pb-3">
        {TABS.map((id) => (
          <button
            key={id}
            type="button"
            onClick={() => setTab(id)}
            className={cn(
              "rounded-lg px-3 py-2 text-sm font-medium transition",
              tab === id
                ? "bg-[var(--color-primary-soft)] text-[var(--color-primary)]"
                : "text-[var(--color-muted)] hover:bg-[var(--color-surface-muted)] hover:text-[var(--color-foreground)]",
            )}
          >
            {t(`account.tabs.${id}`)}
          </button>
        ))}
      </nav>

      {tab === "profile" ? (
        <div className="grid gap-4">
          <Card>
            <CardHeader>
              <CardTitle>{t("profile.account")}</CardTitle>
              {profile ? (
                <CardDescription>
                  <span className="font-medium text-[var(--color-foreground)]">{profile.userName}</span>
                  <span> · {profile.email}</span>
                </CardDescription>
              ) : null}
            </CardHeader>
            <CardBody className="grid gap-5">
              {profile ? (
                <>
                  <div className="flex flex-wrap items-center gap-4">
                    {profile.avatarMediaAssetId || profile.avatarUrl ? (
                      <AvatarImage
                        mediaAssetId={profile.avatarMediaAssetId}
                        fallbackUrl={profile.avatarUrl}
                        cacheKey={profile.rowVersion}
                        className="h-16 w-16 rounded-full border-2 border-[var(--color-border)] object-cover shadow-sm"
                      />
                    ) : (
                      <span className="flex h-16 w-16 items-center justify-center rounded-full bg-[var(--color-primary)] text-lg font-semibold text-white">
                        {profile.firstName?.[0]}
                        {profile.lastName?.[0]}
                      </span>
                    )}
                    <label className="cursor-pointer text-sm font-medium text-[var(--color-primary)] hover:underline">
                      {avatarBusy ? t("common.loading") : t("profile.avatarTitle")}
                      <input
                        type="file"
                        accept="image/*"
                        className="sr-only"
                        disabled={avatarBusy}
                        onChange={(e) => {
                          const file = e.target.files?.[0];
                          if (file) void uploadAvatar(file);
                        }}
                      />
                    </label>
                  </div>
                  {avatarError ? <Alert variant="error">{avatarError}</Alert> : null}
                  <div className="grid gap-4 sm:grid-cols-2">
                    <div className="grid gap-2">
                      <Label htmlFor="firstName">{t("profile.firstName")}</Label>
                      <Input id="firstName" value={firstName} onChange={(e) => setFirstName(e.target.value)} />
                    </div>
                    <div className="grid gap-2">
                      <Label htmlFor="lastName">{t("profile.lastName")}</Label>
                      <Input id="lastName" value={lastName} onChange={(e) => setLastName(e.target.value)} />
                    </div>
                  </div>
                  <div className="flex flex-wrap items-center gap-3">
                    <Button onClick={() => updateMutation.mutate()} loading={updateMutation.isPending}>
                      {t("profile.save")}
                    </Button>
                    <Button variant="secondary" onClick={resendVerification}>
                      {t("profile.resendVerification")}
                    </Button>
                    <Link href="/forgot-password">
                      <Button variant="ghost">{t("profile.changePassword")}</Button>
                    </Link>
                    {updateMutation.isSuccess ? <Badge variant="success">{t("common.saved")}</Badge> : null}
                    {resendMsg ? <span className="text-sm text-[var(--color-muted)]">{resendMsg}</span> : null}
                  </div>
                  {updateMutation.isError ? (
                    <Alert variant="error">
                      {concurrency ? t("errors.concurrencyConflict") : (updateMutation.error as Error).message}
                    </Alert>
                  ) : null}
                </>
              ) : null}
            </CardBody>
          </Card>
        </div>
      ) : null}

      {tab === "security" ? (
        <div className="grid gap-4">
          <Card>
            <CardHeader>
              <CardTitle>{t("twoFa.status")}</CardTitle>
              <CardDescription>{t("account.securityHint")}</CardDescription>
            </CardHeader>
            <CardBody className="grid gap-4">
              {profileQuery.isLoading ? (
                <Skeleton className="h-6 w-24 rounded-full" />
              ) : (
                <Badge variant={profile?.twoFactorEnabled ? "success" : "warning"}>
                  {profile?.twoFactorEnabled ? t("common.enabled") : t("common.disabled")}
                </Badge>
              )}

              {profile?.twoFactorEnabled ? (
                <div className="grid max-w-sm gap-2">
                  <Label htmlFor="disableCode">{t("twoFa.disableCode")}</Label>
                  <Input
                    id="disableCode"
                    value={disableCode}
                    onChange={(e) => setDisableCode(e.target.value)}
                    placeholder="123456"
                  />
                  <Button variant="danger" onClick={disableTwoFactor} disabled={disableCode.length < 6}>
                    {t("twoFa.disable")}
                  </Button>
                </div>
              ) : !setup ? (
                <Button onClick={beginTwoFactor}>{t("twoFa.begin")}</Button>
              ) : (
                <div className="grid gap-6 lg:grid-cols-[auto_1fr] lg:items-start">
                  <div className="grid gap-3">
                    <p className="text-sm font-medium">{t("twoFa.scanQr")}</p>
                    <LazyQrCode value={setup.otpAuthUri} />
                  </div>
                  <div className="grid gap-4">
                    <div className="rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-muted)] p-3">
                      <p className="text-xs font-medium text-[var(--color-muted)]">{t("twoFa.sharedKey")}</p>
                      <p className="mt-1.5 break-all font-mono text-sm">{setup.sharedKey}</p>
                    </div>
                    <div className="grid gap-2">
                      <Label htmlFor="code">{t("twoFa.code")}</Label>
                      <Input id="code" value={twoFaCode} onChange={(e) => setTwoFaCode(e.target.value)} />
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <Button onClick={confirmTwoFactor} disabled={twoFaCode.length < 6}>
                        {t("twoFa.confirm")}
                      </Button>
                      <Button variant="secondary" onClick={() => setSetup(null)}>
                        {t("common.cancel")}
                      </Button>
                    </div>
                  </div>
                </div>
              )}

              {securityMsg ? <Alert variant={securityOk ? "success" : "error"}>{securityMsg}</Alert> : null}
            </CardBody>
          </Card>
        </div>
      ) : null}

      {tab === "preferences" ? (
        <div className="grid gap-4">
          <Card>
            <CardBody>
              <CardTitle>{t("settings.installTitle")}</CardTitle>
              <CardDescription className="mt-2">{t("settings.installHint")}</CardDescription>
              {isStandalone ? (
                <p className="mt-4 text-sm text-[var(--color-primary)]">{t("settings.alreadyInstalled")}</p>
              ) : (
                <div className="mt-4">
                  {canPromptInstall ? (
                    <Button type="button" onClick={() => void promptInstall()}>
                      {t("settings.installButton")}
                    </Button>
                  ) : (
                    <p className="text-sm text-[var(--color-muted)]">{t("settings.installManual")}</p>
                  )}
                </div>
              )}
            </CardBody>
          </Card>
          <Card>
            <CardBody>
              <CardTitle>{t("settings.pushTitle")}</CardTitle>
              <CardDescription className="mt-2">{t("settings.pushHint")}</CardDescription>
              {!swReady ? (
                <p className="mt-4 text-sm text-[var(--color-muted)]">{t("common.loading")}</p>
              ) : (
                <div className="mt-4">
                  {pushEnabled ? (
                    <Button variant="secondary" disabled={pushBusy} onClick={() => void disablePush()}>
                      {t("settings.pushDisable")}
                    </Button>
                  ) : (
                    <Button disabled={pushBusy} onClick={() => void enablePush()}>
                      {t("settings.pushEnable")}
                    </Button>
                  )}
                </div>
              )}
              {pushMessage ? <p className="mt-3 text-sm text-[var(--color-muted)]">{pushMessage}</p> : null}
            </CardBody>
          </Card>
        </div>
      ) : null}
    </PageContent>
  );
}

export default function AccountPage() {
  return (
    <Suspense fallback={<PageContent><ProfileSkeleton /></PageContent>}>
      <AccountPageContent />
    </Suspense>
  );
}
