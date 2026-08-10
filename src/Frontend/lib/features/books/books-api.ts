import { apiRequest } from "@/lib/api/http";

export type BookDto = {
  id: string;
  title: string;
  author: string;
  isbn?: string | null;
  description?: string | null;
  publishedYear?: number | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  rowVersion: number;
};

export type BookListDto = {
  items: BookDto[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export type BookInput = {
  title: string;
  author: string;
  isbn?: string | null;
  description?: string | null;
  publishedYear?: number | null;
};

export const booksApi = {
  async list(page = 1, pageSize = 20, search?: string) {
    return await apiRequest<BookListDto>({
      path: "/api/v1/books",
      auth: true,
      query: { page, pageSize, search },
    });
  },

  async detail(bookId: string) {
    return await apiRequest<BookDto>({
      path: `/api/v1/books/${bookId}`,
      auth: true,
    });
  },

  async create(body: BookInput) {
    return await apiRequest<BookDto>({
      method: "POST",
      path: "/api/v1/books",
      auth: true,
      idempotent: true,
      body,
    });
  },

  async update(bookId: string, body: BookInput & { rowVersion: number }) {
    return await apiRequest<BookDto>({
      method: "PUT",
      path: `/api/v1/books/${bookId}`,
      auth: true,
      body,
    });
  },

  async remove(bookId: string, rowVersion: number) {
    return await apiRequest<boolean>({
      method: "DELETE",
      path: `/api/v1/books/${bookId}`,
      auth: true,
      query: { rowVersion },
    });
  },
};
