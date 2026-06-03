"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";
import { useTranslation } from "react-i18next";
import { AuthLayout } from "@/components/layout/AuthLayout";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardBody } from "@/components/ui/card";
import { TurnstileWidget } from "@/components/ui/TurnstileWidget";
import { authApi } from "@/lib/features/auth/auth-api";

function AwaitingVerificationForm() {
  const { t } = useTranslation();
  const params = useSearchParams();
  const email = params.get("email") ?? "";
  const [message, setMessage] = useState<string | null>(null);
  const [resendOk, setResendOk] = useState(false);
  const [loading, setLoading] = useState(false);
  const [turnstileToken, setTurnstileToken] = useState<string | null>(null);

  async function resend() {
    if (!email) {
      setMessage(t("awaitingVerification.noEmail"));
      return;
    }
    setLoading(true);
    try {
      const res = await authApi.resendVerification(email, turnstileToken);
      setResendOk(res.success);
      setMessage(res.success ? t("profile.resendSent") : res.error?.message ?? t("errors.requestFailed"));
      if (!res.success) setTurnstileToken(null);
    } finally {
      setLoading(false);
    }
  }

  return (
    <AuthLayout title={t("awaitingVerification.title")} subtitle={t("awaitingVerification.subtitle")}>
      <Card>
        <CardBody className="grid gap-4">
          {email ? (
            <p className="text-sm text-[var(--color-muted)]">
              {t("awaitingVerification.sentTo", { email })}
            </p>
          ) : null}
          <Alert variant="info">{t("awaitingVerification.hint")}</Alert>
          {message ? <Alert variant={resendOk ? "success" : "error"}>{message}</Alert> : null}
          <TurnstileWidget onVerify={setTurnstileToken} />
          <Button type="button" disabled={loading || !email} onClick={() => void resend()}>
            {t("profile.resendVerification")}
          </Button>
          <Link href="/login" className="text-center text-sm text-[var(--color-primary)]">
            {t("awaitingVerification.backToLogin")}
          </Link>
        </CardBody>
      </Card>
    </AuthLayout>
  );
}

export default function AwaitingVerificationPage() {
  return (
    <Suspense fallback={null}>
      <AwaitingVerificationForm />
    </Suspense>
  );
}
