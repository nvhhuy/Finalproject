"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { LazyQrCode } from "@/lib/ui/lazy-components";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PageContent, PageHeader } from "@/components/ui/page-header";
import { authApi } from "@/lib/features/auth/auth-api";
import { userApi } from "@/lib/features/users/user-api";

export default function TwoFactorPage() {
  const { t } = useTranslation();
  const profileQuery = useQuery({
    queryKey: ["me", "profile"],
    queryFn: async () => {
      const res = await userApi.getMyProfile();
      if (!res.success || !res.data) throw new Error(res.error?.message ?? t("errors.requestFailed"));
      return res.data;
    },
  });

  const [setup, setSetup] = useState<{ sharedKey: string; otpAuthUri: string } | null>(null);
  const [code, setCode] = useState("");
  const [msg, setMsg] = useState<string | null>(null);
  const [msgOk, setMsgOk] = useState(false);

  const enabled = profileQuery.data?.twoFactorEnabled;

  async function begin() {
    setMsg(null);
    setMsgOk(false);
    const res = await authApi.beginTwoFactor();
    if (!res.success || !res.data) {
      setMsg(res.error?.message ?? t("twoFa.beginFailed"));
      setMsgOk(false);
      return;
    }
    setSetup(res.data);
  }

  async function confirm() {
    setMsg(null);
    const res = await authApi.confirmTwoFactor(code);
    if (!res.success) {
      setMsg(res.error?.message ?? t("twoFa.confirmFailed"));
      setMsgOk(false);
      return;
    }
    setMsg(t("twoFa.enabledMsg"));
    setMsgOk(true);
    setSetup(null);
    setCode("");
    await profileQuery.refetch();
  }

  return (
    <PageContent>
      <PageHeader title={t("twoFa.title")} description={t("twoFa.setupHint")} />

      <Card>
        <CardHeader>
          <CardTitle>{t("twoFa.status")}</CardTitle>
        </CardHeader>
        <CardBody>
          {profileQuery.isLoading ? (
            <p className="text-sm text-[var(--color-muted)]">{t("common.loading")}</p>
          ) : (
            <Badge variant={enabled ? "success" : "warning"}>
              {enabled ? t("common.enabled") : t("common.disabled")}
            </Badge>
          )}
        </CardBody>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t("twoFa.setupTitle")}</CardTitle>
          <CardDescription>{t("twoFa.setupHint")}</CardDescription>
        </CardHeader>
        <CardBody className="grid gap-5">
          {!setup ? (
            <Button onClick={begin}>{t("twoFa.begin")}</Button>
          ) : (
            <div className="grid gap-6 lg:grid-cols-[auto_1fr] lg:items-start">
              <div className="grid gap-3">
                <p className="text-sm font-medium text-[var(--color-foreground)]">{t("twoFa.scanQr")}</p>
                <LazyQrCode value={setup.otpAuthUri} />
              </div>
              <div className="grid gap-4">
                <div className="rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-muted)] p-3">
                  <p className="text-xs font-medium text-[var(--color-muted)]">{t("twoFa.sharedKey")}</p>
                  <p className="mt-1.5 break-all font-mono text-sm text-[var(--color-foreground)]">{setup.sharedKey}</p>
                </div>
                <div className="grid gap-2">
                  <Label htmlFor="code">{t("twoFa.code")}</Label>
                  <Input id="code" value={code} onChange={(e) => setCode(e.target.value)} placeholder="123456" />
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button onClick={confirm} disabled={code.length < 6}>
                    {t("twoFa.confirm")}
                  </Button>
                  <Button variant="secondary" onClick={() => setSetup(null)}>
                    {t("common.cancel")}
                  </Button>
                </div>
              </div>
            </div>
          )}
          {msg ? <Alert variant={msgOk ? "success" : "error"}>{msg}</Alert> : null}
        </CardBody>
      </Card>
    </PageContent>
  );
}
