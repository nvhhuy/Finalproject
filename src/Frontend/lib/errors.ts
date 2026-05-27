import type { ApiErrorPayload } from "@/lib/api/types";

export class ApiClientError extends Error {
  readonly code: string;
  readonly httpStatus?: number;
  readonly validationErrors?: Record<string, string[]>;

  constructor(error: ApiErrorPayload) {
    super(error.message ?? error.code);
    this.name = "ApiClientError";
    this.code = error.code;
    this.httpStatus = error.httpStatus ?? undefined;
    this.validationErrors = error.validationErrors ?? undefined;
  }
}

export function isConcurrencyError(err: unknown): boolean {
  if (!(err instanceof ApiClientError)) return false;
  const msg = (err.message ?? "").toLowerCase();
  return err.httpStatus === 409 || msg.includes("concurrency") || msg.includes("row version");
}
