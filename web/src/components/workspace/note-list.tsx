import { Lock } from "lucide-react";
import type { EditorialNoteDto } from "@/api/generated/model";
import { RichTextView } from "@/components/rich-text/rich-text-view";
import { Badge } from "@/components/ui/badge";

/** Editoryal notlar (gerekçe, açıklama, ders notu); yoksa `empty` metni. */
export function NoteList({ notes, empty }: { notes: EditorialNoteDto[]; empty: string }) {
  if (notes.length === 0) return <p className="text-muted-foreground">{empty}</p>;
  return (
    <div className="divide-y">
      {notes.map((note) => (
        <article key={note.id} className="py-4 first:pt-0 last:pb-0">
          {(note.title || note.effectiveVisibility === "private") && (
            <h4 className="mb-2 flex flex-wrap items-center gap-2 text-lg">
              {note.title}
              {note.effectiveVisibility === "private" && (
                <Badge variant="outline" className="font-normal" title="Yalnız giriş yapan kullanıcılar görür">
                  <Lock /> Özel
                </Badge>
              )}
            </h4>
          )}
          <RichTextView html={note.html} />
        </article>
      ))}
    </div>
  );
}
