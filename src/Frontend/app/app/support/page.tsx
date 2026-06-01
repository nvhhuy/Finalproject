"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { PageContent, PageHeader } from "@/components/ui/page-header";
import { SupportSkeleton } from "@/components/ui/skeleton";
import { formatDateTimeOrDash } from "@/lib/shared";
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
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [replying, setReplying] = useState(false);
  const [statusBusy, setStatusBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState<SupportTicketStatus | "">("");
  const [items, setItems] = useState<SupportTicketDetailDto[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [subject, setSubject] = useState("");
  const [description, setDescription] = useState("");
  const [category, setCategory] = useState("");
  const [priority, setPriority] = useState<SupportTicketPriority>("Normal");
  const [reply, setReply] = useState("");
  const [createFiles, setCreateFiles] = useState<File[]>([]);
  const [replyFiles, setReplyFiles] = useState<File[]>([]);

  const selected = useMemo(() => items.find((x) => x.id === selectedId) ?? null, [items, selectedId]);
  const canReply = selected && selected.status !== "Closed";
  const canClose = selected && selected.status !== "Closed";
  const canReopen = selected && selected.status === "Closed";

  const loadList = useCallback(async () => {
    setLoading(true);
    setError(null);
    const res = await supportApi.list(1, 50, search || undefined, status || undefined);
    if (!res.success || !res.data) {
      setError(res.error?.message ?? t("errors.requestFailed"));
      setLoading(false);
      return;
    }
    const details = await Promise.all(
      res.data.items.map(async (it) => {
        const d = await supportApi.detail(it.id);
        return d.success && d.data ? d.data : null;
      }),
    );
    const safe = details.filter(Boolean) as SupportTicketDetailDto[];
    setItems(safe);
    setSelectedId((prev) => {
      if (!prev && safe.length > 0) return safe[0].id;
      if (prev && !safe.some((x) => x.id === prev)) return safe[0]?.id ?? null;
      return prev;
    });
    setLoading(false);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search, status]);

  // Debounce search so rapid typing doesn't fire N requests
  const searchDebounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [searchDebounced, setSearchDebounced] = useState(search);
  useEffect(() => {
    if (searchDebounceRef.current) clearTimeout(searchDebounceRef.current);
    searchDebounceRef.current = setTimeout(() => setSearchDebounced(search), 350);
    return () => {
      if (searchDebounceRef.current) clearTimeout(searchDebounceRef.current);
    };
  }, [search]);

  // Auto-refetch whenever debounced search or status filter changes
  useEffect(() => {
    void loadList();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchDebounced, status]);

  async function handleCreate() {
    if (!subject.trim() || !description.trim()) return;
    setCreating(true);
    setError(null);
    try {
      const res = await supportApi.create({
        subject: subject.trim(),
        description: description.trim(),
        category: category.trim() || undefined,
        priority,
      });
      if (!res.success || !res.data) {
        setError(res.error?.message ?? t("errors.requestFailed"));
        return;
      }
      if (createFiles.length > 0) {
        const attachments = await Promise.all(createFiles.map((f) => uploadSupportAttachment(f)));
        await supportApi.addMessage(res.data, t("support.attachmentsMessage"), attachments);
      }
      setSubject("");
      setDescription("");
      setCategory("");
      setPriority("Normal");
      setCreateFiles([]);
      await loadList();
      setSelectedId(res.data);
    } catch (e) {
      setError(e instanceof Error ? e.message : t("errors.requestFailed"));
    } finally {
      setCreating(false);
    }
  }

  async function handleReply() {
    if (!selectedId || !reply.trim()) return;
    setReplying(true);
    setError(null);
    try {
      const attachments =
        replyFiles.length > 0 ? await Promise.all(replyFiles.map((f) => uploadSupportAttachment(f))) : undefined;
      const res = await supportApi.addMessage(selectedId, reply.trim(), attachments);
      if (!res.success) {
        setError(res.error?.message ?? t("errors.requestFailed"));
        return;
      }
      setReply("");
      setReplyFiles([]);
      await loadList();
    } catch (e) {
      setError(e instanceof Error ? e.message : t("errors.requestFailed"));
    } finally {
      setReplying(false);
    }
  }

  async function handleClose() {
    if (!selectedId) return;
    setStatusBusy(true);
    const res = await supportApi.close(selectedId);
    setStatusBusy(false);
    if (!res.success) setError(res.error?.message ?? t("errors.requestFailed"));
    else await loadList();
  }

  async function handleReopen() {
    if (!selectedId) return;
    setStatusBusy(true);
    const res = await supportApi.reopen(selectedId);
    setStatusBusy(false);
    if (!res.success) setError(res.error?.message ?? t("errors.requestFailed"));
    else await loadList();
  }

  if (loading && items.length === 0) {
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
              className="min-h-24 w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm"
              placeholder={t("support.descriptionInput")}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              <Input placeholder={t("support.category")} value={category} onChange={(e) => setCategory(e.target.value)} />
              <select
                className="h-10 rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 text-sm"
                value={priority}
                onChange={(e) => setPriority(e.target.value as SupportTicketPriority)}
              >
                {PRIORITY_OPTIONS.map((p) => (
                  <option key={p} value={p}>
                    {p}
                  </option>
                ))}
              </select>
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
            <Button loading={creating} onClick={() => void handleCreate()}>
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
                className="h-10 rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 text-sm"
                value={status}
                onChange={(e) => setStatus(e.target.value as SupportTicketStatus | "")}
              >
                <option value="">{t("support.allStatuses")}</option>
                {STATUS_OPTIONS.map((s) => (
                  <option key={s} value={s}>
                    {s}
                  </option>
                ))}
              </select>
              <Button variant="secondary" size="sm" loading={loading} onClick={() => void loadList()} aria-label={t("common.refresh")}>
                ↻
              </Button>
            </div>

            {error ? <Alert variant="error">{error}</Alert> : null}
            {loading ? (
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
                            {selected.ticketNumber} · {formatDateTimeOrDash(selected.updatedAtUtc)}
                          </p>
                        </div>
                        <div className="flex flex-wrap gap-2">
                          {canClose ? (
                            <Button size="sm" variant="secondary" loading={statusBusy} onClick={() => void handleClose()}>
                              {t("support.close")}
                            </Button>
                          ) : null}
                          {canReopen ? (
                            <Button size="sm" variant="secondary" loading={statusBusy} onClick={() => void handleReopen()}>
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
                          <Button loading={replying} onClick={() => void handleReply()}>
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
