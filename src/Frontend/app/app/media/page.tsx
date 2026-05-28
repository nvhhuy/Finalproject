"use client";

import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageContent, PageHeader } from "@/components/ui/page-header";
import { mediaApi, type MediaAssetDto } from "@/lib/features/media/media-api";
import { formatBytes, formatDateTimeOrDash } from "@/lib/shared";
import { cn } from "@/lib/ui/cn";

export default function MediaLabPage() {
  const { t } = useTranslation();
  const [file, setFile] = useState<File | null>(null);
  const [asset, setAsset] = useState<MediaAssetDto | null>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function run() {
    if (!file) return;
    setError(null);
    setUploading(true);
    try {
      const init = await mediaApi.initiate(file);
      const ok = await mediaApi.putToPresignedUrl(init.uploadUrl, file, file.type || "application/octet-stream");
      if (!ok) throw new Error(t("errors.requestFailed"));
      const completed = await mediaApi.complete(init.mediaAssetId);
      setAsset(completed);
    } catch (e) {
      setError(e instanceof Error ? e.message : t("errors.requestFailed"));
    } finally {
      setUploading(false);
    }
  }

  useEffect(() => {
    if (!asset?.id) return;
    let cancelled = false;
    const interval = setInterval(async () => {
      try {
        const fresh = await mediaApi.get(asset.id);
        if (!cancelled) setAsset(fresh);
      } catch {
        /* ignore poll errors */
      }
    }, 2000);
    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, [asset?.id]);

  return (
    <PageContent>
      <PageHeader title={t("media.title")} description={t("media.flowHint")} />

      <Card>
        <CardHeader>
          <CardTitle>{t("media.flowTitle")}</CardTitle>
          <CardDescription>{t("media.flowHint")}</CardDescription>
        </CardHeader>
        <CardBody className="grid gap-4">
          <label
            className={cn(
              "inline-flex cursor-pointer items-center gap-3 rounded-lg border border-dashed border-[var(--color-border)]",
              "bg-[var(--color-surface-muted)] px-4 py-3 text-sm transition",
              "hover:border-[var(--color-primary)] hover:bg-[var(--color-primary-soft)]",
            )}
          >
            <span className="font-medium text-[var(--color-foreground)]">{file?.name ?? t("media.runUpload")}</span>
            <input type="file" className="sr-only" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
          </label>
          <div className="flex flex-wrap gap-2">
            <Button onClick={run} disabled={!file || uploading}>
              {uploading ? t("media.uploading") : t("media.runUpload")}
            </Button>
            {asset ? (
              <Button
                variant="danger"
                onClick={async () => {
                  await mediaApi.delete(asset.id, asset.rowVersion);
                  setAsset(null);
                }}
              >
                {t("media.delete")}
              </Button>
            ) : null}
          </div>
          {error ? <Alert variant="error">{error}</Alert> : null}
        </CardBody>
      </Card>

      {asset ? (
        <Card>
          <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-2">
            <CardTitle>{t("media.asset")}</CardTitle>
            <Badge variant="primary">{String(asset.status)}</Badge>
          </CardHeader>
          <CardBody className="grid gap-4">
            {asset.errorMessage ? <Alert variant="error">{asset.errorMessage}</Alert> : null}
            <p className="text-sm text-[var(--color-muted)]">
              {formatBytes(asset.byteSize)} · {formatDateTimeOrDash(asset.createdAtUtc)}
            </p>
            {mediaApi.bestRenderableUrl(asset) ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={mediaApi.bestRenderableUrl(asset)!}
                alt=""
                loading="lazy"
                decoding="async"
                className="max-h-80 w-auto rounded-xl border border-[var(--color-border)] object-contain bg-[var(--color-surface-muted)]"
              />
            ) : null}
            <pre className="overflow-auto rounded-xl border border-[var(--color-border)] bg-[var(--color-surface-muted)] p-4 text-xs text-[var(--color-foreground)]">
              {JSON.stringify(asset, null, 2)}
            </pre>
          </CardBody>
        </Card>
      ) : null}
    </PageContent>
  );
}
