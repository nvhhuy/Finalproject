/** Formats a credit package price (smallest currency unit). */
export function formatPackagePrice(priceCents: number, currency: string): string {
  if (currency === "VND") {
    return new Intl.NumberFormat(undefined, {
      style: "currency",
      currency: "VND",
      maximumFractionDigits: 0,
    }).format(priceCents);
  }
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(priceCents / 100);
  } catch {
    return `${(priceCents / 100).toFixed(2)} ${currency}`;
  }
}
