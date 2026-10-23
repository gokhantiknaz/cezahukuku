"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useGetArticleHistory } from "@/api/generated/library/library";
import type { ArticleVersionDetail } from "@/api/generated/model";
import { Badge } from "@/components/ui/badge";
import { formatDate } from "@/lib/dates";
import { paragraphKey, ParagraphList } from "./article-text";

function range(v: ArticleVersionDetail) {
  const from = v.validFrom ? formatDate(v.validFrom) : "başlangıcı girilmemiş";
  return v.validTo ? `${from} – ${formatDate(v.validTo)}` : `${from} – …`;
}

/**
 * Değişiklikler sekmesi: maddenin sürümleri, en yeni önce. Her sürümde bir önceki sürüme göre değişen fıkralar
 * işaretlidir; sürüm o tarihteki metin olarak açılabilir.
 */
export function ArticleHistory({
  lawSlug,
  articleSlug,
  shownVersionId,
}: {
  lawSlug: string;
  articleSlug: string;
  /** Okuma ekranında şu an gösterilen sürüm. */
  shownVersionId?: string;
}) {
  const pathname = usePathname();
  const { data, isPending, error } = useGetArticleHistory(lawSlug, articleSlug);
  const versions = data?.status === 200 ? data.data : [];

  if (isPending) return <p className="text-muted-foreground">Yükleniyor…</p>;
  if (error) return <p className="text-destructive">Değişiklik geçmişi yüklenemedi.</p>;
  if (versions.length === 0) return <p className="text-muted-foreground">Bu maddenin metni henüz girilmedi.</p>;

  return (
    <div className="space-y-5">
      {versions.length === 1 && (
        <p className="text-sm text-muted-foreground">
          Bu maddenin kayıtlı tek bir sürümü var.
          {versions[0].amendedBy &&
            " Metin değişiklik notu taşıyor; değişiklikten önceki metin henüz girilmediği için o tarihten önceki bir gün sorulduğunda metin gösterilmez."}
        </p>
      )}
      <ol className="space-y-5">
        {versions.map((version, index) => {
          const older = versions[index + 1];
          const olderKeys = new Set(older?.paragraphs.map(paragraphKey) ?? []);
          const changed = older
            ? new Set(version.paragraphs.filter((p) => !olderKeys.has(paragraphKey(p))).map((p) => p.id))
            : undefined;

          return (
            <li key={version.id} className="rounded-lg border">
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
              <div className="px-3 py-3">
                {version.paragraphs.length === 0 ? (
                  <p className="text-muted-foreground">{version.isRepealed ? "Mülga." : "Metin yok."}</p>
                ) : (
                  <ParagraphList paragraphs={version.paragraphs} marked={changed} />
                )}
                {older && changed && changed.size > 0 && (
                  <p className="mt-2 text-xs text-muted-foreground">İşaretli fıkralar bir önceki sürüme göre değişti.</p>
                )}
              </div>
            </li>
          );
        })}
      </ol>
    </div>
  );
}
