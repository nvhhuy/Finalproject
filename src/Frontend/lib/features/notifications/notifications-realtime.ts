import type { HubConnection } from "@microsoft/signalr";
import { getNotificationsHubUrl } from "@/lib/api/config";
import { loadTokens } from "@/lib/auth/tokens";

export type LiveNotificationPayload = {
  id: string;
  title: string;
  body: string;
  linkUrl?: string | null;
  createdAtUtc: string;
};

function hubUsesDevProxy(hubUrl: string) {
  return hubUrl.startsWith("/");
}

export async function probeNotificationsHub(accessToken: string): Promise<boolean> {
  const negotiateUrl = `${getNotificationsHubUrl().replace(/\/$/, "")}/negotiate?negotiateVersion=1`;
  try {
    const res = await fetch(negotiateUrl, {
      method: "POST",
      headers: {
        Authorization: `Bearer ${accessToken}`,
        "Content-Type": "application/json",
      },
    });
    return res.ok;
  } catch {
    return false;
  }
}

export async function startNotificationsHub(
  accessToken: string,
  onNotification: (payload: LiveNotificationPayload) => void,
): Promise<HubConnection | null> {
  if (!(await probeNotificationsHub(accessToken))) return null;

  const { HttpTransportType, HubConnectionBuilder, LogLevel } = await import("@microsoft/signalr");
  const hubUrl = getNotificationsHubUrl();

  const conn = new HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: () => loadTokens()?.accessToken ?? "",
      headers: { Authorization: `Bearer ${accessToken}` },
      ...(hubUsesDevProxy(hubUrl) ? { transport: HttpTransportType.LongPolling } : {}),
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.None)
    .build();

  conn.on("notification", onNotification);

  try {
    await conn.start();
    return conn;
  } catch {
    try {
      await conn.stop();
    } catch {
      // ignore stop errors on failed start
    }
    return null;
  }
}
