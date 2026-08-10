"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { PageContent, PageHeader } from "@/components/ui/page-header";
import { formatDateTimeOrDash } from "@/lib/shared";
import { getApiErrorMessageFromPayload } from "@/lib/errors";
import { useDebouncedValue } from "@/lib/hooks/useDebouncedValue";
import { formTextareaClass } from "@/lib/ui/form-styles";
import { booksApi, type BookDto, type BookInput } from "@/lib/features/books/books-api";

const EMPTY_FORM: BookInput = {
  title: "",
  author: "",
  isbn: "",
  description: "",
  publishedYear: null,
};

export default function BooksPage() {
  const { t } = useTranslation();
  const qc = useQueryClient();

  const [search, setSearch] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingRowVersion, setEditingRowVersion] = useState<number | null>(null);
  const [form, setForm] = useState<BookInput>(EMPTY_FORM);

  const searchDebounced = useDebouncedValue(search, 350);
  const booksQueryKey = ["books", searchDebounced] as const;

  const booksQuery = useQuery({
    queryKey: booksQueryKey,
    queryFn: async () => {
      const res = await booksApi.list(1, 50, searchDebounced || undefined);
      if (!res.success || !res.data) throw new Error(res.error?.message ?? t("errors.requestFailed"));
      return res.data;
    },
  });

  const items = booksQuery.data?.items ?? [];
  const isEditing = editingId !== null;

  const invalidate = () => qc.invalidateQueries({ queryKey: ["books"] });

  const resetForm = () => {
    setForm(EMPTY_FORM);
    setEditingId(null);
    setEditingRowVersion(null);
  };

  const startEdit = (book: BookDto) => {
    setEditingId(book.id);
    setEditingRowVersion(book.rowVersion);
    setForm({
      title: book.title,
      author: book.author,
      isbn: book.isbn ?? "",
      description: book.description ?? "",
      publishedYear: book.publishedYear ?? null,
    });
    setFormError(null);
  };

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (!form.title.trim() || !form.author.trim()) {
        throw new Error(t("books.validation.titleAuthorRequired"));
      }
      const payload: BookInput = {
        title: form.title.trim(),
        author: form.author.trim(),
        isbn: form.isbn?.trim() || null,
        description: form.description?.trim() || null,
        publishedYear: form.publishedYear ?? null,
      };
      if (editingId !== null && editingRowVersion !== null) {
        const res = await booksApi.update(editingId, { ...payload, rowVersion: editingRowVersion });
        if (!res.success) throw new Error(getApiErrorMessageFromPayload(res.error));
      } else {
        const res = await booksApi.create(payload);
        if (!res.success) throw new Error(getApiErrorMessageFromPayload(res.error));
      }
    },
    onSuccess: () => {
      setFormError(null);
      resetForm();
      void invalidate();
    },
    onError: (e) => setFormError(e instanceof Error ? e.message : t("errors.requestFailed")),
  });

  const deleteMutation = useMutation({
    mutationFn: async ({ id, rowVersion }: { id: string; rowVersion: number }) => {
      const res = await booksApi.remove(id, rowVersion);
      if (!res.success) throw new Error(getApiErrorMessageFromPayload(res.error));
    },
    onSuccess: (_data, variables) => {
      setFormError(null);
      if (editingId === variables.id) resetForm();
      void invalidate();
    },
    onError: (e) => setFormError(e instanceof Error ? e.message : t("errors.requestFailed")),
  });

  return (
    <PageContent>
      <PageHeader title={t("books.title")} description={t("books.description")} />

      <div className="grid gap-4 lg:grid-cols-[1fr_1.4fr]">
        <Card>
          <CardHeader>
            <CardTitle>{isEditing ? t("books.editBook") : t("books.newBook")}</CardTitle>
          </CardHeader>
          <CardBody className="space-y-3">
            <Input
              placeholder={t("books.fields.title")}
              value={form.title}
              onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
            />
            <Input
              placeholder={t("books.fields.author")}
              value={form.author}
              onChange={(e) => setForm((f) => ({ ...f, author: e.target.value }))}
            />
            <Input
              placeholder={t("books.fields.isbn")}
              value={form.isbn ?? ""}
              onChange={(e) => setForm((f) => ({ ...f, isbn: e.target.value }))}
            />
            <Input
              type="number"
              placeholder={t("books.fields.publishedYear")}
              value={form.publishedYear ?? ""}
              onChange={(e) =>
                setForm((f) => ({
                  ...f,
                  publishedYear: e.target.value === "" ? null : Number(e.target.value),
                }))
              }
            />
            <textarea
              className={formTextareaClass}
              placeholder={t("books.fields.description")}
              value={form.description ?? ""}
              onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
            />

            {formError ? <Alert variant="error">{formError}</Alert> : null}

            <div className="flex gap-2">
              <Button loading={saveMutation.isPending} onClick={() => saveMutation.mutate()}>
                {isEditing ? t("books.save") : t("books.create")}
              </Button>
              {isEditing ? (
                <Button variant="secondary" onClick={resetForm}>
                  {t("books.cancel")}
                </Button>
              ) : null}
            </div>
          </CardBody>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t("books.myBooks")}</CardTitle>
          </CardHeader>
          <CardBody className="space-y-3">
            <Input
              placeholder={t("books.searchPlaceholder")}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />

            {booksQuery.error ? (
              <Alert variant="error">{(booksQuery.error as Error).message}</Alert>
            ) : null}

            {booksQuery.isLoading ? (
              <div className="space-y-2">
                {Array.from({ length: 3 }).map((_, i) => (
                  <div key={i} className="h-16 animate-pulse rounded-lg bg-[var(--color-surface-muted)]" />
                ))}
              </div>
            ) : items.length === 0 ? (
              <p className="text-sm text-[var(--color-muted)]">{t("books.empty")}</p>
            ) : (
              <ul className="max-h-[28rem] space-y-2 overflow-auto">
                {items.map((book) => (
                  <li
                    key={book.id}
                    className="rounded-lg border border-[var(--color-border)] px-3 py-2"
                  >
                    <div className="flex flex-wrap items-start justify-between gap-2">
                      <div className="min-w-0">
                        <p className="truncate text-sm font-semibold">{book.title}</p>
                        <p className="truncate text-xs text-[var(--color-muted)]">
                          {book.author}
                          {book.publishedYear ? ` · ${book.publishedYear}` : ""}
                        </p>
                        <p className="text-xs text-[var(--color-muted)]">
                          {t("books.updatedAt")} {formatDateTimeOrDash(book.updatedAtUtc)}
                        </p>
                      </div>
                      <div className="flex shrink-0 gap-2">
                        <Button size="sm" variant="secondary" onClick={() => startEdit(book)}>
                          {t("books.edit")}
                        </Button>
                        <Button
                          size="sm"
                          variant="danger"
                          loading={deleteMutation.isPending}
                          onClick={() => deleteMutation.mutate({ id: book.id, rowVersion: book.rowVersion })}
                        >
                          {t("books.delete")}
                        </Button>
                      </div>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </CardBody>
        </Card>
      </div>
    </PageContent>
  );
}
