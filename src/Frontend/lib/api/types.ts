export type ApiMeta = {
  traceId?: string | null;
  correlationId?: string | null;
  timestampUtc: string;
  extensions?: Record<string, unknown> | null;
};

export type ApiErrorPayload = {
  code: string;
  message?: string | null;
  httpStatus?: number | null;
  validationErrors?: Record<string, string[]> | null;
  extensions?: Record<string, unknown> | null;
};

export type ApiResponse<T> = {
  success: boolean;
  data?: T | null;
  error?: ApiErrorPayload | null;
  meta: ApiMeta;
};

