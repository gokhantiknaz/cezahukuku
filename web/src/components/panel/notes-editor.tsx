"use client";

import { useState } from "react";
import { Lock, Pencil, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import {
  useCreateArticleNote,
  useCreateLawNote,
  useDeleteEditorialNote,
  useUpdateEditorialNote,
} from "@/api/generated/admin-laws/admin-laws";
import type { EditorialNoteDto, EditorialNoteKind, Visibility } from "@/api/generated/model";
import { RichTextEditor } from "@/components/rich-text/rich-text-editor";
import { RichTextView } from "@/components/rich-text/rich-text-view";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ConfirmDelete } from "./confirm-delete";
import { emptyToNull, Field, fieldMessages, FormMessages, OptionSelect, otherMessages, visibilityLabels } from "./form";
import { useLibraryRefresh } from "./use-library-refresh";

export const noteKindLabels: Record<EditorialNoteKind, string> = {
  gerekce: "Gerekçe",
  aciklama: "Açıklama",
  dersNotu: "Ders notu",
  genelGerekce: "Genel gerekçe",
};

type KindOptions = Partial<Record<EditorialNoteKind, string>>;

// Sunucudaki kurala uygun: genel gerekçe yalnız kanuna, gerekçe ve açıklama yalnız maddeye eklenir.
const articleKinds: KindOptions = { gerekce: "Gerekçe", aciklama: "Açıklama", dersNotu: "Ders notu" };
const lawKinds: KindOptions = { genelGerekce: "Genel gerekçe", dersNotu: "Ders notu" };

export type NoteTarget = { level: "article"; id: string } | { level: "law"; id: string };

/**
 * Bir maddenin (gerekçe, açıklama, ders notu) ya da kanunun (genel gerekçe, ders notu) editoryal notları:
 * liste, ekleme, düzenleme, silme. Notlar okuma ekranında ilgili sekmede görünür.
 */
export function NotesEditor({ target, notes }: { target: NoteTarget; notes: EditorialNoteDto[] }) {
  const [editing, setEditing] = useState<string | "new" | null>(null);
  const kinds = target.level === "article" ? articleKinds : lawKinds;

  return (
    <section className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-lg">{target.level === "article" ? "Gerekçe, açıklama ve ders notu" : "Kanun notları"}</h3>
        {editing !== "new" && (
          <Button variant="outline" size="sm" onClick={() => setEditing("new")}>
            <Plus /> Not ekle
          </Button>
        )}
      </div>

      {editing === "new" && (
        <NoteForm target={target} kinds={kinds} onDone={() => setEditing(null)} />
      )}

      {notes.length === 0 && editing !== "new" && (
        <p className="text-sm text-muted-foreground">Henüz not yok.</p>
      )}

      <ul className="space-y-3">
        {notes.map((note) =>
          editing === note.id ? (
            <li key={note.id}>
              <NoteForm target={target} kinds={kinds} note={note} onDone={() => setEditing(null)} />
            </li>
          ) : (
            <li key={note.id} className="rounded-md border p-3">
              <div className="mb-2 flex flex-wrap items-center gap-2">
                <Badge variant="secondary">{noteKindLabels[note.kind]}</Badge>
                {note.title && <span className="font-medium">{note.title}</span>}
                {note.effectiveVisibility === "private" && (
                  <Badge variant="outline">
                    <Lock /> Özel
                  </Badge>
                )}
                <span className="ml-auto flex gap-1">
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    aria-label="Notu düzenle"
                    title="Notu düzenle"
                    onClick={() => setEditing(note.id)}
                  >
                    <Pencil />
                  </Button>
                  <DeleteNote note={note} />
                </span>
              </div>
              <RichTextView html={note.html} className="max-h-48 overflow-hidden text-base" />
            </li>
          ),
        )}
      </ul>
    </section>
  );
}

function DeleteNote({ note }: { note: EditorialNoteDto }) {
  const refresh = useLibraryRefresh();
  const remove = useDeleteEditorialNote();
  return (
    <ConfirmDelete
      title="Not silinsin mi?"
      description="Not okuyuculardan kaldırılır. Kayıt veritabanında saklanır."
      onConfirm={async () => {
        await remove.mutateAsync({ id: note.id });
        await refresh();
        toast.success("Not silindi.");
      }}
    >
      <Button variant="ghost" size="icon-sm" aria-label="Notu sil" title="Notu sil">
        <Trash2 />
      </Button>
    </ConfirmDelete>
  );
}

function NoteForm({
  target,
  kinds,
  note,
  onDone,
}: {
  target: NoteTarget;
  kinds: KindOptions;
  note?: EditorialNoteDto;
  onDone: () => void;
}) {
  const refresh = useLibraryRefresh();
  const firstKind = Object.keys(kinds)[0] as EditorialNoteKind;
  const [form, setForm] = useState({
    kind: note?.kind ?? firstKind,
    title: note?.title ?? "",
    visibility: note?.visibility ?? ("public" as Visibility),
    html: note?.html ?? "",
  });
  const createForArticle = useCreateArticleNote();
  const createForLaw = useCreateLawNote();
  const update = useUpdateEditorialNote();
  const error = createForArticle.error ?? createForLaw.error ?? update.error;
  const pending = createForArticle.isPending || createForLaw.isPending || update.isPending;

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const data = { kind: form.kind, title: emptyToNull(form.title), html: form.html, visibility: form.visibility, sortOrder: null };
    if (note) await update.mutateAsync({ id: note.id, data });
    else if (target.level === "article") await createForArticle.mutateAsync({ articleId: target.id, data });
    else await createForLaw.mutateAsync({ lawId: target.id, data });
    await refresh();
    toast.success(note ? "Not güncellendi." : "Not eklendi.");
    onDone();
  }

  return (
    <form onSubmit={(e) => submit(e).catch(() => {})} className="space-y-3 rounded-md border bg-muted/30 p-3">
      <div className="grid gap-3 sm:grid-cols-[12rem_1fr_14rem]">
        <Field label="Tür" htmlFor="note-kind" error={error} field="kind">
          <OptionSelect
            id="note-kind"
            value={form.kind}
            options={kinds as Record<EditorialNoteKind, string>}
            onChange={(kind) => setForm((f) => ({ ...f, kind }))}
          />
        </Field>
        <Field label="Başlık (isteğe bağlı)" htmlFor="note-title" error={error} field="title">
          <Input
            id="note-title"
            value={form.title}
            onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
            placeholder="5377 sayılı Kanunla yapılan değişikliğin gerekçesi"
          />
        </Field>
        <Field label="Görünürlük" htmlFor="note-visibility">
          <OptionSelect
            id="note-visibility"
            value={form.visibility}
            options={visibilityLabels}
            onChange={(visibility) => setForm((f) => ({ ...f, visibility }))}
          />
        </Field>
      </div>
      <div>
        <RichTextEditor value={form.html} onChange={(html) => setForm((f) => ({ ...f, html }))} label="Not metni" />
        {fieldMessages(error, "html").map((m) => (
          <p key={m} className="mt-1 text-xs text-destructive">
            {m}
          </p>
        ))}
      </div>
      <FormMessages messages={otherMessages(error, ["kind", "title", "html"])} />
      <div className="flex gap-2">
        <Button type="submit" disabled={pending}>
          {note ? "Kaydet" : "Notu ekle"}
        </Button>
        <Button type="button" variant="ghost" onClick={onDone}>
          Vazgeç
        </Button>
      </div>
    </form>
  );
}
