"use client";

import Link from "next/link";
import { useTranslation } from "react-i18next";
import { useQuery } from "@tanstack/react-query";
import { PublicHeader } from "@/components/layout/PublicHeader";
import { cn } from "@/lib/ui/cn";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { Alert } from "@/components/ui/alert";
import { PricingCardsSkeleton } from "@/components/ui/skeleton";
import { billingApi, type PlanDto } from "@/lib/features/billing/billing-api";

function formatPrice(cents: number, currency: string) {
  return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(cents / 100);
}

export default function PricingPage() {
  const { t } = useTranslation();

  const { data: plans = [], isLoading, error } = useQuery<PlanDto[], Error>({
    queryKey: ["public-plans"],
    queryFn: async () => {
      const res = await billingApi.listPublicPlans();
      if (!res.success || !res.data) throw new Error(res.error?.message ?? t("errors.requestFailed"));
      return res.data.filter((p) => p.isActive).sort((a, b) => a.sortOrder - b.sortOrder);
    },
    staleTime: 5 * 60 * 1000,
  });

  return (
    <div className="mesh-bg min-h-screen min-h-[100dvh]">
      <div className="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">
        <PublicHeader />
        <div className="mt-10 text-center">
          <h1 className="text-3xl font-semibold tracking-tight sm:text-4xl">{t("pricing.title")}</h1>
          <p className="mt-3 text-[var(--color-muted)]">{t("pricing.subtitle")}</p>
        </div>

        {error ? <Alert className="mt-6" variant="error">{error.message}</Alert> : null}
        {isLoading ? <PricingCardsSkeleton /> : null}

        {!isLoading ? (
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
                    <Link
                      href={`/app/subscribe?plan=${encodeURIComponent(plan.code)}`}
                      className={cn(
                        "inline-flex h-10 w-full items-center justify-center rounded-lg bg-[var(--color-primary)] px-4 text-sm font-medium text-white shadow-sm transition hover:bg-[var(--color-primary-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-primary)]/50 focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--color-background)]",
                      )}
                    >
                      {t("pricing.choosePlan")}
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
