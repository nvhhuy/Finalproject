import { apiRequest } from "@/lib/api/http";

export type WalletBalanceDto = {
  walletId: string;
  balanceCredits: number;
};

export type WalletLedgerEntryDto = {
  id: string;
  entryType: string;
  amountCredits: number;
  balanceAfterCredits: number;
  actionCode?: string | null;
  description?: string | null;
  createdAtUtc: string;
};

export type MeteredActionDto = {
  code: string;
  costCredits: number;
  description: string;
};

export type WalletDebitResponse = {
  success: boolean;
  chargedCredits: number;
  remainingBalance: number;
  denyReason?: string | null;
};

export type CreditPackageDto = {
  id: string;
  code: string;
  name: string;
  description: string | null;
  priceCents: number;
  currency: string;
  creditsAmount: number;
  isActive: boolean;
  sortOrder: number;
};

export type BillingInvoiceDto = {
  id: string;
  tenantId?: string | null;
  userId?: string | null;
  invoiceNumber: string;
  description: string;
  amountCents: number;
  amountCredits?: number | null;
  currency: string;
  status: string;
  provider: string;
  issuedAtUtc: string;
};

export const walletApi = {
  listPackages() {
    return apiRequest<CreditPackageDto[]>({ path: "/api/v1/public/credit-packages" });
  },
  getBalance() {
    return apiRequest<WalletBalanceDto>({ path: "/api/v1/wallet/balance", auth: true });
  },
  getLedger(page = 1, pageSize = 20) {
    return apiRequest<WalletLedgerEntryDto[]>({
      path: "/api/v1/wallet/ledger",
      auth: true,
      query: { page, pageSize },
    });
  },
  listActions() {
    return apiRequest<MeteredActionDto[]>({ path: "/api/v1/wallet/actions", auth: true });
  },
  topUp(amountCredits: number) {
    return apiRequest<WalletBalanceDto>({
      path: "/api/v1/wallet/top-up",
      method: "POST",
      body: { amountCredits },
      auth: true,
      idempotent: true,
    });
  },
  purchasePackage(packageCode: string) {
    return apiRequest<WalletBalanceDto>({
      path: "/api/v1/wallet/purchase",
      method: "POST",
      body: { packageCode },
      auth: true,
      idempotent: true,
    });
  },
  debit(actionCode: string) {
    return apiRequest<WalletDebitResponse>({
      path: "/api/v1/wallet/debit",
      method: "POST",
      body: { actionCode },
      auth: true,
      idempotent: true,
    });
  },
  listInvoices(page = 1, pageSize = 20) {
    return apiRequest<BillingInvoiceDto[]>({
      path: "/api/v1/wallet/invoices",
      auth: true,
      query: { page, pageSize },
    });
  },
};
