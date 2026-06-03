import { NextRequest } from "next/server";
import { forwardToUpstream } from "@/lib/server/upstream-proxy";
import { proxyUpstreamResponse } from "@/lib/server/proxy-response";

const apiTarget = (process.env.API_PROXY_TARGET ?? "https://localhost:7124").replace(/\/$/, "");

async function proxy(req: NextRequest, ctx: { params: Promise<{ path: string[] }> }) {
  const { path } = await ctx.params;
  const targetUrl = new URL(`${apiTarget}/api/${path.join("/")}${req.nextUrl.search}`);

  const hasBody = req.method !== "GET" && req.method !== "HEAD";
  const body = hasBody ? Buffer.from(await req.arrayBuffer()) : undefined;

  const upstream = await forwardToUpstream(req, targetUrl, body);

  const responseHeaders = new Headers();
  for (const [key, value] of Object.entries(upstream.headers)) {
    if (value === undefined) continue;
    if (key.toLowerCase() === "transfer-encoding") continue;
    if (Array.isArray(value)) {
      for (const v of value) responseHeaders.append(key, v);
    } else {
      responseHeaders.set(key, value);
    }
  }

  return proxyUpstreamResponse(upstream.body, upstream.statusCode, responseHeaders);
}

export const GET = proxy;
export const POST = proxy;
export const PUT = proxy;
export const PATCH = proxy;
export const DELETE = proxy;
export const OPTIONS = proxy;
