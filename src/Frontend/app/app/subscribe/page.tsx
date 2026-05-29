"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
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
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const fromQuery = searchParams.get("plan");
    if (fromQuery) setPlanCode(fromQuery);
  }, [searchParams]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError(null);
    const res = await billingApi.subscribe({
      planCode: planCode.trim(),
      tenantSlug: tenantSlug.trim(),
      tenantDisplayName: tenantDisplayName.trim(),
      publicTagline: tagline.trim() || null,
      publicDescription: description.trim() || null,
    });
    setLoading(false);
    if (!res.success || !res.data) {
      setError(res.error?.message ?? t("errors.requestFailed"));
      return;
    }
    setActiveTenantId(res.data.tenantId);
    setActiveTenantSlug(res.data.tenantSlug);
    router.push("/app");
  }

  return (
    <PageContent>
      <PageHeader title={t("subscribe.title")} description={t("subscribe.subtitle")} />
      {error ? <Alert variant="error">{error}</Alert> : null}
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
            <Button type="submit" loading={loading}>
              {t("subscribe.submit")}
            </Button>
          </form>
        </CardBody>
      </Card>
    </PageContent>
  );
}
