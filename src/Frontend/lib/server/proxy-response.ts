import { NextResponse } from "next/server";

/**
 * Next.js Response rejects some upstream statuses (e.g. 204) that SignalR/long-polling use.
 */
export function proxyUpstreamResponse(
  body: Buffer,
  statusCode: number,
  headers: Headers,
): NextResponse {
  const status =
    statusCode === 204 ? 200 : statusCode >= 200 && statusCode <= 599 ? statusCode : 502;

  return new NextResponse(new Uint8Array(body), { status, headers });
}
