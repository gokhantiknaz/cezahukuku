"use client";

import { useState } from "react";
import { Pencil, Trash2, Unlink } from "lucide-react";
import { toast } from "sonner";
import { useDeleteEditorialNote, useUnlinkDecision } from "@/api/generated/admin-laws/admin-laws";
import type { DecisionDto, EditorialNoteDto } from "@/api/generated/model";
import { ConfirmDelete } from "@/components/panel/confirm-delete";
import { DecisionDialog } from "@/components/panel/decision-form";
import { NoteForm } from "@/components/panel/notes-editor";
import { useLibraryRefresh } from "@/components/panel/use-library-refresh";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { decisionCitation } from "@/lib/decisions";

// Okuma ekranındaki not türleri: düğme adlarında ("Gerekçeyi düzenle") ve pencere başlığında.
const noteWords = {
  gerekce: { label: "Gerekçe", accusative: "Gerekçeyi", ordinal: "gerekçe" },
  aciklama: { label: "Açıklama", accusative: "Açıklamayı", ordinal: "açıklama" },
} as const;

/**
 * Yönetici için okuma ekranındaki bir notun düğmeleri (spec 0001): Düzenle penceresi ve onaylı Kaldır. Not
 * okuyuculardan kaldırılır, kayıt veritabanında saklanır.
 */
export function NoteActions({
  note,
  articleId,
  index,
}: {
  note: EditorialNoteDto;
  articleId: string;
  /** Sekmedeki sırası (0'dan); başlıksız notun erişilebilir adı için. */
  index: number;
}) {
  const [open, setOpen] = useState(false);
  const refresh = useLibraryRefresh();
  const remove = useDeleteEditorialNote();
  if (note.kind !== "gerekce" && note.kind !== "aciklama") return null;
  const words = noteWords[note.kind];
  const name = note.title || `${index + 1}. ${words.ordinal}`;

  return (
    <span className="ml-auto flex shrink-0 gap-0.5 print:hidden">
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogTrigger asChild>
          <Button variant="ghost" size="icon-sm" aria-label={`${words.accusative} düzenle: ${name}`} title="Düzenle">
            <Pencil />
          </Button>
        </DialogTrigger>
        <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-3xl">
          <DialogHeader>
            <DialogTitle>{words.label} düzenle</DialogTitle>
            <DialogDescription>Kaydedince bu sekmede güncel hâli görünür.</DialogDescription>
          </DialogHeader>
          {open && (
            <NoteForm
              target={{ level: "article", id: articleId }}
              kinds={{ [note.kind]: words.label }}
              note={note}
              onDone={() => setOpen(false)}
            />
          )}
        </DialogContent>
      </Dialog>
      <ConfirmDelete
        title={`${words.label} kaldırılsın mı?`}
        description="Okuyuculardan kaldırılır, kayıt veritabanında saklanır."
        confirmLabel="Kaldır"
        onGone={refresh}
        onConfirm={async () => {
          await remove.mutateAsync({ id: note.id });
          await refresh();
          toast.success(`${words.label} kaldırıldı.`);
        }}
      >
        <Button variant="ghost" size="icon-sm" aria-label={`${words.accusative} kaldır: ${name}`} title="Kaldır">
          <Trash2 />
        </Button>
      </ConfirmDelete>
    </span>
  );
}

/**
 * Yönetici için okuma ekranındaki bir kararın düğmeleri (spec 0001): Düzenle penceresi ve onaylı "Bu maddeden
 * kaldır". Karar silinmez; diğer maddelerle bağlantısı kalır (tümden silme karar yönetim sayfasında).
 */
export function DecisionActions({ decision, articleId }: { decision: DecisionDto; articleId: string }) {
  const refresh = useLibraryRefresh();
  const unlink = useUnlinkDecision();
  const citation = decisionCitation(decision);

  return (
    <span className="ml-auto flex shrink-0 gap-0.5 print:hidden">
      <DecisionDialog
        decisionId={decision.id}
        trigger={
          <Button variant="ghost" size="icon-sm" aria-label={`Kararı düzenle: ${citation}`} title="Düzenle">
            <Pencil />
          </Button>
        }
      />
      <ConfirmDelete
        title="Karar bu maddeden kaldırılsın mı?"
        description="Yalnız bu maddeden kaldırılır, karar silinmez; bağlı olduğu diğer maddelerde görünmeye devam eder."
        confirmLabel="Kaldır"
        onGone={refresh}
        onConfirm={async () => {
          await unlink.mutateAsync({ articleId, decisionId: decision.id });
          await refresh();
          toast.success("Karar bu maddeden kaldırıldı.");
        }}
      >
        <Button variant="ghost" size="icon-sm" aria-label={`Kararı bu maddeden kaldır: ${citation}`} title="Bu maddeden kaldır">
          <Unlink />
        </Button>
      </ConfirmDelete>
    </span>
  );
}
