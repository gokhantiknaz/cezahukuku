"use client";

import { useState, type ReactNode } from "react";
import Link from "next/link";
import { CircleAlert, Highlighter, LogIn, Mic, NotebookPen, Pencil, Plus, Trash2 } from "lucide-react";
import type { HighlightDto, PersonalNoteDto } from "@/api/generated/model";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { ScrollArea } from "@/components/ui/scroll-area";
import { useHydrated } from "@/hooks/use-hydrated";
import { useCurrentUser } from "@/lib/auth";
import { canAnnotate, highlightClassName, highlightColors } from "@/lib/highlights";
import { cn } from "@/lib/utils";
import { useCurrentArticle, type CurrentArticle } from "./current-article";
import { NoteForm } from "./note-form";
import { revealHighlight, usePersonalLayer } from "./use-personal-layer";

/**
 * Sağ panel: açık maddedeki kişisel vurgular ve notlar. Vurgular metindeki sırasıyla listelenir; tıklanınca
 * metinde o yere gidilir. Madde değiştiği için metinde bulunamayan vurgular sonda "metinde yok" diye durur.
 * Maddenin geneline (bir yer seçmeden) not da buradan yazılır.
 */
export function NotesPanel() {
  const hydrated = useHydrated();
  const { data: user, isPending } = useCurrentUser();
  const { article } = useCurrentArticle();

  let body;
  if (!hydrated || isPending) body = null;
  else if (!user)
    body = (
      <Intro>
        <Button variant="outline" className="w-full" asChild>
          <Link href="/giris">
            <LogIn /> Vurgu ve not için giriş yapın
          </Link>
        </Button>
      </Intro>
    );
  else if (!canAnnotate(user))
    body = (
      <Intro>
        <p className="text-sm text-muted-foreground">
          Hesabınız yalnız okuma yetkisine sahip; vurgu ve not ekleyemezsiniz.
        </p>
      </Intro>
    );
  else if (!article) body = <Intro />;
  else body = <ArticleNotes key={article.articleId} article={article} />;

  return (
    <div className="flex h-full flex-col">
      <div className="flex items-center gap-2 bg-primary px-3 py-2.5 text-lg text-primary-foreground">
        <NotebookPen className="size-5" aria-hidden />
        Notlarım
      </div>
      <ScrollArea className="min-h-0 flex-1">
        <div className="space-y-5 p-4">
          {body}
          <Button variant="outline" className="w-full" disabled title="Yakında">
            <Mic /> Sesli not al
          </Button>
        </div>
      </ScrollArea>
    </div>
  );
}

/** Madde açık değilken: renkler ve kısa kullanım bilgisi. */
function Intro({ children }: { children?: ReactNode }) {
  return (
    <>
      <section>
        <h3 className="flex items-center gap-2 font-medium">
          <Highlighter className="size-4" aria-hidden /> Vurgu renkleri
        </h3>
        <ul className="mt-2 flex flex-wrap gap-2">
          {highlightColors.map((color) => (
            <li key={color.key} className={`rounded px-2 py-0.5 text-sm ${color.className}`}>
              {color.label}
            </li>
          ))}
        </ul>
      </section>
      <p className="text-sm text-muted-foreground">
        Madde metninde bir yeri seçip vurgulayabilir, seçili yere not bağlayabilirsiniz. Vurgu ve notlarınızı
        yalnızca siz görürsünüz.
      </p>
      {children}
    </>
  );
}

/** Vurguları metindeki sırasıyla dizer (fıkra/bent sırası, sonra konum); metinde bulunamayanlar sonda. */
function sortByPosition(highlights: HighlightDto[], blockOrder: string[]) {
  const index = new Map(blockOrder.map((key, i) => [key, i]));
  const position = (h: HighlightDto) => {
    const first = h.segments[0];
    if (!first || h.isStale) return [Number.MAX_SAFE_INTEGER, 0];
    return [index.get(`${first.paragraphId}:${first.itemId ?? ""}`) ?? Number.MAX_SAFE_INTEGER, first.start];
  };
  return [...highlights].sort((a, b) => {
    const [ab, as] = position(a);
    const [bb, bs] = position(b);
    return ab - bb || as - bs;
  });
}

const formatDate = (iso: string) => new Date(iso).toLocaleDateString("tr-TR");

function ArticleNotes({ article }: { article: CurrentArticle }) {
  const layer = usePersonalLayer(article.articleId, article.versionId);
  const { onReveal } = useCurrentArticle();
  // Aynı anda tek form açık: "new" maddeye yeni not, not kimliği ya da "vurgu:<id>" vurgunun notu.
  const [editing, setEditing] = useState<string | null>(null);
  const highlights = sortByPosition(layer.highlights, article.blockOrder);

  if (layer.loading) return <p className="text-sm text-muted-foreground">Yükleniyor…</p>;

  return (
    <>
      <p className="text-sm font-medium text-primary">{article.label}</p>

      <section className="space-y-2">
        <h3 className="flex items-center justify-between gap-2 font-medium">
          Madde notlarım
          {editing !== "new" && (
            <Button size="sm" variant="ghost" onClick={() => setEditing("new")}>
              <Plus /> Not ekle
            </Button>
          )}
        </h3>
        {editing === "new" && (
          <NoteForm
            busy={layer.busy}
            rows={4}
            onCancel={() => setEditing(null)}
            onSave={async (text) => (await layer.saveNote(null, undefined, text)) && setEditing(null)}
          />
        )}
        {layer.articleNotes.length === 0 && editing !== "new" && (
          <p className="text-sm text-muted-foreground">Bu maddeye genel bir not yazmadınız.</p>
        )}
        <ul className="space-y-2">
          {layer.articleNotes.map((note) => (
            <li key={note.id} className="rounded-md border bg-card p-2.5">
              {editing === note.id ? (
                <NoteForm
                  initialText={note.text}
                  busy={layer.busy}
                  rows={4}
                  onCancel={() => setEditing(null)}
                  onSave={async (text) => (await layer.saveNote(null, note, text)) && setEditing(null)}
                  onDelete={async () => (await layer.removeNote(note)) && setEditing(null)}
                />
              ) : (
                <NoteText note={note} onEdit={() => setEditing(note.id)} />
              )}
            </li>
          ))}
        </ul>
      </section>

      <section className="space-y-2">
        <h3 className="font-medium">
          Vurgularım{highlights.length > 0 && <span className="text-muted-foreground"> ({highlights.length})</span>}
        </h3>
        {highlights.length === 0 && (
          <p className="text-sm text-muted-foreground">
            Metinde bir yer seçip renk seçerek vurgulayın; seçimin üstündeki not düğmesiyle not bağlayın.
          </p>
        )}
        <ul className="space-y-2">
          {highlights.map((highlight) => (
            <HighlightItem
              key={highlight.id}
              highlight={highlight}
              note={layer.noteOf.get(highlight.id)}
              layer={layer}
              editing={editing === `vurgu:${highlight.id}`}
              onEdit={(open) => setEditing(open ? `vurgu:${highlight.id}` : null)}
              onReveal={() => revealHighlight(highlight.id) && onReveal?.()}
            />
          ))}
        </ul>
      </section>
    </>
  );
}

function NoteText({ note, onEdit }: { note: PersonalNoteDto; onEdit: () => void }) {
  return (
    <div className="group flex gap-2">
      <p className="min-w-0 flex-1 font-reading text-[15px] whitespace-pre-wrap">{note.text}</p>
      <Button
        size="icon-sm"
        variant="ghost"
        className="shrink-0 text-muted-foreground"
        onClick={onEdit}
        aria-label="Notu düzenle"
        title={`Notu düzenle (son değişiklik ${formatDate(note.updatedAt)})`}
      >
        <Pencil />
      </Button>
    </div>
  );
}

function HighlightItem({
  highlight,
  note,
  layer,
  editing,
  onEdit,
  onReveal,
}: {
  highlight: HighlightDto;
  note: PersonalNoteDto | undefined;
  layer: ReturnType<typeof usePersonalLayer>;
  editing: boolean;
  onEdit: (open: boolean) => void;
  onReveal: () => void;
}) {
  const [confirmDelete, setConfirmDelete] = useState(false);

  return (
    <li className="overflow-hidden rounded-md border bg-card">
      <button
        type="button"
        onClick={onReveal}
        disabled={highlight.isStale}
        title={highlight.isStale ? undefined : "Metinde göster"}
        className={cn(
          "block w-full border-l-4 px-2.5 py-2 text-left",
          highlight.isStale ? "cursor-default border-muted-foreground/40 opacity-80" : "hover:bg-accent",
        )}
        style={highlight.isStale ? undefined : { borderLeftColor: `var(--highlight-${highlight.color})` }}
      >
        <span className={cn("line-clamp-3 rounded-[2px] font-reading text-[15px]", !highlight.isStale && highlightClassName(highlight.color))}>
          {highlight.quote}
        </span>
        {highlight.isStale && (
          <Badge variant="outline" className="mt-1.5 font-normal text-muted-foreground">
            <CircleAlert /> Bu metinde yok
          </Badge>
        )}
      </button>
      {highlight.isStale && (
        <p className="px-2.5 pb-1 text-xs text-muted-foreground">
          Madde değiştiği için vurguladığınız ifade gösterilen metinde bulunamadı.
        </p>
      )}

      <div className="space-y-1 px-2.5 pb-2">
        {editing ? (
          <NoteForm
            initialText={note?.text}
            busy={layer.busy}
            rows={4}
            onCancel={() => onEdit(false)}
            onSave={async (text) => (await layer.saveNote(highlight.id, note, text)) && onEdit(false)}
            onDelete={note ? async () => (await layer.removeNote(note)) && onEdit(false) : undefined}
          />
        ) : note ? (
          <NoteText note={note} onEdit={() => onEdit(true)} />
        ) : null}

        {!editing && (
          <div className="flex items-center justify-between gap-2 text-xs text-muted-foreground">
            <span>{formatDate(highlight.createdAt)}</span>
            {confirmDelete ? (
              <span className="flex items-center gap-1">
                {note ? "Vurgu notuyla silinsin mi?" : "Vurgu silinsin mi?"}
                <Button size="xs" variant="destructive" disabled={layer.busy} onClick={() => layer.removeHighlight(highlight.id)}>
                  Sil
                </Button>
                <Button size="xs" variant="ghost" onClick={() => setConfirmDelete(false)}>
                  Vazgeç
                </Button>
              </span>
            ) : (
              <span className="flex gap-0.5">
                {!note && (
                  <Button size="xs" variant="ghost" onClick={() => onEdit(true)}>
                    <NotebookPen /> Not ekle
                  </Button>
                )}
                <Button
                  size="icon-xs"
                  variant="ghost"
                  onClick={() => setConfirmDelete(true)}
                  aria-label="Vurguyu sil"
                  title="Vurguyu sil"
                  className="hover:text-destructive"
                >
                  <Trash2 />
                </Button>
              </span>
            )}
          </div>
        )}
      </div>
    </li>
  );
}
