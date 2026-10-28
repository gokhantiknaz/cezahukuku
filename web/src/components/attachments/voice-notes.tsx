"use client";

import { useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Mic, Pencil, RotateCw, Square, Trash2 } from "lucide-react";
import { toast } from "sonner";
import {
  getGetAttachmentQueryKey,
  getListAttachmentsQueryKey,
  useDeleteAttachment,
  useGetAttachment,
  useListAttachments,
  useReprocessAttachment,
  useUpdateAttachment,
  useUploadAttachment,
} from "@/api/generated/personal/personal";
import type { AttachmentDto } from "@/api/generated/model";
import { Button } from "@/components/ui/button";
import { NoteForm } from "@/components/workspace/note-form";
import { attachmentFileUrl, formatDateTime, formatSeconds } from "@/lib/attachments";

/** Bir kayıt en fazla bu kadar sürer (sunucu sınırı 25 MB; opus ile 10 dakika ≈ 5 MB). */
const MAX_SECONDS = 10 * 60;

// Tarayıcının kaydedebildiği ilk tür (Chrome/Edge/Firefox webm-opus, Safari mp4).
const MIME_TYPES = ["audio/webm;codecs=opus", "audio/webm", "audio/mp4", "audio/ogg;codecs=opus"];

const extensionOf = (mimeType: string) => (mimeType.includes("mp4") ? "m4a" : mimeType.includes("ogg") ? "ogg" : "webm");

/** Bas-konuş kayıt: başlat → süre sayar → durdur → kayıt yüklenir. */
function useVoiceRecorder(onRecorded: (file: File) => void) {
  const [seconds, setSeconds] = useState<number | null>(null);
  const recorderRef = useRef<MediaRecorder | null>(null);
  const timerRef = useRef<number | undefined>(undefined);

  useEffect(() => () => {
    window.clearInterval(timerRef.current);
    recorderRef.current?.stream.getTracks().forEach((t) => t.stop());
  }, []);

  async function start() {
    if (!navigator.mediaDevices?.getUserMedia || typeof MediaRecorder === "undefined") {
      toast.error("Bu tarayıcı ses kaydını desteklemiyor (güvenli bağlantı gerekir).");
      return;
    }
    let stream: MediaStream;
    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    } catch {
      toast.error("Mikrofon izni verilmedi.");
      return;
    }
    const mimeType = MIME_TYPES.find((type) => MediaRecorder.isTypeSupported(type)) ?? "";
    const recorder = new MediaRecorder(stream, mimeType ? { mimeType } : undefined);
    const chunks: Blob[] = [];
    recorder.ondataavailable = (event) => event.data.size > 0 && chunks.push(event.data);
    recorder.onstop = () => {
      stream.getTracks().forEach((t) => t.stop());
      window.clearInterval(timerRef.current);
      setSeconds(null);
      const type = recorder.mimeType || mimeType || "audio/webm";
      const blob = new Blob(chunks, { type });
      if (blob.size > 0) onRecorded(new File([blob], `sesli-not.${extensionOf(type)}`, { type }));
    };
    recorderRef.current = recorder;
    recorder.start(1000);
    setSeconds(0);
    const startedAt = Date.now();
    timerRef.current = window.setInterval(() => {
      const elapsed = Math.floor((Date.now() - startedAt) / 1000);
      setSeconds(elapsed);
      if (elapsed >= MAX_SECONDS) recorder.stop();
    }, 250);
  }

  return { recording: seconds !== null, seconds: seconds ?? 0, start, stop: () => recorderRef.current?.stop() };
}

/**
 * Maddedeki sesli notlarım (sağ panel): kaydet, dinle, yazıya dökülmüş metni oku ya da kendin yaz. Yazıya dökme
 * (Faz 3) sunucuda ayarlıysa metin kendiliğinden gelir (işlenirken liste kendini yeniler).
 */
export function VoiceNotes({ articleId }: { articleId: string }) {
  const queryClient = useQueryClient();
  const listParams = { articleId, kind: "audio" };
  const { data } = useListAttachments(listParams, {
    query: { refetchInterval: (query) => (query.state.data?.data.some((a) => a.status === "pending") ? 2000 : false) },
  });
  const notes = data?.status === 200 ? data.data : [];
  const upload = useUploadAttachment();
  const refresh = () => queryClient.invalidateQueries({ queryKey: getListAttachmentsQueryKey(listParams) });

  const recorder = useVoiceRecorder(async (file) => {
    try {
      await upload.mutateAsync({ data: { file, articleId } });
      await refresh();
      toast.success("Sesli not kaydedildi.");
    } catch {
      toast.error("Sesli not yüklenemedi.");
    }
  });

  return (
    <section className="space-y-2">
      <h3 className="font-medium">
        Sesli notlarım{notes.length > 0 && <span className="text-muted-foreground"> ({notes.length})</span>}
      </h3>
      {recorder.recording ? (
        <Button variant="destructive" className="w-full" onClick={recorder.stop}>
          <Square className="fill-current" /> Kaydı bitir · {formatSeconds(recorder.seconds)}
          <span className="ml-1 size-2 animate-pulse rounded-full bg-primary-foreground" aria-hidden />
        </Button>
      ) : (
        <Button variant="outline" className="w-full" onClick={recorder.start} disabled={upload.isPending}>
          <Mic /> {upload.isPending ? "Yükleniyor…" : "Sesli not al"}
        </Button>
      )}
      <ul className="space-y-2">
        {notes.map((note) => (
          <VoiceNoteItem key={note.id} note={note} onChanged={refresh} />
        ))}
      </ul>
    </section>
  );
}

function VoiceNoteItem({ note, onChanged }: { note: AttachmentDto; onChanged: () => Promise<unknown> }) {
  const queryClient = useQueryClient();
  const audioRef = useRef<HTMLAudioElement>(null);
  const [editing, setEditing] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const { data } = useGetAttachment(note.id, { query: { enabled: note.status !== "pending" } });
  const detail = data?.status === 200 ? data.data : null;
  const update = useUpdateAttachment();
  const remove = useDeleteAttachment();
  const reprocess = useReprocessAttachment();
  const transcript = detail?.segments.map((s) => s.text).join("\n") ?? "";
  const refreshDetail = () => queryClient.invalidateQueries({ queryKey: getGetAttachmentQueryKey(note.id) });

  async function saveTranscript(text: string) {
    try {
      await update.mutateAsync({ id: note.id, data: { title: null, transcript: text } });
      await Promise.all([onChanged(), refreshDetail()]);
      setEditing(false);
    } catch {
      toast.error("Metin kaydedilemedi.");
    }
  }

  return (
    <li className="space-y-1.5 rounded-md border bg-card p-2.5">
      <div className="flex items-center justify-between gap-2 text-xs text-muted-foreground">
        <span>{formatDateTime(note.createdAt)}</span>
        {confirmDelete ? (
          <span className="flex items-center gap-1">
            Silinsin mi?
            <Button
              size="xs"
              variant="destructive"
              disabled={remove.isPending}
              onClick={async () => {
                await remove.mutateAsync({ id: note.id });
                await onChanged();
              }}
            >
              Sil
            </Button>
            <Button size="xs" variant="ghost" onClick={() => setConfirmDelete(false)}>
              Vazgeç
            </Button>
          </span>
        ) : (
          <Button size="icon-xs" variant="ghost" aria-label="Sesli notu sil" title="Sesli notu sil" onClick={() => setConfirmDelete(true)}>
            <Trash2 />
          </Button>
        )}
      </div>
      {/* Ses parça parça istenir (Range); ileri sarılabilir. */}
      <audio ref={audioRef} controls preload="metadata" src={attachmentFileUrl(note.id)} className="h-9 w-full" />

      {note.status === "pending" ? (
        <p className="text-xs text-muted-foreground">İşleniyor…</p>
      ) : editing ? (
        <NoteForm initialText={transcript} busy={update.isPending} rows={4} onSave={saveTranscript} onCancel={() => setEditing(false)} />
      ) : detail && detail.segments.length > 0 ? (
        <div className="group flex gap-2">
          <p className="min-w-0 flex-1 font-reading text-[15px]">
            {detail.segments.map((s) => (
              // Bölüme tıklayınca ses o saniyeye gider.
              <button
                key={s.id}
                type="button"
                title={s.startSeconds != null ? `${formatSeconds(s.startSeconds)} — buradan dinle` : undefined}
                onClick={() => {
                  if (audioRef.current && s.startSeconds != null) {
                    audioRef.current.currentTime = s.startSeconds;
                    void audioRef.current.play();
                  }
                }}
                className="text-left whitespace-pre-wrap hover:bg-accent"
              >
                {s.text}{" "}
              </button>
            ))}
          </p>
          <Button size="icon-sm" variant="ghost" className="shrink-0 text-muted-foreground" aria-label="Metni düzenle" title="Metni düzenle" onClick={() => setEditing(true)}>
            <Pencil />
          </Button>
        </div>
      ) : (
        <div className="flex flex-wrap items-center gap-1 text-xs text-muted-foreground">
          {/* Yazıya dökme kapalıyken (Faz 3) bundan hiç söz edilmez; kullanıcı metni kendisi yazabilir. */}
          {note.status === "failed" ? note.error : detail?.transcriptionEnabled && "Konuşma algılanmadı."}
          <Button size="xs" variant="ghost" onClick={() => setEditing(true)}>
            <Pencil /> Metni yaz
          </Button>
          {detail?.transcriptionEnabled && (
            <Button
              size="xs"
              variant="ghost"
              disabled={reprocess.isPending}
              onClick={async () => {
                await reprocess.mutateAsync({ id: note.id });
                await onChanged();
              }}
            >
              <RotateCw /> Yeniden dök
            </Button>
          )}
        </div>
      )}
    </li>
  );
}
