"use client";

import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useAuth } from "@/lib/auth/auth-context";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PageContent, PageHeader } from "@/components/ui/page-header";
import { ProfileSkeleton } from "@/components/ui/skeleton";
import { authApi } from "@/lib/features/auth/auth-api";
import { userApi } from "@/lib/features/users/user-api";
import { mediaApi } from "@/lib/features/media/media-api";
import { isConcurrencyError } from "@/lib/errors";
import { cn } from "@/lib/ui/cn";

export default function ProfilePage() {
  const { t } = useTranslation();
  const { reloadProfile } = useAuth();
  const qc = useQueryClient();
  const profileQuery = useQuery({
    queryKey: ["me", "profile"],
    queryFn: async () => {
      const res = await userApi.getMyProfile();
      if (!res.success || !res.data) throw new Error(res.error?.message ?? t("errors.requestFailed"));
      return res.data;
    },
  });

  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null);
  const [resendMsg, setResendMsg] = useState<string | null>(null);

  const profile = profileQuery.data;

  useEffect(() => {
    if (profile) {
      setFirstName(profile.firstName);
      setLastName(profile.lastName);
    }
  }, [profile]);

  const updateMutation = useMutation({
    mutationFn: async () => {
      if (!profile) return;
      const res = await userApi.updateMyProfile({
        firstName: firstName.trim() || profile.firstName,
        lastName: lastName.trim() || profile.lastName,
        rowVersion: profile.rowVersion,
      });
      if (!res.success) throw new Error(res.error?.message ?? t("errors.requestFailed"));
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ["me", "profile"] });
      // Sync AuthContext so UserMenu/header name updates immediately without reload
      await reloadProfile();
    },
  });

  async function uploadAvatar(file: File) {
    const init = await mediaApi.initiate(file);
    const uploaded = await mediaApi.putToPresignedUrl(init.uploadUrl, file, file.type);
    if (!uploaded) throw new Error(t("errors.requestFailed"));
    const completed = await mediaApi.complete(init.mediaAssetId);
    setAvatarUrl(mediaApi.bestRenderableUrl(completed));
  }

  async function resendVerification() {
    if (!profile?.email) return;
    const res = await authApi.resendVerification(profile.email);
    setResendMsg(res.success ? t("profile.resendSent") : res.error?.message ?? t("errors.requestFailed"));
  }

  const updateError = updateMutation.error;
  const concurrency = updateError && isConcurrencyError(updateError);

  if (profileQuery.isLoading) {
    return (
      <PageContent>
        <PageHeader title={t("profile.title")} />
        <ProfileSkeleton />
      </PageContent>
    );
  }

  return (
    <PageContent>
      <PageHeader title={t("profile.title")} />

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
          {profileQuery.isError ? (
            <Alert variant="error">{(profileQuery.error as Error).message}</Alert>
          ) : profile ? (
            <>
              <div className="flex flex-wrap gap-2">
                <Badge variant="primary">
                  {t("profile.role")}: {profile.role}
                </Badge>
                <Badge variant={profile.twoFactorEnabled ? "success" : "warning"}>
                  2FA: {profile.twoFactorEnabled ? t("common.enabled") : t("common.disabled")}
                </Badge>
              </div>
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

      <Card>
        <CardHeader>
          <CardTitle>{t("profile.avatarTitle")}</CardTitle>
          <CardDescription>{t("profile.avatarHint")}</CardDescription>
        </CardHeader>
        <CardBody>
          <label
            className={cn(
              "inline-flex cursor-pointer items-center gap-3 rounded-lg border border-dashed border-[var(--color-border)]",
              "bg-[var(--color-surface-muted)] px-4 py-3 text-sm font-medium text-[var(--color-foreground)] transition",
              "hover:border-[var(--color-primary)] hover:bg-[var(--color-primary-soft)]",
            )}
          >
            <span aria-hidden>📷</span>
            <span>{t("profile.avatarTitle")}</span>
            <input
              type="file"
              accept="image/*"
              className="sr-only"
              onChange={async (e) => {
                const file = e.target.files?.[0];
                if (!file) return;
                await uploadAvatar(file);
              }}
            />
          </label>
          {avatarUrl ? (
            <div className="mt-4 flex items-center gap-4">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src={avatarUrl}
                alt=""
                loading="lazy"
                decoding="async"
                className="h-20 w-20 rounded-full border-2 border-[var(--color-border)] object-cover shadow-md ring-2 ring-[var(--color-primary-soft)]"
              />
              <p className="text-sm text-[var(--color-muted)]">{t("profile.preview")}</p>
            </div>
          ) : null}
        </CardBody>
      </Card>
    </PageContent>
  );
}
