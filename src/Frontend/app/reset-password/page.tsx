"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";
import { useTranslation } from "react-i18next";
import { AuthLayout } from "@/components/layout/AuthLayout";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardBody } from "@/components/ui/card";
import { LoadingState } from "@/components/ui/loading";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { authApi } from "@/lib/features/auth/auth-api";

function ResetForm() {
  const { t } = useTranslation();
  const router = useRouter();
  const params = useSearchParams();
  const tokenId = params.get("tokenId") ?? "";
  const token = params.get("token") ?? "";

  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [isSuccess, setIsSuccess] = useState(false);
  const [loading, setLoading] = useState(false);

  if (!tokenId || !token) {
    return <Alert variant="error">{t("resetPassword.invalidLink")}</Alert>;
  }

  async function submit() {
    if (password.length < 8) {
      setMessage(t("errors.passwordMin"));
      setIsSuccess(false);
      return;
    }
    if (password !== confirm) {
      setMessage(t("errors.passwordMismatch"));
      setIsSuccess(false);
      return;
    }
    setLoading(true);
    setMessage(null);
    const res = await authApi.resetPassword({ tokenId, token, newPassword: password });
    setLoading(false);
    if (res.success) {
      setMessage(t("resetPassword.success"));
      setIsSuccess(true);
      setTimeout(() => router.push("/login"), 1500);
      return;
    }
    setMessage(res.error?.message ?? t("errors.requestFailed"));
    setIsSuccess(false);
  }

  return (
    <>
      <div className="grid gap-2">
        <Label htmlFor="password">{t("common.newPassword")}</Label>
        <Input id="password" value={password} onChange={(e) => setPassword(e.target.value)} type="password" />
      </div>
      <div className="grid gap-2">
        <Label htmlFor="confirm">{t("common.confirmPassword")}</Label>
        <Input id="confirm" value={confirm} onChange={(e) => setConfirm(e.target.value)} type="password" />
      </div>
      <Button onClick={submit} disabled={loading} className="w-full">
        {t("resetPassword.reset")}
      </Button>
      {message ? <Alert variant={isSuccess ? "success" : "error"}>{message}</Alert> : null}
    </>
  );
}

export default function ResetPasswordPage() {
  const { t } = useTranslation();
  return (
    <AuthLayout title={t("resetPassword.title")} subtitle={t("resetPassword.subtitle")}>
      <Card>
        <CardBody className="grid gap-4">
          <Suspense fallback={<LoadingState compact />}>
            <ResetForm />
          </Suspense>
          <Link className="link text-sm" href="/login">
            {t("resetPassword.backToLogin")}
          </Link>
        </CardBody>
      </Card>
    </AuthLayout>
  );
}
