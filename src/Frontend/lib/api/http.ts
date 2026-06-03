import type { ApiResponse } from "./types";
import { getApiBaseUrl } from "./config";
import { loadTokens, saveTokens } from "@/lib/auth/tokens";
import { IDEMPOTENCY_HEADER, newIdempotencyKey } from "./idempotency";
import { tenantHeaders } from "@/lib/tenancy/tenant-context";
import i18n from "@/i18n/config";

export const TURNSTILE_HEADER = "X-Turnstile-Token";

type RequestOptions = {
  method?: "GET" | "POST" | "PUT" | "DELETE";
  path: string;
  body?: unknown;
  query?: Record<string, string | number | boolean | undefined | null>;
  auth?: boolean;
  idempotent?: boolean;
  headers?: Record<string, string>;
  /** Cloudflare Turnstile challenge token. Sent as X-Turnstile-Token header when provided. */
  turnstileToken?: string | null;
};

function buildUrl(path: string, query?: RequestOptions["query"]) {
  const base = getApiBaseUrl().replace(/\/$/, "");
  const url = base
    ? new URL(base + path)
    : new URL(path, typeof window !== "undefined" ? window.location.origin : "http://localhost:3000");
  if (query) {
    for (const [k, v] of Object.entries(query)) {
      if (v === undefined || v === null) continue;
      url.searchParams.set(k, String(v));
    }
  }
  return url.toString();
}

async function parseJsonSafe<T>(res: Response): Promise<T> {
  const text = await res.text();
  if (!text) return {} as T;
  try {
    return JSON.parse(text) as T;
  } catch {
    const preview = text.replace(/\s+/g, " ").slice(0, 120);
    throw new Error(
      res.ok
        ? "API returned non-JSON response."
        : `API error ${res.status}: ${preview}`,
    );
  }
}

export async function apiRequest<T>(options: RequestOptions): Promise<ApiResponse<T>> {
  const url = buildUrl(options.path, options.query);
  const tokens = options.auth ? loadTokens() : null;

  const headers: Record<string, string> = {
    Accept: "application/json",
    "Accept-Language": i18n.language || "en",
    ...(options.body ? { "Content-Type": "application/json" } : {}),
    ...(options.headers ?? {}),
  };

  if (options.idempotent) headers[IDEMPOTENCY_HEADER] = newIdempotencyKey();
  if (tokens?.accessToken) headers.Authorization = `Bearer ${tokens.accessToken}`;
  if (options.auth) Object.assign(headers, tenantHeaders());
  if (options.turnstileToken) headers[TURNSTILE_HEADER] = options.turnstileToken;

  const res = await fetch(url, {
    method: options.method ?? "GET",
    headers,
    body: options.body ? JSON.stringify(options.body) : undefined,
    credentials: "omit",
  });

  // If access token expired, the backend returns standard envelope with 401/403.
  // We attempt a one-shot refresh for authenticated calls, then retry once.
  if (options.auth && res.status === 401) {
    const current = loadTokens();
    if (current?.refreshToken) {
      const refreshRes = await fetch(buildUrl("/api/v1/auth/refresh"), {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ refreshToken: current.refreshToken }),
      });
      if (refreshRes.ok) {
        const refreshed = await parseJsonSafe<ApiResponse<any>>(refreshRes);
        if (refreshed?.success && refreshed.data?.accessToken && refreshed.data?.refreshToken) {
          saveTokens({
            accessToken: refreshed.data.accessToken,
            accessTokenExpiresUtc: refreshed.data.accessTokenExpiresUtc,
            refreshToken: refreshed.data.refreshToken,
            refreshTokenExpiresUtc: refreshed.data.refreshTokenExpiresUtc,
          });

          const retryHeaders = { ...headers, Authorization: `Bearer ${loadTokens()!.accessToken}` };
          const retryRes = await fetch(url, {
            method: options.method ?? "GET",
            headers: retryHeaders,
            body: options.body ? JSON.stringify(options.body) : undefined,
          });
          return await parseJsonSafe<ApiResponse<T>>(retryRes);
        }
      }
    }
  }

  const envelope = await parseJsonSafe<ApiResponse<T>>(res);
  return envelope;
}

/** For endpoints with non-200 success (e.g. login → 202 for 2FA). */
export async function apiRaw<T>(
  path: string,
  init: RequestInit & { auth?: boolean; idempotent?: boolean; turnstileToken?: string | null } = {},
): Promise<{ status: number; envelope: ApiResponse<T> }> {
  const url = buildUrl(path);
  const tokens = init.auth ? loadTokens() : null;
  const headers: Record<string, string> = {
    Accept: "application/json",
    "Accept-Language": i18n.language || "en",
    "Content-Type": "application/json",
    ...(init.headers as Record<string, string> | undefined),
  };
  if (init.idempotent) headers[IDEMPOTENCY_HEADER] = newIdempotencyKey();
  if (tokens?.accessToken) headers.Authorization = `Bearer ${tokens.accessToken}`;
  if (init.turnstileToken) headers[TURNSTILE_HEADER] = init.turnstileToken;

  const res = await fetch(url, {
    ...init,
    headers,
    credentials: "omit",
  });
  const envelope = await parseJsonSafe<ApiResponse<T>>(res);
  return { status: res.status, envelope };
}

