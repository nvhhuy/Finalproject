"use client";

import Link from "next/link";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { AuthLayout } from "@/components/layout/AuthLayout";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardBody } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { authApi } from "@/lib/features/auth/auth-api";

export default function ForgotPasswordPage() {
  const { t } = useTranslation();
  const [email, setEmail] = useState("");
  const [sent, setSent] = useState(false);
  const [loading, setLoading] = useState(false);

  async function submit() {
    setLoading(true);
    await authApi.forgotPassword(email);
    setSent(true);
    setLoading(false);
  }

  return (
    <AuthLayout title={t("forgotPassword.title")} subtitle={t("forgotPassword.subtitle")}>
      <Card>
        <CardBody className="grid gap-4">
          <div className="grid gap-2">
            <Label htmlFor="email">{t("common.email")}</Label>
            <Input id="email" value={email} onChange={(e) => setEmail(e.target.value)} type="email" />
          </div>
          <Button onClick={submit} disabled={loading || !email} className="w-full">
            {t("forgotPassword.send")}
          </Button>
          {sent ? <Alert variant="success">{t("forgotPassword.sent")}</Alert> : null}
          <Link className="link text-sm" href="/login">
            {t("forgotPassword.backToLogin")}
          </Link>
        </CardBody>
      </Card>
    </AuthLayout>
  );
}
