"use client";

import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { PageContent, PageHeader } from "@/components/ui/page-header";
import { SupportSkeleton } from "@/components/ui/skeleton";
import { formatDateTimeOrDash } from "@/lib/shared";
import { getApiErrorMessageFromPayload } from "@/lib/errors";
import { useDebouncedValue } from "@/lib/hooks/useDebouncedValue";
import { formSelectClass, formTextareaClass } from "@/lib/ui/form-styles";
import {
  SUPPORT_CATEGORY_VALUES,
  type SupportCategory,
} from "@/lib/features/support/support-categories";
import { uploadSupportAttachment } from "@/lib/features/support/support-attachments";
import {
  supportApi,
  type SupportTicketDetailDto,
  type SupportTicketPriority,
  type SupportTicketStatus,
} from "@/lib/features/support/support-api";

const STATUS_OPTIONS: SupportTicketStatus[] = ["Open", "PendingSupport", "PendingUser", "Resolved", "Closed"];
const PRIORITY_OPTIONS: SupportTicketPriority[] = ["Low", "Normal", "High", "Urgent"];

export default function SupportPage() {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const [mutationError, setMutationError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState<SupportTicketStatus | "">("");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [subject, setSubject] = useState("");
  const [description, setDescription] = useState("");
  const [category, setCategory] = useState<SupportCategory>("account");
  const [priority, setPriority] = useState<SupportTicketPriority>("Normal");
  const [reply, setReply] = useState("");
  const [createFiles, setCreateFiles] = useState<File[]>([]);
  const [replyFiles, setReplyFiles] = useState<File[]>([]);

  const searchDebounced = useDebouncedValue(search, 350);

  const ticketsQueryKey = ["support", "tickets", searchDebounced, status] as const;

  const ticketsQuery = useQuery({
    queryKey: ticketsQueryKey,
    queryFn: async () => {
      const res = await supportApi.list(1, 50, searchDebounced || undefined, status || undefined);
      if (!res.success || !res.data) throw new Error(res.error?.message ?? t("errors.requestFailed"));
      const details = await Promise.all(
        res.data.items.map(async (it) => {
          const d = await supportApi.detail(it.id);
          return d.success && d.data ? d.data : null;
        }),
      );
      return details.filter(Boolean) as SupportTicketDetailDto[];
    },
  });

  const items = ticketsQuery.data ?? [];

  const selected = useMemo(() => items.find((x) => x.id === selectedId) ?? null, [items, selectedId]);
  const canReply = selected && selected.status !== "Closed";
  const canClose = selected && selected.status !== "Closed";
  const canReopen = selected && selected.status === "Closed";

  // Auto-select first ticket when list changes
  const resolvedId = useMemo(() => {
    if (!ticketsQuery.isSuccess) return selectedId;
    if (selectedId && items.some((x) => x.id === selectedId)) return selectedId;
    return items[0]?.id ?? null;
  }, [items, selectedId, ticketsQuery.isSuccess]);

  if (resolvedId !== selectedId) setSelectedId(resolvedId);

  const invalidate = () => qc.invalidateQueries({ queryKey: ["support", "tickets"] });

  const createMutation = useMutation({
    mutationFn: async () => {
      if (!subject.trim() || !description.trim()) throw new Error("Subject and description required");
      const res = await supportApi.create({
        subject: subject.trim(),
        description: description.trim(),
        category,
        priority,
      });
      if (!res.success || !res.data) throw new Error(res.error?.message ?? t("errors.requestFailed"));
      if (createFiles.length > 0) {
        const attachments = await Promise.all(createFiles.map((f) => uploadSupportAttachment(f)));
        await supportApi.addMessage(res.data, t("support.attachmentsMessage"), attachments);
      }
      return res.data;
    },
    onSuccess: (ticketId) => {
      setMutationError(null);
      setSubject("");
      setDescription("");
      setCategory("account");
      setPriority("Normal");
      setCreateFiles([]);
      void invalidate();
      setSelectedId(ticketId);
    },
    onError: (e) => setMutationError(e instanceof Error ? e.message : t("errors.requestFailed")),
  });

  const replyMutation = useMutation({
    mutationFn: async () => {
      if (!selectedId || !reply.trim()) return;
      const attachments =
        replyFiles.length > 0 ? await Promise.all(replyFiles.map((f) => uploadSupportAttachment(f))) : undefined;
      const res = await supportApi.addMessage(selectedId, reply.trim(), attachments);
      if (!res.success) throw new Error(res.error?.message ?? t("errors.requestFailed"));
    },
    onSuccess: () => {
      setMutationError(null);
      setReply("");
      setReplyFiles([]);
      void invalidate();
    },
    onError: (e) => setMutationError(e instanceof Error ? e.message : t("errors.requestFailed")),
  });

  const closeMutation = useMutation({
    mutationFn: async () => {
      if (!selectedId) return;
      const res = await supportApi.close(selectedId);
      if (!res.success) throw new Error(res.error?.message ?? t("errors.requestFailed"));
    },
    onSuccess: () => { setMutationError(null); void invalidate(); },
    onError: (e) => setMutationError(e instanceof Error ? e.message : t("errors.requestFailed")),
  });

  const reopenMutation = useMutation({
    mutationFn: async () => {
      if (!selectedId) return;
      const res = await supportApi.reopen(selectedId);
      if (!res.success) throw new Error(res.error?.message ?? t("errors.requestFailed"));
    },
    onSuccess: () => { setMutationError(null); void invalidate(); },
    onError: (e) => setMutationError(e instanceof Error ? e.message : t("errors.requestFailed")),
  });

  const statusBusy = closeMutation.isPending || reopenMutation.isPending;

  if (ticketsQuery.isLoading && items.length === 0) {
    return (
      <PageContent>
        <PageHeader title={t("support.title")} description={t("support.description")} />
        <SupportSkeleton />
      </PageContent>
    );
  }

  return (
    <PageContent>
      <PageHeader title={t("support.title")} description={t("support.description")} />

      <div className="grid gap-4 lg:grid-cols-[1.1fr_1.4fr]">
        <Card>
          <CardHeader>
            <CardTitle>{t("support.newTicket")}</CardTitle>
          </CardHeader>
          <CardBody className="space-y-3">
            <Input placeholder={t("support.subject")} value={subject} onChange={(e) => setSubject(e.target.value)} />
            <textarea
              className={formTextareaClass}
              placeholder={t("support.descriptionInput")}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              <div className="grid gap-1.5">
                <label htmlFor="ticket-category" className="text-xs font-medium text-[var(--color-muted)]">
                  {t("support.category")}
                </label>
                <select
                  id="ticket-category"
                  className={formSelectClass}
                  value={category}
                  onChange={(e) => setCategory(e.target.value as SupportCategory)}
                >
                  {SUPPORT_CATEGORY_VALUES.map((c) => (
                    <option key={c} value={c}>
                      {t(`support.categories.${c}`)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="grid gap-1.5">
                <label htmlFor="ticket-priority" className="text-xs font-medium text-[var(--color-muted)]">
                  {t("support.priority")}
                </label>
                <select
                  id="ticket-priority"
                  className={formSelectClass}
                  value={priority}
                  onChange={(e) => setPriority(e.target.value as SupportTicketPriority)}
                >
                  {PRIORITY_OPTIONS.map((p) => (
                    <option key={p} value={p}>
                      {t(`support.priorities.${p}`)}
                    </option>
                  ))}
                </select>
              </div>
            </div>
            <label className="block text-sm">
              <span className="text-[var(--color-muted)]">{t("support.attachments")}</span>
              <input
                type="file"
                multiple
                className="mt-1 block w-full text-sm"
                onChange={(e) => setCreateFiles(Array.from(e.target.files ?? []))}
              />
            </label>
            <Button loading={createMutation.isPending} onClick={() => createMutation.mutate()}>
              {t("support.create")}
            </Button>
          </CardBody>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t("support.myTickets")}</CardTitle>
          </CardHeader>
          <CardBody className="space-y-3">
            <div className="flex flex-wrap gap-2">
              <Input
                className="min-w-[140px] flex-1"
                placeholder={t("support.searchPlaceholder")}
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
              <select
                className={formSelectClass}
                value={status}
                onChange={(e) => setStatus(e.target.value as SupportTicketStatus | "")}
                aria-label={t("support.filterStatus")}
              >
                <option value="">{t("support.allStatuses")}</option>
                {STATUS_OPTIONS.map((s) => (
                  <option key={s} value={s}>
                    {t(`support.statuses.${s}`)}
                  </option>
                ))}
              </select>
              <Button
                size="sm"
                onClick={() => qc.invalidateQueries({ queryKey: ticketsQueryKey })}
              >
                {t("support.search")}
              </Button>
              <Button
                variant="secondary"
                size="sm"
                loading={ticketsQuery.isFetching}
                onClick={() => qc.invalidateQueries({ queryKey: ticketsQueryKey })}
                aria-label={t("common.refresh")}
              >
                ↻
              </Button>
            </div>

            {(mutationError ?? ticketsQuery.error) ? (
              <Alert variant="error">
                {mutationError ?? (ticketsQuery.error as Error)?.message}
              </Alert>
            ) : null}

            {ticketsQuery.isFetching ? (
              <div className="grid gap-2 lg:grid-cols-[0.9fr_1.1fr]">
                <div className="max-h-96 space-y-2 rounded-lg border border-[var(--color-border)] p-2">
                  {Array.from({ length: 4 }).map((_, i) => (
                    <div key={i} className="h-12 animate-pulse rounded-md bg-[var(--color-surface-muted)]" />
                  ))}
                </div>
                <div className="h-48 animate-pulse rounded-lg bg-[var(--color-surface-muted)]" />
              </div>
            ) : (
              <div className="grid gap-2 lg:grid-cols-[0.9fr_1.1fr]">
                <div className="max-h-96 space-y-1 overflow-auto rounded-lg border border-[var(--color-border)] p-1">
                  {items.map((it) => (
                    <button
                      key={it.id}
                      type="button"
                      onClick={() => setSelectedId(it.id)}
                      className={`w-full rounded-md px-2 py-2 text-left ${selectedId === it.id ? "bg-[var(--color-primary-soft)]" : "hover:bg-[var(--color-surface-muted)]"}`}
                    >
                      <p className="text-sm font-semibold">{it.ticketNumber}</p>
                      <p className="truncate text-xs text-[var(--color-muted)]">{it.subject}</p>
                      <div className="mt-1 flex gap-1">
                        <Badge>{it.status}</Badge>
                        <Badge variant={it.priority === "Urgent" ? "danger" : "default"}>{it.priority}</Badge>
                      </div>
                    </button>
                  ))}
                </div>

                <div className="space-y-2">
                  {selected ? (
                    <>
                      <div className="flex flex-wrap items-start justify-between gap-2">
                        <div>
                          <p className="text-sm font-semibold">{selected.subject}</p>
                          <p className="text-xs text-[var(--color-muted)]">
                            {selected.ticketNumber}
                            {selected.category
                              ? ` · ${t(`support.categories.${selected.category as SupportCategory}`, { defaultValue: selected.category })}`
                              : ""}{" "}
                            · {formatDateTimeOrDash(selected.updatedAtUtc)}
                          </p>
                        </div>
                        <div className="flex flex-wrap gap-2">
                          {canClose ? (
                            <Button size="sm" variant="secondary" loading={statusBusy} onClick={() => closeMutation.mutate()}>
                              {t("support.close")}
                            </Button>
                          ) : null}
                          {canReopen ? (
                            <Button size="sm" variant="secondary" loading={statusBusy} onClick={() => reopenMutation.mutate()}>
                              {t("support.reopen")}
                            </Button>
                          ) : null}
                        </div>
                      </div>
                      <div className="max-h-72 space-y-2 overflow-auto rounded-lg border border-[var(--color-border)] p-2">
                        {selected.messages
                          .filter((m) => !m.isInternalNote)
                          .map((m) => (
                            <div key={m.id} className="rounded border border-[var(--color-border)] px-2 py-1.5">
                              <p className="text-xs text-[var(--color-muted)]">{formatDateTimeOrDash(m.createdAtUtc)}</p>
                              <p className="text-sm">{m.body}</p>
                              {m.attachments.length > 0 ? (
                                <ul className="mt-1 text-xs text-[var(--color-primary)]">
                                  {m.attachments.map((a) => (
                                    <li key={a.id}>{a.fileName}</li>
                                  ))}
                                </ul>
                              ) : null}
                            </div>
                          ))}
                      </div>
                      {canReply ? (
                        <>
                          <textarea
                            className="min-h-20 w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm"
                            placeholder={t("support.replyPlaceholder")}
                            value={reply}
                            onChange={(e) => setReply(e.target.value)}
                          />
                          <label className="block text-sm">
                            <span className="text-[var(--color-muted)]">{t("support.attachments")}</span>
                            <input
                              type="file"
                              multiple
                              className="mt-1 block w-full text-sm"
                              onChange={(e) => setReplyFiles(Array.from(e.target.files ?? []))}
                            />
                          </label>
                          <Button loading={replyMutation.isPending} onClick={() => replyMutation.mutate()}>
                            {t("support.reply")}
                          </Button>
                        </>
                      ) : (
                        <p className="text-sm text-[var(--color-muted)]">{t("support.closedHint")}</p>
                      )}
                    </>
                  ) : (
                    <p className="text-sm text-[var(--color-muted)]">{t("support.selectTicket")}</p>
                  )}
                </div>
              </div>
            )}
          </CardBody>
        </Card>
      </div>
    </PageContent>
  );
}
