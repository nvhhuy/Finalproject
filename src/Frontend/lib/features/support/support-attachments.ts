import { mediaApi } from "@/lib/features/media/media-api";

export type SupportAttachmentInput = {
  storageKey: string;
  fileName: string;
  contentType: string;
  byteSize: number;
};

export async function uploadSupportAttachment(file: File): Promise<SupportAttachmentInput> {
  const init = await mediaApi.initiate(file);
  const uploaded = await mediaApi.putToPresignedUrl(init.uploadUrl, file, file.type || "application/octet-stream");
  if (!uploaded) throw new Error("Upload failed");
  await mediaApi.complete(init.mediaAssetId);
  return {
    storageKey: init.storageKey,
    fileName: file.name,
    contentType: file.type || "application/octet-stream",
    byteSize: file.size,
  };
}
