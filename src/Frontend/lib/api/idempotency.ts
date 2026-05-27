export const IDEMPOTENCY_HEADER = "Idempotency-Key";

export function newIdempotencyKey() {
  // Use Web Crypto when available (browser + modern runtimes).
  return globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

