/** ISO 4217 codes supported in the plan catalog (Stripe-compatible subset). */
export const PLAN_CURRENCIES = [
  'USD',
  'EUR',
  'GBP',
  'VND',
  'JPY',
  'CNY',
  'AUD',
  'CAD',
  'SGD',
  'HKD',
  'INR',
  'KRW',
  'THB',
  'MYR',
  'IDR',
  'PHP',
  'CHF',
  'NZD',
] as const

export type PlanCurrency = (typeof PLAN_CURRENCIES)[number]
