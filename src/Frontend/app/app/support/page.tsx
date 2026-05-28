"use client";

import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { PageContent, PageHeader } from "@/components/ui/page-header";
import { formatDateTimeOrDash } from "@/lib/shared";
import { supportApi, type SupportTicketDetailDto, type SupportTicketPriority, type SupportTicketStatus } from "@/lib/features/support/support-api";

const STATUS_OPTIONS: SupportTicketStatus[] = ["Open", "PendingSupport", "PendingUser", "Resolved", "Closed"];
const PRIORITY_OPTIONS: SupportTicketPriority[] = ["Low", "Normal", "High", "Urgent"];

export default function SupportPage() {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(true);
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

  const selected = useMemo(() => items.find((x) => x.id === selectedId) ?? null, [items, selectedId]);

  async function loadList() {
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
    if (!selectedId && safe.length > 0) setSelectedId(safe[0].id);
    setLoading(false);
  }

  useEffect(() => {
    void loadList();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

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
            <div className="grid grid-cols-2 gap-2">
              <Input placeholder={t("support.category")} value={category} onChange={(e) => setCategory(e.target.value)} />
              <select
                className="h-10 rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 text-sm"
                value={priority}
                onChange={(e) => setPriority(e.target.value as SupportTicketPriority)}
              >
                {PRIORITY_OPTIONS.map((p) => <option key={p} value={p}>{p}</option>)}
              </select>
            </div>
            <Button
              onClick={async () => {
                if (!subject.trim() || !description.trim()) return;
                await supportApi.create({
                  subject: subject.trim(),
                  description: description.trim(),
                  category: category.trim() || undefined,
                  priority,
                });
                setSubject("");
                setDescription("");
                setCategory("");
                setPriority("Normal");
                await loadList();
              }}
            >
              {t("support.create")}
            </Button>
          </CardBody>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t("support.myTickets")}</CardTitle>
          </CardHeader>
          <CardBody className="space-y-3">
            <div className="flex gap-2">
              <Input
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
                {STATUS_OPTIONS.map((s) => <option key={s} value={s}>{s}</option>)}
              </select>
              <Button variant="secondary" onClick={() => void loadList()}>{t("common.submit")}</Button>
            </div>

            {error ? <Alert variant="error">{error}</Alert> : null}
            {loading ? <p className="text-sm text-[var(--color-muted)]">{t("common.loading")}</p> : null}

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
                    <p className="text-sm font-semibold">{selected.subject}</p>
                    <p className="text-xs text-[var(--color-muted)]">{selected.ticketNumber} · {formatDateTimeOrDash(selected.updatedAtUtc)}</p>
                    <div className="max-h-72 space-y-2 overflow-auto rounded-lg border border-[var(--color-border)] p-2">
                      {selected.messages.filter((m) => !m.isInternalNote).map((m) => (
                        <div key={m.id} className="rounded border border-[var(--color-border)] px-2 py-1.5">
                          <p className="text-xs text-[var(--color-muted)]">{formatDateTimeOrDash(m.createdAtUtc)}</p>
                          <p className="text-sm">{m.body}</p>
                        </div>
                      ))}
                    </div>
                    <textarea
                      className="min-h-20 w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm"
                      placeholder={t("support.replyPlaceholder")}
                      value={reply}
                      onChange={(e) => setReply(e.target.value)}
                    />
                    <Button
                      onClick={async () => {
                        if (!selectedId || !reply.trim()) return;
                        await supportApi.addMessage(selectedId, reply.trim());
                        setReply("");
                        await loadList();
                      }}
                    >
                      {t("support.reply")}
                    </Button>
                  </>
                ) : (
                  <p className="text-sm text-[var(--color-muted)]">{t("support.selectTicket")}</p>
                )}
              </div>
            </div>
          </CardBody>
        </Card>
      </div>
    </PageContent>
  );
}
