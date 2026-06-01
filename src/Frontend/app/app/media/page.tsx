"use client";

import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageContent, PageHeader } from "@/components/ui/page-header";
import { mediaApi, type MediaAssetDto } from "@/lib/features/media/media-api";
import { formatBytes, formatDateTimeOrDash } from "@/lib/shared";
import { cn } from "@/lib/ui/cn";

const MEDIA_ASSET_KEY = (id: string) => ["media-asset", id] as const;

const TERMINAL_STATUSES = new Set(["Ready", "Failed"]);

export default function MediaLabPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [file, setFile] = useState<File | null>(null);
  const [assetId, setAssetId] = useState<string | null>(null);

  const { data: asset } = useQuery<MediaAssetDto>({
    queryKey: assetId ? MEDIA_ASSET_KEY(assetId) : ["media-asset-none"],
    queryFn: () => mediaApi.get(assetId!),
    enabled: !!assetId,
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status && TERMINAL_STATUSES.has(String(status)) ? false : 2000;
    },
  });

  const uploadMutation = useMutation({
    mutationFn: async (uploadFile: File) => {
      const init = await mediaApi.initiate(uploadFile);
      const ok = await mediaApi.putToPresignedUrl(init.uploadUrl, uploadFile, uploadFile.type || "application/octet-stream");
      if (!ok) throw new Error(t("errors.requestFailed"));
      return mediaApi.complete(init.mediaAssetId);
    },
    onSuccess: (completed) => {
      setAssetId(completed.id);
      queryClient.setQueryData(MEDIA_ASSET_KEY(completed.id), completed);
    },
  });

  const deleteMutation = useMutation({
    mutationFn: ({ id, rowVersion }: { id: string; rowVersion: number }) =>
      mediaApi.delete(id, rowVersion),
    onSuccess: () => {
      setAssetId(null);
    },
  });

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
            <Button
              onClick={() => file && uploadMutation.mutate(file)}
              disabled={!file}
              loading={uploadMutation.isPending}
            >
              {t("media.runUpload")}
            </Button>
            {asset ? (
              <Button
                variant="danger"
                loading={deleteMutation.isPending}
                onClick={() => deleteMutation.mutate({ id: asset.id, rowVersion: asset.rowVersion })}
              >
                {t("media.delete")}
              </Button>
            ) : null}
          </div>
          {uploadMutation.error ? (
            <Alert variant="error">
              {uploadMutation.error instanceof Error ? uploadMutation.error.message : t("errors.requestFailed")}
            </Alert>
          ) : null}
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
