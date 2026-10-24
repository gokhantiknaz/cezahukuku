import { getGetAttachmentFileUrl } from "@/api/generated/personal/personal";

/** Ekin dosyası: PDF tarayıcıda açılır, ses oynatılır; `download` ile özgün adıyla indirilir. */
export const attachmentFileUrl = (id: string, download = false) =>
  getGetAttachmentFileUrl(id, download ? { download: true } : undefined);

export function formatBytes(bytes: number) {
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1).replace(".", ",")} MB`;
}

/** 75 → "1:15" */
export function formatSeconds(seconds: number) {
  const whole = Math.max(0, Math.floor(seconds));
  return `${Math.floor(whole / 60)}:${String(whole % 60).padStart(2, "0")}`;
}

export const formatDateTime = (iso: string) =>
  new Date(iso).toLocaleString("tr-TR", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
