"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Suspense } from "react";
import { useTranslation } from "react-i18next";
import { useQuery } from "@tanstack/react-query";
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

  const { data, isLoading, error } = useQuery({
    queryKey: ["verify-email", tokenId, token],
    queryFn: async () => {
      const res = await authApi.verifyEmail(tokenId, token);
      if (!res.success) throw new Error(res.error?.message ?? t("verifyEmail.failed"));
      return res.data;
    },
    enabled: !!tokenId && !!token,
    retry: false,
  });

  if (!tokenId || !token) {
    return <Alert variant="error">{t("verifyEmail.missingParams")}</Alert>;
  }

  if (isLoading) {
    return <Alert variant="info">{t("verifyEmail.verifying")}</Alert>;
  }

  if (error) {
    return <Alert variant="error">{error instanceof Error ? error.message : t("verifyEmail.failed")}</Alert>;
  }

  return <Alert variant="success">{t("verifyEmail.success")}</Alert>;
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
