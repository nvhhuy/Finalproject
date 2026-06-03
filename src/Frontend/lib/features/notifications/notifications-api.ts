import { apiRequest } from "@/lib/api/http";

export type NotificationDto = {
  id: string;
  title: string;
  body: string;
  linkUrl?: string | null;
  createdAtUtc: string;
  readAtUtc?: string | null;
};

export const notificationsApi = {
  async list(page = 1, pageSize = 20) {
    return await apiRequest<NotificationDto[]>({
      path: "/api/v1/notifications",
      auth: true,
      query: { page, pageSize },
    });
  },
  async unreadCount() {
    return await apiRequest<number>({
      path: "/api/v1/notifications/unread-count",
      auth: true,
    });
  },
  async vapidPublicKey() {
    return await apiRequest<string | null>({
      path: "/api/v1/notifications/push/vapid-public-key",
      auth: true,
    });
  },
  async subscribePush(body: { endpoint: string; keys: { p256dh: string; auth: string } }) {
    return await apiRequest<boolean>({
      method: "POST",
      path: "/api/v1/notifications/push/subscribe",
      auth: true,
      body,
    });
  },
  async unsubscribePush(endpoint: string) {
    return await apiRequest<boolean>({
      method: "DELETE",
      path: "/api/v1/notifications/push/subscribe",
      auth: true,
      body: { endpoint },
    });
  },
  async markRead(id: string) {
    return await apiRequest<boolean>({
      method: "POST",
      path: `/api/v1/notifications/${id}/read`,
      auth: true,
    });
  },
};
