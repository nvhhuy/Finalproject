/* Service worker: Web Push + notification click (scope: /). */

self.addEventListener("push", (event) => {
  let data = { title: "Notification", body: "", linkUrl: "/app/notifications" };
  try {
    if (event.data) {
      const parsed = event.data.json();
      data = { ...data, ...parsed };
    }
  } catch {
    if (event.data) data.body = event.data.text();
  }

  const title = data.title || "Notification";
  const body = data.body || "";
  const url = data.linkUrl || "/app/notifications";

  event.waitUntil(
    self.registration.showNotification(title, {
      body,
      icon: "/favicon.svg",
      badge: "/favicon.svg",
      data: { url },
      tag: data.id ? String(data.id) : undefined,
    }),
  );
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  const url = event.notification.data?.url || "/app/notifications";
  event.waitUntil(
    clients.matchAll({ type: "window", includeUncontrolled: true }).then((clientList) => {
      for (const client of clientList) {
        if (client.url.includes(self.location.origin) && "focus" in client) {
          client.navigate(url);
          return client.focus();
        }
      }
      if (clients.openWindow) return clients.openWindow(url);
    }),
  );
});
