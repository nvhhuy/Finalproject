/**
 * Same pattern as Admin: browser calls same-origin `/api/*`; Next rewrites to the API host.
 * Default dev target: https://localhost:7124 (see API_PROXY_TARGET in next.config.js).
 */
export function getApiBaseUrl() {
  const configured = process.env.NEXT_PUBLIC_API_BASE_URL?.trim();

  if (typeof window !== "undefined") {
    return configured && configured.length > 0 ? configured : "";
  }
  return configured && configured.length > 0 ? configured : "https://localhost:7124";
}

