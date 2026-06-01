"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useMutation } from "@tanstack/react-query";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { PageContent, PageHeader } from "@/components/ui/page-header";
import { billingApi } from "@/lib/features/billing/billing-api";
import { setActiveTenantId, setActiveTenantSlug } from "@/lib/tenancy/tenant-context";

export default function SubscribePage() {
  const { t } = useTranslation();
  const router = useRouter();
  const searchParams = useSearchParams();

  const [planCode, setPlanCode] = useState(searchParams.get("plan") ?? "starter");
  const [tenantSlug, setTenantSlug] = useState("");
  const [tenantDisplayName, setTenantDisplayName] = useState("");
  const [tagline, setTagline] = useState("");
  const [description, setDescription] = useState("");

  const subscribeMutation = useMutation({
    mutationFn: () =>
      billingApi.subscribe({
        planCode: planCode.trim(),
        tenantSlug: tenantSlug.trim(),
        tenantDisplayName: tenantDisplayName.trim(),
        publicTagline: tagline.trim() || null,
        publicDescription: description.trim() || null,
      }),
    onSuccess: (res) => {
      if (!res.success || !res.data) {
        subscribeMutation.reset();
        return;
      }
      setActiveTenantId(res.data.tenantId);
      setActiveTenantSlug(res.data.tenantSlug);
      router.push("/app");
    },
  });

  const errorMessage = subscribeMutation.data?.success === false
    ? (subscribeMutation.data.error?.message ?? t("errors.requestFailed"))
    : subscribeMutation.error instanceof Error
      ? subscribeMutation.error.message
      : null;

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    subscribeMutation.mutate();
  }

  return (
    <PageContent>
      <PageHeader title={t("subscribe.title")} description={t("subscribe.subtitle")} />
      {errorMessage ? <Alert variant="error">{errorMessage}</Alert> : null}
      <Card>
        <CardHeader>
          <CardTitle>{t("subscribe.formTitle")}</CardTitle>
        </CardHeader>
        <CardBody>
          <form className="space-y-3" onSubmit={handleSubmit}>
            <Input placeholder={t("subscribe.planCode")} value={planCode} onChange={(e) => setPlanCode(e.target.value)} required />
            <Input placeholder={t("subscribe.tenantSlug")} value={tenantSlug} onChange={(e) => setTenantSlug(e.target.value)} required />
            <Input placeholder={t("subscribe.tenantName")} value={tenantDisplayName} onChange={(e) => setTenantDisplayName(e.target.value)} required />
            <Input placeholder={t("subscribe.tagline")} value={tagline} onChange={(e) => setTagline(e.target.value)} />
            <textarea
              className="min-h-20 w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm"
              placeholder={t("subscribe.description")}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
            <Button type="submit" loading={subscribeMutation.isPending}>
              {t("subscribe.submit")}
            </Button>
          </form>
        </CardBody>
      </Card>
    </PageContent>
  );
}
