import type { ReactNode } from "react";
import { Lock } from "lucide-react";
import type { EditorialNoteDto } from "@/api/generated/model";
import { RichTextView } from "@/components/rich-text/rich-text-view";
import { Badge } from "@/components/ui/badge";

/**
 * Editoryal notlar (gerekçe, açıklama, ders notu); yoksa `empty` metni. `actions` verilirse (yönetici) her notun
 * başlık satırının sağında gösterilir.
 */
export function NoteList({
  notes,
  empty,
  actions,
}: {
  notes: EditorialNoteDto[];
  empty: string;
  actions?: (note: EditorialNoteDto, index: number) => ReactNode;
}) {
  if (notes.length === 0) return <p className="text-muted-foreground">{empty}</p>;
  return (
    <div className="divide-y">
      {notes.map((note, index) => (
        <article key={note.id} className="py-4 first:pt-0 last:pb-0">
          {(note.title || note.effectiveVisibility === "private" || actions) && (
            <div className="mb-2 flex items-start gap-2">
              <h4 className="flex flex-wrap items-center gap-2 text-lg">
                {note.title}
                {note.effectiveVisibility === "private" && (
                  <Badge variant="outline" className="font-normal" title="Yalnız giriş yapan kullanıcılar görür">
                    <Lock /> Özel
                  </Badge>
                )}
              </h4>
              {actions?.(note, index)}
            </div>
          )}
          <RichTextView html={note.html} />
        </article>
      ))}
    </div>
  );
}
