"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Suspense, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { AuthLayout } from "@/components/layout/AuthLayout";
import { Alert } from "@/components/ui/alert";
import { Card, CardBody } from "@/components/ui/card";
import { LoadingState } from "@/components/ui/loading";
import { authApi } from "@/lib/features/auth/auth-api";

function VerifyBody() {
  const { t } = useTranslation();
  const params = useSearchParams();
  const tokenId = params.get("tokenId") ?? "";
  const token = params.get("token") ?? "";
  const [status, setStatus] = useState<"loading" | "ok" | "fail">("loading");
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!tokenId || !token) {
      setStatus("fail");
      setMessage(t("verifyEmail.missingParams"));
      return;
    }
    void authApi.verifyEmail(tokenId, token).then((res) => {
      if (res.success) {
        setStatus("ok");
        setMessage(t("verifyEmail.success"));
      } else {
        setStatus("fail");
        setMessage(res.error?.message ?? t("verifyEmail.failed"));
      }
    });
  }, [tokenId, token, t]);

  return (
    <Alert variant={status === "ok" ? "success" : status === "fail" ? "error" : "info"}>
      {status === "loading" ? t("verifyEmail.verifying") : message}
    </Alert>
  );
}

export default function VerifyEmailPage() {
  const { t } = useTranslation();
  return (
    <AuthLayout title={t("verifyEmail.title")}>
      <Card>
        <CardBody className="grid gap-4">
          <Suspense fallback={<LoadingState compact label={t("verifyEmail.verifying")} />}>
            <VerifyBody />
          </Suspense>
          <Link className="link text-sm" href="/login">
            {t("verifyEmail.backToLogin")}
          </Link>
        </CardBody>
      </Card>
    </AuthLayout>
  );
}
