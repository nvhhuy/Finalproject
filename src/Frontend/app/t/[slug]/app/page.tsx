"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardBody } from "@/components/ui/card";
import { PublicHeader } from "@/components/layout/PublicHeader";
import { RequireAuth } from "@/lib/auth/require-auth";
import { billingApi } from "@/lib/features/billing/billing-api";
import { setActiveTenantId } from "@/lib/tenancy/tenant-context";

function TenantAppEntryContent() {
  const { t } = useTranslation();
  const router = useRouter();
  const params = useParams<{ slug: string }>();
  const slug = params.slug;
  const [error, setError] = useState<string | null>(null);
  const [displayName, setDisplayName] = useState<string | null>(null);

  useEffect(() => {
    if (!slug) return;
    void (async () => {
      const res = await billingApi.getPublicTenant(slug);
      if (!res.success || !res.data) {
        setError(res.error?.message ?? t("tenantPublic.notFound"));
        return;
      }
      setDisplayName(res.data.displayName);
      setActiveTenantId(res.data.id);
    })();
  }, [slug, t]);

  return (
    <div className="mesh-bg min-h-screen min-h-[100dvh]">
      <div className="mx-auto max-w-3xl px-4 py-8 sm:px-6">
        <PublicHeader showAuthLinks={false} />
        <h1 className="mt-8 text-2xl font-semibold">{displayName ?? t("tenantPublic.appEntry")}</h1>
        <p className="mt-2 text-sm text-[var(--color-muted)]">{t("tenantPublic.appEntryHint")}</p>
        {error ? <Alert className="mt-4" variant="error">{error}</Alert> : null}
        <Card className="mt-6">
          <CardBody className="flex flex-wrap gap-3">
            <Button onClick={() => router.push("/app")}>{t("tenantPublic.continueToMainApp")}</Button>
            <Link href={`/t/${slug}`}>
              <Button variant="secondary">{t("common.back")}</Button>
            </Link>
          </CardBody>
        </Card>
      </div>
    </div>
  );
}

export default function TenantAppEntryPage() {
  return (
    <RequireAuth>
      <TenantAppEntryContent />
    </RequireAuth>
  );
}
