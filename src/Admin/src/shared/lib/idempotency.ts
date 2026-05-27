/** RFC-friendly idempotency key for mutating admin requests. */
export function newIdempotencyKey(): string {
  return crypto.randomUUID()
}
