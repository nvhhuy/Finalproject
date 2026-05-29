import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { extractTenantSlugFromHost, getTenantSubdomainBaseHost } from "@/lib/tenancy/host";

async function resolveCustomDomainSlug(request: NextRequest, host: string): Promise<string | null> {
  try {
    const url = new URL(
      `/api/v1/public/tenants/resolve-host?host=${encodeURIComponent(host)}`,
      request.url,
    );
    const response = await fetch(url, { headers: { accept: "application/json" } });
    if (!response.ok) return null;
    const payload = (await response.json()) as { data?: { slug?: string } };
    return payload.data?.slug ?? null;
  } catch {
    return null;
  }
}

function isLocalDevHost(host: string): boolean {
  const hostname = host.split(":")[0]?.trim().toLowerCase() ?? "";
  return hostname === "localhost" || hostname === "127.0.0.1" || hostname === "[::1]";
}

export async function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;
  // Must run before any internal fetch — otherwise resolve-host re-enters middleware and loops.
  if (pathname.startsWith("/api") || pathname.startsWith("/_next")) {
    return NextResponse.next();
  }

  const host = request.headers.get("host") ?? "";
  const baseHost = getTenantSubdomainBaseHost();
  let slug = baseHost ? extractTenantSlugFromHost(host, baseHost) : null;
  if (!slug && host && !isLocalDevHost(host)) {
    slug = await resolveCustomDomainSlug(request, host);
  }
  if (!slug) return NextResponse.next();

  if (pathname.startsWith(`/t/${slug}`)) {
    const response = NextResponse.next();
    response.headers.set("x-tenant-slug", slug);
    return response;
  }

  const rewritePath =
    pathname === "/" || pathname === "" ? `/t/${slug}` : `/t/${slug}${pathname}`;

  const url = request.nextUrl.clone();
  url.pathname = rewritePath;
  const response = NextResponse.rewrite(url);
  response.headers.set("x-tenant-slug", slug);
  return response;
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
};
