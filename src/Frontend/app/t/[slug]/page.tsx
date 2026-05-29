"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { PublicHeader } from "@/components/layout/PublicHeader";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardBody } from "@/components/ui/card";
import { CardSkeleton } from "@/components/ui/skeleton";
import { TenantSsoSignInButton } from "@/components/auth/TenantSsoSignInButton";
import { billingApi, type TenantPublicDto } from "@/lib/features/billing/billing-api";

export default function TenantPublicPage() {
  const { t } = useTranslation();
  const params = useParams<{ slug: string }>();
  const slug = params.slug;
  const [tenant, setTenant] = useState<TenantPublicDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!slug) return;
    void (async () => {
      const res = await billingApi.getPublicTenant(slug);
      if (!res.success || !res.data) {
        setError(res.error?.message ?? t("tenantPublic.notFound"));
      } else {
        setTenant(res.data);
      }
      setLoading(false);
    })();
  }, [slug, t]);

  return (
    <div className="mesh-bg min-h-screen min-h-[100dvh]">
      <div className="mx-auto max-w-3xl px-4 py-8 sm:px-6 lg:px-8">
        <PublicHeader />
        {loading ? <div className="mt-10"><CardSkeleton lines={4} /></div> : null}
        {error ? <Alert className="mt-6" variant="error">{error}</Alert> : null}
        {tenant ? (
          <Card className="mt-10">
            <CardBody className="space-y-4">
              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-[var(--color-muted)]">
                  {t("tenantPublic.workspace")}
                </p>
                <h1 className="text-3xl font-semibold">{tenant.displayName}</h1>
                {tenant.publicTagline ? (
                  <p className="mt-2 text-lg text-[var(--color-muted)]">{tenant.publicTagline}</p>
                ) : null}
              </div>
              {tenant.publicDescription ? (
                <p className="text-sm leading-relaxed text-[var(--color-foreground)]">{tenant.publicDescription}</p>
              ) : null}
              <div className="flex flex-wrap gap-3">
                <Link href={`/t/${tenant.slug}/app`}>
                  <Button>{t("tenantPublic.openApp")}</Button>
                </Link>
                <Link href="/pricing">
                  <Button variant="secondary">{t("nav.pricing")}</Button>
                </Link>
              </div>
              <div className="mt-4 border-t border-[var(--color-border)] pt-4">
                <p className="mb-2 text-sm font-medium">{t("tenantPublic.ssoTitle")}</p>
                <TenantSsoSignInButton tenantSlug={tenant.slug} />
              </div>
            </CardBody>
          </Card>
        ) : null}
      </div>
    </div>
  );
}
