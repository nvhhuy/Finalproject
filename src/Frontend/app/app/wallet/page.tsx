"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { PageContent, PageHeader } from "@/components/ui/page-header";
import { WalletSkeleton } from "@/components/ui/skeleton";
import { formatDateTimeOrDash } from "@/lib/shared";
import { formatPackagePrice } from "@/lib/features/wallet/format-package-price";
import {
  walletApi,
  type CreditPackageDto,
  type MeteredActionDto,
  type BillingInvoiceDto,
  type WalletLedgerEntryDto,
} from "@/lib/features/wallet/wallet-api";

const QUERY_KEYS = {
  balance: ["wallet", "balance"] as const,
  ledger: ["wallet", "ledger"] as const,
  actions: ["wallet", "actions"] as const,
  packages: ["wallet", "packages"] as const,
  invoices: ["wallet", "invoices"] as const,
};

export default function WalletPage() {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const [topUpAmount, setTopUpAmount] = useState("100");
  const [mutationError, setMutationError] = useState<string | null>(null);

  const balanceQuery = useQuery({
    queryKey: QUERY_KEYS.balance,
    queryFn: async () => {
      const res = await walletApi.getBalance();
      if (!res.success || !res.data) throw new Error(res.error?.message ?? t("errors.requestFailed"));
      return res.data.balanceCredits;
    },
  });

  const ledgerQuery = useQuery({
    queryKey: QUERY_KEYS.ledger,
    queryFn: async () => {
      const res = await walletApi.getLedger(1, 20);
      if (!res.success || !res.data) return [] as WalletLedgerEntryDto[];
      return res.data;
    },
  });

  const actionsQuery = useQuery({
    queryKey: QUERY_KEYS.actions,
    queryFn: async () => {
      const res = await walletApi.listActions();
      if (!res.success || !res.data) return [] as MeteredActionDto[];
      return res.data;
    },
  });

  const packagesQuery = useQuery({
    queryKey: QUERY_KEYS.packages,
    queryFn: async () => {
      const res = await walletApi.listPackages();
      if (!res.success || !res.data) return [] as CreditPackageDto[];
      return res.data;
    },
  });

  const invoicesQuery = useQuery({
    queryKey: QUERY_KEYS.invoices,
    queryFn: async () => {
      const res = await walletApi.listInvoices(1, 20);
      if (!res.success || !res.data) return [] as BillingInvoiceDto[];
      return res.data;
    },
  });

  const invalidateAll = () =>
    qc.invalidateQueries({ queryKey: ["wallet"] });

  const purchaseMutation = useMutation({
    mutationFn: (packageCode: string) => walletApi.purchasePackage(packageCode),
    onSuccess: (res) => {
      if (!res.success || !res.data) {
        setMutationError(res.error?.message ?? t("errors.requestFailed"));
        return;
      }
      setMutationError(null);
      void invalidateAll();
    },
    onError: () => setMutationError(t("errors.requestFailed")),
  });

  const topUpMutation = useMutation({
    mutationFn: () => {
      const amount = Number(topUpAmount);
      if (!Number.isFinite(amount) || amount <= 0) throw new Error(t("errors.requestFailed"));
      return walletApi.topUp(amount);
    },
    onSuccess: (res) => {
      if (!res.success || !res.data) {
        setMutationError(res.error?.message ?? t("errors.requestFailed"));
        return;
      }
      setMutationError(null);
      void invalidateAll();
    },
    onError: () => setMutationError(t("errors.requestFailed")),
  });

  const debitMutation = useMutation({
    mutationFn: (code: string) => walletApi.debit(code),
    onSuccess: (res) => {
      if (!res.success || !res.data) {
        setMutationError(res.error?.message ?? t("errors.requestFailed"));
        return;
      }
      if (!res.data.success) {
        setMutationError(res.data.denyReason ?? t("wallet.debitFailed"));
        return;
      }
      setMutationError(null);
      void invalidateAll();
    },
    onError: () => setMutationError(t("errors.requestFailed")),
  });

  const isLoading = balanceQuery.isLoading || ledgerQuery.isLoading || actionsQuery.isLoading ||
    packagesQuery.isLoading || invoicesQuery.isLoading;

  if (isLoading) {
    return (
      <PageContent>
        <PageHeader title={t("wallet.title")} description={t("wallet.subtitle")} />
        <WalletSkeleton />
      </PageContent>
    );
  }

  const balance = balanceQuery.data ?? 0;
  const ledger = ledgerQuery.data ?? [];
  const actions = actionsQuery.data ?? [];
  const packages = packagesQuery.data ?? [];
  const invoices = invoicesQuery.data ?? [];

  return (
    <PageContent>
      <PageHeader title={t("wallet.title")} description={t("wallet.subtitle")} />
      {balanceQuery.isError ? (
        <Alert variant="error">{(balanceQuery.error as Error).message}</Alert>
      ) : null}
      {mutationError ? <Alert variant="error">{mutationError}</Alert> : null}

      <Card>
        <CardHeader>
          <CardTitle>{t("wallet.balance")}</CardTitle>
        </CardHeader>
        <CardBody>
          <p className="text-3xl font-semibold">{balance}</p>
          <p className="text-sm text-[var(--color-muted)]">{t("wallet.creditsHint")}</p>
        </CardBody>
      </Card>

      <Card className="mt-4">
        <CardHeader>
          <CardTitle>{t("wallet.packages")}</CardTitle>
        </CardHeader>
        <CardBody className="space-y-2">
          {packages.length === 0 ? <p className="text-sm text-[var(--color-muted)]">{t("wallet.noPackages")}</p> : null}
          {packages.map((pkg) => (
            <div
              key={pkg.id}
              className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-[var(--color-border)] px-3 py-2 text-sm"
            >
              <div>
                <p className="font-medium">{pkg.name}</p>
                <p className="text-[var(--color-muted)]">
                  {pkg.description ?? pkg.code} · {t("wallet.packageCredits", { count: pkg.creditsAmount })}
                </p>
              </div>
              <Button
                size="sm"
                loading={purchaseMutation.isPending && purchaseMutation.variables === pkg.code}
                onClick={() => purchaseMutation.mutate(pkg.code)}
              >
                {t("wallet.purchase", { price: formatPackagePrice(pkg.priceCents, pkg.currency) })}
              </Button>
            </div>
          ))}
        </CardBody>
      </Card>

      <Card className="mt-4">
        <CardHeader>
          <CardTitle>{t("wallet.demoTopUp")}</CardTitle>
        </CardHeader>
        <CardBody>
          <p className="text-sm text-[var(--color-muted)]">{t("wallet.demoTopUpHint")}</p>
          <div className="mt-4 flex flex-wrap gap-2">
            <Input className="max-w-[140px]" value={topUpAmount} onChange={(e) => setTopUpAmount(e.target.value)} />
            <Button variant="secondary" loading={topUpMutation.isPending} onClick={() => topUpMutation.mutate()}>
              {t("wallet.topUp")}
            </Button>
          </div>
        </CardBody>
      </Card>

      <Card className="mt-4">
        <CardHeader>
          <CardTitle>{t("wallet.meteredActions")}</CardTitle>
        </CardHeader>
        <CardBody className="space-y-2">
          {actions.map((a) => (
            <div key={a.code} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-[var(--color-border)] px-3 py-2 text-sm">
              <div>
                <p className="font-medium">{a.code}</p>
                <p className="text-[var(--color-muted)]">{a.description}</p>
              </div>
              <Button
                size="sm"
                variant="secondary"
                loading={debitMutation.isPending && debitMutation.variables === a.code}
                onClick={() => debitMutation.mutate(a.code)}
              >
                {t("wallet.debit", { cost: a.costCredits })}
              </Button>
            </div>
          ))}
        </CardBody>
      </Card>

      <Card className="mt-4">
        <CardHeader>
          <CardTitle>{t("wallet.receipts")}</CardTitle>
        </CardHeader>
        <CardBody className="space-y-2 text-sm">
          {invoices.length === 0 ? <p className="text-[var(--color-muted)]">{t("wallet.noReceipts")}</p> : null}
          {invoices.map((inv) => (
            <div key={inv.id} className="flex flex-wrap justify-between gap-x-4 gap-y-0.5 border-b border-[var(--color-border)] py-2">
              <span className="min-w-0 break-words">
                {inv.invoiceNumber} — {inv.description}
              </span>
              <span className="shrink-0 text-[var(--color-muted)]">
                +{inv.amountCredits ?? 0} · {formatDateTimeOrDash(inv.issuedAtUtc)}
              </span>
            </div>
          ))}
        </CardBody>
      </Card>

      <Card className="mt-4">
        <CardHeader>
          <CardTitle>{t("wallet.ledger")}</CardTitle>
        </CardHeader>
        <CardBody className="space-y-2 text-sm">
          {ledger.length === 0 ? <p className="text-[var(--color-muted)]">{t("wallet.emptyLedger")}</p> : null}
          {ledger.map((e) => (
            <div key={e.id} className="flex flex-wrap justify-between gap-x-4 gap-y-0.5 border-b border-[var(--color-border)] py-2">
              <span className="min-w-0 break-words">
                {e.entryType} {e.amountCredits > 0 ? `+${e.amountCredits}` : e.amountCredits}
              </span>
              <span className="shrink-0 text-[var(--color-muted)]">{formatDateTimeOrDash(e.createdAtUtc)}</span>
            </div>
          ))}
        </CardBody>
      </Card>
    </PageContent>
  );
}
