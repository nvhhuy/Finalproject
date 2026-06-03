"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { AuthLayout } from "@/components/layout/AuthLayout";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardBody } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { TurnstileWidget } from "@/components/ui/TurnstileWidget";
import { authApi } from "@/lib/features/auth/auth-api";
import { getApiErrorMessageFromPayload, getNetworkErrorMessage } from "@/lib/errors";

export default function RegisterPage() {
  const { t } = useTranslation();
  const router = useRouter();
  const [form, setForm] = useState({
    email: "",
    userName: "",
    password: "",
    confirmPassword: "",
    firstName: "",
    lastName: "",
  });
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [isSuccess, setIsSuccess] = useState(false);
  const [turnstileToken, setTurnstileToken] = useState<string | null>(null);

  async function submit() {
    setLoading(true);
    setMessage(null);
    setIsSuccess(false);
    if (form.password !== form.confirmPassword) {
      setMessage(t("errors.passwordMismatch"));
      setLoading(false);
      return;
    }
    if (form.password.length < 8) {
      setMessage(t("errors.passwordMin"));
      setLoading(false);
      return;
    }
    try {
      const res = await authApi.register({ ...form, turnstileToken });
      if (res.success) {
        router.push(`/awaiting-verification?email=${encodeURIComponent(form.email)}`);
        return;
      }
      setMessage(getApiErrorMessageFromPayload(res.error));
      setTurnstileToken(null);
    } catch (e) {
      setMessage(getNetworkErrorMessage(e));
      setTurnstileToken(null);
    } finally {
      setLoading(false);
    }
  }

  async function resend() {
    setLoading(true);
    try {
      const res = await authApi.resendVerification(form.email, turnstileToken);
      setMessage(
        res.success ? t("profile.resendSent") : getApiErrorMessageFromPayload(res.error),
      );
      setIsSuccess(res.success);
      if (!res.success) setTurnstileToken(null);
    } catch (e) {
      setMessage(getNetworkErrorMessage(e));
      setTurnstileToken(null);
    } finally {
      setLoading(false);
    }
  }

  return (
    <AuthLayout title={t("register.title")} subtitle={t("register.subtitle")}>
      <Card>
        <CardBody className="grid gap-4">
          <div className="grid gap-2">
            <Label htmlFor="email">{t("common.email")}</Label>
            <Input id="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="userName">{t("register.userName")}</Label>
            <Input id="userName" value={form.userName} onChange={(e) => setForm({ ...form, userName: e.target.value })} />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="password">{t("common.password")}</Label>
            <Input
              id="password"
              value={form.password}
              onChange={(e) => setForm({ ...form, password: e.target.value })}
              type="password"
              autoComplete="new-password"
            />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="confirmPassword">{t("register.confirmPassword")}</Label>
            <Input
              id="confirmPassword"
              value={form.confirmPassword}
              onChange={(e) => setForm({ ...form, confirmPassword: e.target.value })}
              type="password"
              autoComplete="new-password"
            />
          </div>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div className="grid gap-2">
              <Label htmlFor="firstName">{t("register.firstName")}</Label>
              <Input id="firstName" value={form.firstName} onChange={(e) => setForm({ ...form, firstName: e.target.value })} />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="lastName">{t("register.lastName")}</Label>
              <Input id="lastName" value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} />
            </div>
          </div>
          <TurnstileWidget
            onVerify={setTurnstileToken}
            onExpire={() => setTurnstileToken(null)}
            onError={() => setTurnstileToken(null)}
          />
          <Button onClick={submit} disabled={loading} className="w-full">
            {t("register.create")}
          </Button>
          <Button variant="secondary" onClick={() => router.push("/login")} disabled={loading} className="w-full">
            {t("register.backToLogin")}
          </Button>
          {message ? (
            <Alert variant={isSuccess ? "success" : "error"}>
              <p className="whitespace-pre-wrap">{message}</p>
              {isSuccess ? (
                <p className="mt-2">
                  {t("register.resendHint")}{" "}
                  <button type="button" className="link" onClick={resend}>
                    {t("register.resend")}
                  </button>
                </p>
              ) : null}
            </Alert>
          ) : null}
        </CardBody>
      </Card>
    </AuthLayout>
  );
}
