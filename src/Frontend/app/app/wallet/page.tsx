"use client";

import { useEffect, useState } from "react";
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
  type BillingInvoiceDto,
  type CreditPackageDto,
  type MeteredActionDto,
  type WalletLedgerEntryDto,
} from "@/lib/features/wallet/wallet-api";

export default function WalletPage() {
  const { t } = useTranslation();
  const [balance, setBalance] = useState<number | null>(null);
  const [ledger, setLedger] = useState<WalletLedgerEntryDto[]>([]);
  const [actions, setActions] = useState<MeteredActionDto[]>([]);
  const [packages, setPackages] = useState<CreditPackageDto[]>([]);
  const [topUpAmount, setTopUpAmount] = useState("100");
  const [error, setError] = useState<string | null>(null);
  const [invoices, setInvoices] = useState<BillingInvoiceDto[]>([]);
  const [loading, setLoading] = useState(true);

  async function reload() {
    setLoading(true);
    setError(null);
    const [balRes, ledgerRes, actionsRes, invoicesRes, packagesRes] = await Promise.all([
      walletApi.getBalance(),
      walletApi.getLedger(1, 20),
      walletApi.listActions(),
      walletApi.listInvoices(1, 20),
      walletApi.listPackages(),
    ]);
    if (!balRes.success || !balRes.data) {
      setError(balRes.error?.message ?? t("errors.requestFailed"));
    } else {
      setBalance(balRes.data.balanceCredits);
    }
    if (ledgerRes.success && ledgerRes.data) setLedger(ledgerRes.data);
    if (actionsRes.success && actionsRes.data) setActions(actionsRes.data);
    if (invoicesRes.success && invoicesRes.data) setInvoices(invoicesRes.data);
    if (packagesRes.success && packagesRes.data) setPackages(packagesRes.data);
    setLoading(false);
  }

  async function handlePurchase(packageCode: string) {
    const res = await walletApi.purchasePackage(packageCode);
    if (!res.success || !res.data) {
      setError(res.error?.message ?? t("errors.requestFailed"));
      return;
    }
    setBalance(res.data.balanceCredits);
    await reload();
  }

  useEffect(() => {
    void reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function handleTopUp() {
    const amount = Number(topUpAmount);
    if (!Number.isFinite(amount) || amount <= 0) return;
    const res = await walletApi.topUp(amount);
    if (!res.success || !res.data) {
      setError(res.error?.message ?? t("errors.requestFailed"));
      return;
    }
    setBalance(res.data.balanceCredits);
    await reload();
  }

  async function handleDebit(code: string) {
    const res = await walletApi.debit(code);
    if (!res.success || !res.data) {
      setError(res.error?.message ?? t("errors.requestFailed"));
      return;
    }
    if (!res.data.success) {
      setError(res.data.denyReason ?? t("wallet.debitFailed"));
      return;
    }
    setBalance(res.data.remainingBalance);
    await reload();
  }

  if (loading) {
    return (
      <PageContent>
        <PageHeader title={t("wallet.title")} description={t("wallet.subtitle")} />
        <WalletSkeleton />
      </PageContent>
    );
  }

  return (
    <PageContent>
      <PageHeader title={t("wallet.title")} description={t("wallet.subtitle")} />
      {error ? <Alert variant="error">{error}</Alert> : null}

      <Card>
        <CardHeader>
          <CardTitle>{t("wallet.balance")}</CardTitle>
        </CardHeader>
        <CardBody>
          <p className="text-3xl font-semibold">{balance ?? 0}</p>
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
              <Button size="sm" onClick={() => void handlePurchase(pkg.code)}>
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
            <Button variant="secondary" onClick={() => void handleTopUp()}>
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
              <Button size="sm" variant="secondary" onClick={() => void handleDebit(a.code)}>
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
            <div key={inv.id} className="flex justify-between gap-2 border-b border-[var(--color-border)] py-2">
              <span>
                {inv.invoiceNumber} — {inv.description}
              </span>
              <span className="text-[var(--color-muted)]">
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
            <div key={e.id} className="flex justify-between gap-2 border-b border-[var(--color-border)] py-2">
              <span>
                {e.entryType} {e.amountCredits > 0 ? `+${e.amountCredits}` : e.amountCredits}
              </span>
              <span className="text-[var(--color-muted)]">{formatDateTimeOrDash(e.createdAtUtc)}</span>
            </div>
          ))}
        </CardBody>
      </Card>
    </PageContent>
  );
}
