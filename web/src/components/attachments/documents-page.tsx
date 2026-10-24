"use client";

import { useRef, useState } from "react";
import Link from "next/link";
import { useQueryClient } from "@tanstack/react-query";
import { FileAudio, FileText, LogIn, Trash2, Upload } from "lucide-react";
import { toast } from "sonner";
import { ApiError } from "@/api/fetcher";
import {
  getListAttachmentsQueryKey,
  useDeleteAttachment,
  useListAttachments,
  useUploadAttachment,
} from "@/api/generated/personal/personal";
import type { AttachmentDto } from "@/api/generated/model";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useHydrated } from "@/hooks/use-hydrated";
import { formatBytes, formatDateTime } from "@/lib/attachments";
import { useCurrentUser } from "@/lib/auth";
import { canAnnotate } from "@/lib/highlights";

/** Belgelerim: kullanıcının PDF'leri ve sesli notları. PDF yüklenir, metni çıkarılıp aranabilir olur. */
export function DocumentsPage() {
  const hydrated = useHydrated();
  const { data: user, isPending } = useCurrentUser();
  const enabled = hydrated && canAnnotate(user);
  const queryClient = useQueryClient();
  const { data } = useListAttachments(undefined, {
    query: {
      enabled,
      refetchInterval: (query) => (query.state.data?.data.some((a) => a.status === "pending") ? 2000 : false),
    },
  });
  const attachments = data?.status === 200 ? data.data : [];
  const refresh = () => queryClient.invalidateQueries({ queryKey: getListAttachmentsQueryKey().slice(0, 1) });

  if (!hydrated || isPending) return null;
  if (!user) {
    return (
      <p className="mt-6">
        <Button asChild>
          <Link href="/giris">
            <LogIn /> Belgeleriniz için giriş yapın
          </Link>
        </Button>
      </p>
    );
  }
  if (!enabled) return <p className="mt-6 text-muted-foreground">Hesabınız yalnız okuma yetkisine sahip; belge yükleyemezsiniz.</p>;

  const pdfs = attachments.filter((a) => a.kind === "pdf");
  const voices = attachments.filter((a) => a.kind === "audio");
  return (
    <div className="mt-4 space-y-6">
      <UploadForm onUploaded={refresh} />
      <section>
        <h2 className="text-xl">PDF belgelerim {pdfs.length > 0 && <span className="text-muted-foreground">({pdfs.length})</span>}</h2>
        {pdfs.length === 0 ? (
          <p className="mt-2 text-muted-foreground">Henüz belge yüklemediniz. Karar, makale ya da ders notu PDF&apos;lerini yükleyin; metinleri aramada çıkar.</p>
        ) : (
          <ul className="mt-2 divide-y rounded-lg border bg-card">
            {pdfs.map((a) => (
              <AttachmentRow key={a.id} attachment={a} onDeleted={refresh} />
            ))}
          </ul>
        )}
      </section>
      {voices.length > 0 && (
        <section>
          <h2 className="text-xl">Sesli notlarım <span className="text-muted-foreground">({voices.length})</span></h2>
          <ul className="mt-2 divide-y rounded-lg border bg-card">
            {voices.map((a) => (
              <AttachmentRow key={a.id} attachment={a} onDeleted={refresh} />
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}

function UploadForm({ onUploaded }: { onUploaded: () => Promise<unknown> }) {
  const fileRef = useRef<HTMLInputElement>(null);
  const [title, setTitle] = useState("");
  const upload = useUploadAttachment();

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const file = fileRef.current?.files?.[0];
    if (!file) return;
    try {
      await upload.mutateAsync({ data: { file, title: title.trim() || undefined } });
      await onUploaded();
      toast.success("Belge yüklendi; metni çıkarılıyor.");
      setTitle("");
      if (fileRef.current) fileRef.current.value = "";
    } catch (error) {
      toast.error(error instanceof ApiError ? error.messages.join(" ") : "Belge yüklenemedi.");
    }
  }

  return (
    <form onSubmit={submit} className="flex flex-wrap items-end gap-2 rounded-lg border bg-card p-4">
      <label className="min-w-56 flex-1 space-y-1">
        <span className="text-sm font-medium">PDF dosyası (en fazla 50 MB)</span>
        <Input ref={fileRef} type="file" name="file" accept="application/pdf,.pdf" required />
      </label>
      <label className="min-w-56 flex-1 space-y-1">
        <span className="text-sm font-medium">Başlık (isteğe bağlı)</span>
        <Input value={title} onChange={(e) => setTitle(e.target.value)} maxLength={300} placeholder="Yargıtay CGK 2020/123" />
      </label>
      <Button type="submit" disabled={upload.isPending}>
        <Upload /> {upload.isPending ? "Yükleniyor…" : "Yükle"}
      </Button>
    </form>
  );
}

function AttachmentRow({ attachment: a, onDeleted }: { attachment: AttachmentDto; onDeleted: () => Promise<unknown> }) {
  const remove = useDeleteAttachment();
  const [confirm, setConfirm] = useState(false);
  const Icon = a.kind === "pdf" ? FileText : FileAudio;
  return (
    <li className="flex flex-wrap items-center gap-3 px-4 py-3">
      <Icon className="size-5 shrink-0 text-primary" aria-hidden />
      <div className="min-w-0 flex-1">
        <Link href={`/belgelerim/${a.id}`} className="font-medium text-primary hover:underline">
          {a.title}
        </Link>
        <p className="text-sm text-muted-foreground">
          {[
            formatDateTime(a.createdAt),
            formatBytes(a.sizeBytes),
            a.pageCount != null ? `${a.pageCount} sayfa` : null,
            a.article ? `${a.article.lawLabel} Madde ${a.article.number}` : null,
          ]
            .filter(Boolean)
            .join(" · ")}
        </p>
      </div>
      <StatusBadge attachment={a} />
      {confirm ? (
        <span className="flex items-center gap-1 text-sm">
          Silinsin mi?
          <Button
            size="sm"
            variant="destructive"
            disabled={remove.isPending}
            onClick={async () => {
              await remove.mutateAsync({ id: a.id });
              await onDeleted();
            }}
          >
            Sil
          </Button>
          <Button size="sm" variant="ghost" onClick={() => setConfirm(false)}>
            Vazgeç
          </Button>
        </span>
      ) : (
        <Button size="icon-sm" variant="ghost" aria-label="Sil" title="Sil" onClick={() => setConfirm(true)}>
          <Trash2 />
        </Button>
      )}
    </li>
  );
}

export function StatusBadge({ attachment: a }: { attachment: AttachmentDto }) {
  if (a.status === "pending") return <Badge variant="secondary">İşleniyor…</Badge>;
  if (a.status === "failed") return <Badge variant="destructive" title={a.error ?? undefined}>İşlenemedi</Badge>;
  if (a.kind === "pdf" && a.scannedPageCount)
    return (
      <Badge variant="outline" title="Bu sayfalarda metin katmanı yok (taranmış görüntü); aramada çıkmaz.">
        {a.scannedPageCount} taranmış sayfa
      </Badge>
    );
  return null;
}
