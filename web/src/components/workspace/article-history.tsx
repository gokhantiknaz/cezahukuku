"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { ChevronDown, Plus } from "lucide-react";
import { useGetArticleHistory } from "@/api/generated/library/library";
import type { ArticleVersionDetail, BlockChange, DiffPiece } from "@/api/generated/model";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { formatDate } from "@/lib/dates";
import { cn } from "@/lib/utils";
import { changeKey, ParagraphList } from "./article-text";

function range(v: ArticleVersionDetail) {
  const from = v.validFrom ? formatDate(v.validFrom) : "başlangıcı girilmemiş";
  return v.validTo ? `${from} – ${formatDate(v.validTo)}` : `${from} – …`;
}

/** "(1) a)" → "(1) numaralı fıkranın a) bendi"; "(2)" → "(2) numaralı fıkra". */
function blockName({ label, itemId }: BlockChange) {
  if (itemId === null) return label ? `${label} numaralı fıkra` : "Fıkra";
  const [paragraph, item] = label.startsWith("(") ? [label.split(" ")[0], label.split(" ").slice(1).join(" ")] : ["", label];
  if (!paragraph) return item ? `${item} bendi` : "Bentlerden sonraki metin";
  return `${paragraph} numaralı fıkranın ${item ? `${item} bendi` : "bentlerden sonraki metni"}`;
}

const kindLabels = { changed: "değişti", added: "eklendi", removed: "mülga" } as const;

/** Eski metin: çıkarılan kelimeler üstü çizili. */
function OldText({ pieces }: { pieces: DiffPiece[] }) {
  return pieces
    .filter((p) => p.kind !== "inserted")
    .map((p, i) =>
      p.kind === "deleted" ? (
        <del key={i} className="bg-change/10 text-muted-foreground decoration-change">
          {p.text}
        </del>
      ) : (
        p.text
      ),
    );
}

/** Yeni metin: gelen kelimeler kırmızı. */
function NewText({ pieces }: { pieces: DiffPiece[] }) {
  return pieces
    .filter((p) => p.kind !== "deleted")
    .map((p, i) =>
      p.kind === "inserted" ? (
        <ins key={i} className="text-change no-underline">
          {p.text}
        </ins>
      ) : (
        p.text
      ),
    );
}

function ChangeItem({ change, versionId }: { change: BlockChange; versionId: string }) {
  return (
    <li
      data-history-key={changeKey({ versionId, paragraphId: change.paragraphId, itemId: change.itemId })}
      className="scroll-mt-20 rounded-md border bg-background p-3"
    >
      <p className="flex flex-wrap items-center gap-2 text-sm font-medium">
        {blockName(change)}
        <Badge variant={change.kind === "changed" ? "secondary" : "outline"} className={cn(change.kind !== "changed" && "text-change")}>
          {kindLabels[change.kind]}
        </Badge>
      </p>
      {change.kind === "changed" ? (
        <dl className="reading mt-2 grid gap-x-3 gap-y-1.5 sm:grid-cols-[4.5rem_1fr]">
          <dt className="font-sans text-sm text-muted-foreground sm:pt-1">Önceki</dt>
          <dd className="whitespace-pre-line">
            <OldText pieces={change.pieces} />
          </dd>
          <dt className="font-sans text-sm text-muted-foreground sm:pt-1">Yeni</dt>
          <dd className="whitespace-pre-line">
            <NewText pieces={change.pieces} />
          </dd>
        </dl>
      ) : change.kind === "added" ? (
        <p className="reading mt-2 whitespace-pre-line text-change">{change.newText}</p>
      ) : (
        <p className="reading mt-2 whitespace-pre-line text-muted-foreground line-through decoration-change">{change.oldText}</p>
      )}
    </li>
  );
}

/**
 * Değişiklikler sekmesi: maddenin sürümleri, en yeni önce. Her sürümde yalnız bir önceki sürüme göre değişen fıkra
 * ve bentler (önceki ve yeni hâli, kelime düzeyinde); sürümün tam metni istenince açılır ve o tarihteki metin olarak
 * gösterilebilir. `focus` verilirse (okuma ekranında kırmızı yere tıklanınca) o değişikliğe kaydırılır.
 */
export function ArticleHistory({
  lawSlug,
  articleSlug,
  shownVersionId,
  focus,
  canEdit,
}: {
  lawSlug: string;
  articleSlug: string;
  /** Okuma ekranında şu an gösterilen sürüm. */
  shownVersionId?: string;
  focus?: { key: string; n: number } | null;
  /** Yönetici: önceki metni girmek için bağlantı gösterilir. */
  canEdit?: boolean;
}) {
  const pathname = usePathname();
  const { data, isPending, error } = useGetArticleHistory(lawSlug, articleSlug);
  const entries = data?.status === 200 ? data.data : [];
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const listRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const list = listRef.current;
    if (!focus || !list || entries.length === 0) return;
    const [versionId, paragraphId] = focus.key.split(":");
    // Eklenen bütün fıkrada değişiklik fıkra düzeyindedir; o da yoksa sürümün kaydı.
    const target =
      list.querySelector<HTMLElement>(`[data-history-key="${CSS.escape(focus.key)}"]`) ??
      list.querySelector<HTMLElement>(`[data-history-key="${CSS.escape(`${versionId}:${paragraphId}:`)}"]`) ??
      list.querySelector<HTMLElement>(`[data-history-version="${CSS.escape(versionId)}"]`);
    if (!target) return;
    target.scrollIntoView({ block: "center", behavior: "smooth" });
    target.animate([{ boxShadow: "0 0 0 3px var(--change)" }, { boxShadow: "0 0 0 3px transparent" }], {
      duration: 800,
      iterations: 2,
      delay: 300,
    });
  }, [focus, entries.length]);

  if (isPending) return <p className="text-muted-foreground">Yükleniyor…</p>;
  if (error) return <p className="text-destructive">Değişiklik geçmişi yüklenemedi.</p>;
  if (entries.length === 0) return <p className="text-muted-foreground">Bu maddenin metni henüz girilmedi.</p>;

  const toggle = (id: string) =>
    setExpanded((current) => {
      const next = new Set(current);
      if (!next.delete(id)) next.add(id);
      return next;
    });
  const only = entries.length === 1 ? entries[0].version : null;

  return (
    <div ref={listRef} className="space-y-5">
      {only && (
        <div className="space-y-2 text-sm text-muted-foreground">
          <p>
            {only.amendedBy
              ? `Bu madde ${only.amendedBy} ile değişmiş; değişiklikten önceki metni henüz girilmedi. Önceki metin girilince değişen kelimeler madde metninde kırmızı görünür.`
              : "Bu maddenin kayıtlı bir değişikliği yok."}
          </p>
          {canEdit && only.amendedBy && only.validFrom && (
            <Button size="sm" variant="outline" asChild>
              <Link href={`/panel/kutuphane/${lawSlug}?madde=${articleSlug}&surum=onceki`}>
                <Plus /> Değişiklikten önceki metni ekle
              </Link>
            </Button>
          )}
        </div>
      )}
      <ol className="space-y-5">
        {entries.map(({ version, changes }, index) => {
          const oldest = index === entries.length - 1;
          const open = expanded.has(version.id);
          return (
            <li key={version.id} data-history-version={version.id} className="scroll-mt-20 rounded-lg border">
              <div className="flex flex-wrap items-center gap-2 border-b bg-muted/40 px-3 py-2">
                <span className="font-medium">{range(version)}</span>
                {version.validTo === null && <Badge variant="secondary">yürürlükte</Badge>}
                {version.isRepealed && <Badge variant="outline">mülga</Badge>}
                {version.id === shownVersionId && <Badge>gösterilen metin</Badge>}
                {version.amendedBy && <span className="text-sm text-muted-foreground">{version.amendedBy}</span>}
                {version.validFrom && version.id !== shownVersionId && (
                  <Link
                    href={`${pathname}?tarih=${version.validFrom}`}
                    className="ml-auto text-sm text-primary underline-offset-2 hover:underline print:hidden"
                  >
                    Bu metni göster
                  </Link>
                )}
              </div>
              <div className="space-y-3 px-3 py-3">
                {oldest ? (
                  entries.length > 1 && <p className="text-sm text-muted-foreground">Kayıtlı ilk metin.</p>
                ) : changes.length === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    Metin bir önceki sürümle aynı; yalnız tarih ya da düzenleme bilgisi farklı.
                  </p>
                ) : (
                  <ul className="space-y-2">
                    {changes.map((change) => (
                      <ChangeItem key={`${change.paragraphId}:${change.itemId}:${change.kind}`} change={change} versionId={version.id} />
                    ))}
                  </ul>
                )}
                <Button
                  size="sm"
                  variant="ghost"
                  className="-ml-2 print:hidden"
                  aria-expanded={open}
                  onClick={() => toggle(version.id)}
                >
                  <ChevronDown className={cn("transition-transform", open && "rotate-180")} />
                  {open ? "Tam metni gizle" : "Tam metni göster"}
                </Button>
                {open &&
                  (version.paragraphs.length === 0 ? (
                    <p className="text-muted-foreground">{version.isRepealed ? "Mülga." : "Metin yok."}</p>
                  ) : (
                    <ParagraphList paragraphs={version.paragraphs} />
                  ))}
              </div>
            </li>
          );
        })}
      </ol>
    </div>
  );
}
