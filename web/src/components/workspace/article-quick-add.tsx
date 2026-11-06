"use client";

import { useState } from "react";
import Link from "next/link";
import { Link2, Pencil, Plus } from "lucide-react";
import type { ArticleLinkKind, DecisionKind, EditorialNoteKind } from "@/api/generated/model";
import { DecisionSearch } from "@/components/panel/article-decisions";
import { LinkForm } from "@/components/panel/article-links";
import { DecisionForm } from "@/components/panel/decision-form";
import { NoteForm } from "@/components/panel/notes-editor";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";

/** Okuma ekranındaki sekmelerden hızlıca eklenebilen kayıtlar. */
export type QuickAddKind =
  | { type: "note"; kind: Extract<EditorialNoteKind, "gerekce" | "aciklama"> }
  | { type: "decision"; kind: DecisionKind }
  | { type: "link"; kind: ArticleLinkKind };

const labels = {
  gerekce: { button: "Gerekçe ekle", title: "Gerekçe ekle" },
  aciklama: { button: "Açıklama ekle", title: "Açıklama ekle" },
  ictihat: { button: "İçtihat ekle", title: "İçtihat ekle" },
  normDenetimi: { button: "Norm denetimi ekle", title: "Norm denetimi kararı ekle" },
  ilgili: { button: "İlgili madde ekle", title: "İlgili madde ekle" },
  eskiKanunKarsiligi: { button: "765 karşılığı ekle", title: "Eski kanun (765) karşılığı ekle" },
} satisfies Record<QuickAddKind["kind"], { button: string; title: string }>;

/**
 * Yönetici için sekmenin üstünde: "Gerekçe ekle" gibi düğme kısa kayıt penceresini açar; "Düzenleme ekranı"
 * maddenin paneldeki tam düzenleme ekranına gider (var olan kayıtları düzeltme ve silme orada).
 */
export function ArticleQuickAdd({
  add,
  articleId,
  articleLabel,
  linkedDecisionIds,
  editHref,
}: {
  add: QuickAddKind;
  articleId: string;
  /** "TCK Madde 81" — pencere başlığında. */
  articleLabel: string;
  /** Maddeye zaten bağlı kararlar ("Var olan kararı bağla" listesinde "Bağlı" görünür). */
  linkedDecisionIds: string[];
  editHref: string;
}) {
  const [open, setOpen] = useState(false);
  const { button, title } = labels[add.kind];
  const close = () => setOpen(false);

  return (
    <div className="mb-3 flex flex-wrap items-center justify-end gap-2 print:hidden">
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogTrigger asChild>
          <Button size="sm" variant="outline">
            <Plus /> {button}
          </Button>
        </DialogTrigger>
        <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-3xl">
          <DialogHeader>
            <DialogTitle>{title}</DialogTitle>
            <DialogDescription>
              {articleLabel}. Kaydedince bu sekmede görünür; var olan kayıtları{" "}
              <Link href={editHref} className="text-primary underline-offset-2 hover:underline">
                düzenleme ekranında
              </Link>{" "}
              değiştirebilirsiniz.
            </DialogDescription>
          </DialogHeader>
          {open &&
            (add.type === "note" ? (
              <NoteForm
                target={{ level: "article", id: articleId }}
                kinds={{ [add.kind]: button }}
                onDone={close}
              />
            ) : add.type === "decision" ? (
              <DecisionQuickAdd
                articleId={articleId}
                kind={add.kind}
                linkedDecisionIds={linkedDecisionIds}
                onDone={close}
              />
            ) : (
              <LinkForm articleId={articleId} initialKind={add.kind} onDone={close} />
            ))}
        </DialogContent>
      </Dialog>
      <Button size="sm" variant="ghost" asChild>
        <Link href={editHref}>
          <Pencil /> Düzenleme ekranı
        </Link>
      </Button>
    </div>
  );
}

/** Karar: yeni karar girilir ya da başka maddeye girilmiş karar bu maddeye de bağlanır. */
function DecisionQuickAdd({
  articleId,
  kind,
  linkedDecisionIds,
  onDone,
}: {
  articleId: string;
  kind: DecisionKind;
  linkedDecisionIds: string[];
  onDone: () => void;
}) {
  const [mode, setMode] = useState<"new" | "link">("new");
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap gap-2" role="group" aria-label="Karar ekleme biçimi">
        <Button size="sm" variant={mode === "new" ? "default" : "outline"} aria-pressed={mode === "new"} onClick={() => setMode("new")}>
          <Plus /> Yeni karar
        </Button>
        <Button size="sm" variant={mode === "link" ? "default" : "outline"} aria-pressed={mode === "link"} onClick={() => setMode("link")}>
          <Link2 /> Var olan kararı bağla
        </Button>
      </div>
      {mode === "new" ? (
        <DecisionForm articleIds={[articleId]} initialKind={kind} onDone={onDone} />
      ) : (
        <DecisionSearch articleId={articleId} linkedIds={new Set(linkedDecisionIds)} />
      )}
    </div>
  );
}
