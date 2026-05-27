import { apiRequest } from "@/lib/api/http";

export type InitiateMediaUploadResult = {
  mediaAssetId: string;
  uploadUrl: string;
  storageKey: string;
  uploadUrlExpiresAtUtc: string;
};

export type MediaVariantDto = {
  id: string;
  kind: string | number;
  url: string;
  contentType: string;
  byteSize: number;
  width?: number | null;
  height?: number | null;
  duration?: string | null;
};

export type MediaAssetDto = {
  id: string;
  ownerUserId: string;
  kind: string | number;
  status: string | number;
  contentType: string;
  byteSize: number;
  createdAtUtc: string;
  uploadedAtUtc?: string | null;
  processedAtUtc?: string | null;
  variants: MediaVariantDto[];
  errorMessage?: string | null;
  rowVersion: number;
};

export const mediaApi = {
  async initiate(file: File): Promise<InitiateMediaUploadResult> {
    const ext = file.name.includes(".") ? file.name.split(".").pop() : null;
    const res = await apiRequest<InitiateMediaUploadResult>({
      method: "POST",
      path: "/api/v1/media/uploads",
      auth: true,
      body: {
        contentType: file.type || "application/octet-stream",
        byteSize: file.size,
        fileExtension: ext,
      },
    });
    if (!res.success || !res.data) throw new Error(res.error?.message ?? "Initiate upload failed");
    return res.data;
  },

  async putToPresignedUrl(uploadUrl: string, file: Blob, contentType: string) {
    const res = await fetch(uploadUrl, {
      method: "PUT",
      headers: {
        "Content-Type": contentType,
      },
      body: file,
    });
    return res.ok;
  },

  async complete(mediaAssetId: string) {
    const res = await apiRequest<MediaAssetDto>({
      method: "POST",
      path: `/api/v1/media/${mediaAssetId}/complete`,
      auth: true,
      idempotent: true,
    });
    if (!res.success || !res.data) throw new Error(res.error?.message ?? "Complete upload failed");
    return res.data;
  },

  async get(mediaAssetId: string) {
    const res = await apiRequest<MediaAssetDto>({
      path: `/api/v1/media/${mediaAssetId}`,
      auth: true,
    });
    if (!res.success || !res.data) throw new Error(res.error?.message ?? "Get asset failed");
    return res.data;
  },

  async delete(mediaAssetId: string, rowVersion: number) {
    const res = await apiRequest<unknown>({
      method: "DELETE",
      path: `/api/v1/media/${mediaAssetId}`,
      auth: true,
      query: { rowVersion },
      idempotent: false,
    });
    if (!res.success) throw new Error(res.error?.message ?? "Delete failed");
    return true;
  },

  bestRenderableUrl(asset: MediaAssetDto) {
    // Prefer an image variant when present; otherwise fall back to first URL.
    const img = asset.variants.find((v) => v.contentType.startsWith("image/"));
    return img?.url ?? asset.variants[0]?.url ?? null;
  },
};

