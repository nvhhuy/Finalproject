import { apiRequest } from "@/lib/api/http";

export type SupportTicketStatus = "Open" | "PendingUser" | "PendingSupport" | "Resolved" | "Closed";
export type SupportTicketPriority = "Low" | "Normal" | "High" | "Urgent";

export type SupportTicketSummaryDto = {
  id: string;
  ticketNumber: string;
  subject: string;
  category?: string | null;
  priority: SupportTicketPriority;
  status: SupportTicketStatus;
  assignedAdminUserId?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type SupportTicketAttachmentDto = {
  id: string;
  fileName: string;
  contentType: string;
  byteSize: number;
  storageKey: string;
};

export type SupportTicketMessageDto = {
  id: string;
  authorUserId: string;
  body: string;
  isInternalNote: boolean;
  createdAtUtc: string;
  attachments: SupportTicketAttachmentDto[];
};

export type SupportTicketDetailDto = {
  id: string;
  ticketNumber: string;
  createdByUserId: string;
  assignedAdminUserId?: string | null;
  subject: string;
  description: string;
  category?: string | null;
  priority: SupportTicketPriority;
  status: SupportTicketStatus;
  createdAtUtc: string;
  updatedAtUtc: string;
  firstResponseAtUtc?: string | null;
  closedAtUtc?: string | null;
  messages: SupportTicketMessageDto[];
};

export type SupportTicketListDto = {
  items: SupportTicketSummaryDto[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export const supportApi = {
  async list(page = 1, pageSize = 20, search?: string, status?: SupportTicketStatus) {
    return await apiRequest<SupportTicketListDto>({
      path: "/api/v1/support/tickets",
      auth: true,
      query: { page, pageSize, search, status },
    });
  },

  async detail(ticketId: string) {
    return await apiRequest<SupportTicketDetailDto>({
      path: `/api/v1/support/tickets/${ticketId}`,
      auth: true,
    });
  },

  async create(body: {
    subject: string;
    description: string;
    category?: string;
    priority?: SupportTicketPriority;
  }) {
    return await apiRequest<string>({
      method: "POST",
      path: "/api/v1/support/tickets",
      auth: true,
      idempotent: true,
      body,
    });
  },

  async addMessage(
    ticketId: string,
    body: string,
    attachments?: Array<{
      storageKey: string;
      fileName: string;
      contentType: string;
      byteSize: number;
    }>,
  ) {
    return await apiRequest<boolean>({
      method: "POST",
      path: `/api/v1/support/tickets/${ticketId}/messages`,
      auth: true,
      idempotent: true,
      body: { body, attachments: attachments?.length ? attachments : undefined },
    });
  },

  async close(ticketId: string) {
    return await apiRequest<boolean>({
      method: "POST",
      path: `/api/v1/support/tickets/${ticketId}/close`,
      auth: true,
      idempotent: true,
    });
  },

  async reopen(ticketId: string) {
    return await apiRequest<boolean>({
      method: "POST",
      path: `/api/v1/support/tickets/${ticketId}/reopen`,
      auth: true,
      idempotent: true,
    });
  },
};
