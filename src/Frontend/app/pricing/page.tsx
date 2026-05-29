"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { PublicHeader } from "@/components/layout/PublicHeader";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { Alert } from "@/components/ui/alert";
import { PricingCardsSkeleton } from "@/components/ui/skeleton";
import { billingApi, type PlanDto } from "@/lib/features/billing/billing-api";

function formatPrice(cents: number, currency: string) {
  return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(cents / 100);
}

export default function PricingPage() {
  const { t } = useTranslation();
  const [plans, setPlans] = useState<PlanDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    void (async () => {
      const res = await billingApi.listPublicPlans();
      if (!res.success || !res.data) {
        setError(res.error?.message ?? t("errors.requestFailed"));
      } else {
        setPlans(res.data.filter((p) => p.isActive).sort((a, b) => a.sortOrder - b.sortOrder));
      }
      setLoading(false);
    })();
  }, [t]);

  return (
    <div className="mesh-bg min-h-screen min-h-[100dvh]">
      <div className="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">
        <PublicHeader />
        <div className="mt-10 text-center">
          <h1 className="text-3xl font-semibold tracking-tight sm:text-4xl">{t("pricing.title")}</h1>
          <p className="mt-3 text-[var(--color-muted)]">{t("pricing.subtitle")}</p>
        </div>

        {error ? <Alert className="mt-6" variant="error">{error}</Alert> : null}
        {loading ? <PricingCardsSkeleton /> : null}

        {!loading ? (
        <div className="mt-10 grid gap-6 md:grid-cols-2 lg:grid-cols-3">
          {plans.map((plan) => (
            <Card key={plan.id} className="flex flex-col">
              <CardHeader>
                <CardTitle>{plan.name}</CardTitle>
                <p className="text-2xl font-semibold">{formatPrice(plan.priceMonthlyCents, plan.currency)}</p>
                <p className="text-sm text-[var(--color-muted)]">/ {t("pricing.month")}</p>
              </CardHeader>
              <CardBody className="flex flex-1 flex-col gap-3">
                {plan.description ? <p className="text-sm text-[var(--color-muted)]">{plan.description}</p> : null}
                <ul className="space-y-1 text-sm">
                  <li>{t("pricing.maxUsers", { count: plan.limits.maxUsers })}</li>
                  <li>{t("pricing.trialDays", { count: plan.trialDays })}</li>
                </ul>
                <div className="mt-auto pt-2">
                  <Link href={`/app/subscribe?plan=${encodeURIComponent(plan.code)}`}>
                    <Button className="w-full">{t("pricing.choosePlan")}</Button>
                  </Link>
                </div>
              </CardBody>
            </Card>
          ))}
        </div>
        ) : null}
      </div>
    </div>
  );
}
