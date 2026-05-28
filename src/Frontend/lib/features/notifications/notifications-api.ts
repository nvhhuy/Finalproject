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
  async markRead(id: string) {
    return await apiRequest<boolean>({
      method: "POST",
      path: `/api/v1/notifications/${id}/read`,
      auth: true,
    });
  },
};
