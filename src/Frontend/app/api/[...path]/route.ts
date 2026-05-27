import http from "node:http";
import https from "node:https";
import { NextRequest, NextResponse } from "next/server";

const apiTarget = (process.env.API_PROXY_TARGET ?? "https://localhost:7124").replace(/\/$/, "");

function forward(
  req: NextRequest,
  targetUrl: URL,
  body: Buffer | undefined,
): Promise<{ statusCode: number; headers: http.IncomingHttpHeaders; body: Buffer }> {
  return new Promise((resolve, reject) => {
    const isHttps = targetUrl.protocol === "https:";
    const lib = isHttps ? https : http;
    const port = targetUrl.port ? Number(targetUrl.port) : isHttps ? 443 : 80;

    const headers: Record<string, string> = {};
    req.headers.forEach((value, key) => {
      const lower = key.toLowerCase();
      if (lower === "host" || lower === "connection" || lower === "content-length") return;
      headers[key] = value;
    });

    const upstream = lib.request(
      {
        hostname: targetUrl.hostname,
        port,
        path: `${targetUrl.pathname}${targetUrl.search}`,
        method: req.method,
        headers,
        rejectUnauthorized: process.env.NODE_ENV !== "development",
      },
      (res) => {
        const chunks: Buffer[] = [];
        res.on("data", (chunk: Buffer) => chunks.push(chunk));
        res.on("end", () =>
          resolve({
            statusCode: res.statusCode ?? 500,
            headers: res.headers,
            body: Buffer.concat(chunks),
          }),
        );
      },
    );
    upstream.on("error", reject);
    if (body) upstream.write(body);
    upstream.end();
  });
}

async function proxy(req: NextRequest, ctx: { params: Promise<{ path: string[] }> }) {
  const { path } = await ctx.params;
  const targetUrl = new URL(`${apiTarget}/api/${path.join("/")}${req.nextUrl.search}`);

  const hasBody = req.method !== "GET" && req.method !== "HEAD";
  const body = hasBody ? Buffer.from(await req.arrayBuffer()) : undefined;

  const upstream = await forward(req, targetUrl, body);

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

  return new NextResponse(new Uint8Array(upstream.body), {
    status: upstream.statusCode,
    headers: responseHeaders,
  });
}

export const GET = proxy;
export const POST = proxy;
export const PUT = proxy;
export const PATCH = proxy;
export const DELETE = proxy;
export const OPTIONS = proxy;
