import http from "node:http";
import https from "node:https";
import type { NextRequest } from "next/server";

export function forwardToUpstream(
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
