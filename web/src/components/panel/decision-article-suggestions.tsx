"use client";

import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { ExternalLink, Sparkles } from "lucide-react";
import { suggestDecisionArticles } from "@/api/generated/admin-laws/admin-laws";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { useDebouncedValue } from "@/hooks/use-debounced-value";

/**
 * Karar metninden madde bağlantısı önerisi: özette ve metinde geçen atıflar ("TCK 81/2", "5237 sayılı Kanunun 82 nci
 * maddesi") kütüphanedeki maddelere çözülür. Seçilen maddeler kaydedince karara bağlanır; zaten bağlı olanlar
 * (`fixedIds` ve kararın bağlantıları) işaretli ve kilitli görünür.
 */
export function DecisionArticleSuggestions({
  text,
  decisionId,
  fixedIds,
  selected,
  onChange,
}: {
  text: string;
  decisionId?: string;
  fixedIds: string[];
  selected: string[];
  onChange: (selected: string[]) => void;
}) {
  const debounced = useDebouncedValue(text.trim(), 700);
  const query = useQuery({
    queryKey: ["decision-article-suggestions", decisionId ?? null, debounced],
    queryFn: async () => {
      const response = await suggestDecisionArticles({ text: debounced, decisionId: decisionId ?? null });
      return response.status === 200 ? response.data : { items: [], notFound: [] };
    },
    enabled: debounced.length > 0,
    placeholderData: keepPreviousData,
  });
  const data = debounced ? query.data : undefined;
  const isFixed = (id: string) => fixedIds.includes(id);
  const selectable = data?.items.filter((i) => !i.linked && !isFixed(i.article.articleId)) ?? [];
  const allSelected = selectable.length > 0 && selectable.every((i) => selected.includes(i.article.articleId));

  function toggle(id: string, on: boolean) {
    onChange(on ? [...selected, id] : selected.filter((s) => s !== id));
  }

  return (
    <section className="space-y-2 rounded-md border bg-muted/30 p-3" aria-labelledby="decision-suggestions-title">
      <div className="flex flex-wrap items-center gap-2">
        <h3 id="decision-suggestions-title" className="flex items-center gap-1.5 text-sm font-medium">
          <Sparkles className="size-4 text-primary" /> Metinde geçen maddeler
        </h3>
        {query.isFetching && <span className="text-xs text-muted-foreground">Aranıyor…</span>}
        {selectable.length > 1 && (
          <Button
            type="button"
            size="sm"
            variant="ghost"
            className="ml-auto h-7"
            onClick={() =>
              onChange(
                allSelected
                  ? selected.filter((id) => !selectable.some((i) => i.article.articleId === id))
                  : [...new Set([...selected, ...selectable.map((i) => i.article.articleId)])],
              )
            }
          >
            {allSelected ? "Seçimi kaldır" : "Tümünü seç"}
          </Button>
        )}
      </div>

      {!data ? (
        <p className="text-sm text-muted-foreground">
          Özete ya da metne atıf yazdıkça (“TCK 81/2”, “5237 sayılı Kanunun 82 nci maddesi”) kütüphanedeki maddeler
          burada önerilir.
        </p>
      ) : data.items.length === 0 ? (
        <p className="text-sm text-muted-foreground">Metinde kütüphanedeki bir maddeye atıf bulunamadı.</p>
      ) : (
        <>
          <p className="text-xs text-muted-foreground">Seçtiğiniz maddeler kaydedince karara bağlanır.</p>
          <ul className="divide-y">
            {data.items.map(({ article, mentions, cited, excerpt, linked }) => {
              const locked = linked || isFixed(article.articleId);
              const id = `suggestion-${article.articleId}`;
              return (
                <li key={article.articleId} className="flex items-start gap-2.5 py-2">
                  <Checkbox
                    id={id}
                    className="mt-0.5"
                    checked={locked || selected.includes(article.articleId)}
                    disabled={locked}
                    onCheckedChange={(on) => toggle(article.articleId, on === true)}
                  />
                  <div className="min-w-0 flex-1 text-sm">
                    <label htmlFor={id} className="flex flex-wrap items-center gap-x-2 gap-y-1 font-medium">
                      <span>
                        {article.lawLabel} m. {article.number}
                        {article.title && <span className="font-normal text-muted-foreground"> · {article.title}</span>}
                      </span>
                      {mentions > 1 && <Badge variant="secondary">{mentions} kez</Badge>}
                      {locked && <Badge variant="outline">Bağlı</Badge>}
                    </label>
                    <p className="mt-0.5 line-clamp-2 text-xs text-muted-foreground" title={cited.join(" · ")}>
                      {excerpt}
                    </p>
                  </div>
                  <a
                    href={`/${article.lawSlug}/${article.articleSlug}`}
                    target="_blank"
                    rel="noreferrer"
                    className="mt-0.5 shrink-0 text-muted-foreground hover:text-foreground"
                    aria-label={`${article.lawLabel} m. ${article.number} yeni sekmede aç`}
                    title="Maddeyi yeni sekmede aç"
                  >
                    <ExternalLink className="size-4" />
                  </a>
                </li>
              );
            })}
          </ul>
        </>
      )}
      {data && data.notFound.length > 0 && (
        <p className="text-xs text-muted-foreground">
          Kütüphanede bulunamadı: {data.notFound.join(", ")}
        </p>
      )}
    </section>
  );
}
