import { ExternalLink, Lock } from "lucide-react";
import type { DecisionDto } from "@/api/generated/model";
import { RichTextView } from "@/components/rich-text/rich-text-view";
import { Badge } from "@/components/ui/badge";
import { formatDate } from "@/lib/dates";
import { decisionCitation, outcomeBadgeVariant, outcomeLabels } from "@/lib/decisions";

/**
 * Maddeye bağlı kararlar (İçtihatlar / Norm Denetimi sekmeleri): künye, özet; tam metin açılır kapanır
 * (<details>: JavaScript gerekmez, sunucuda çizilir).
 */
export function DecisionList({ decisions, empty }: { decisions: DecisionDto[]; empty: string }) {
  if (decisions.length === 0) return <p className="text-muted-foreground">{empty}</p>;
  return (
    <ol className="divide-y">
      {decisions.map((d) => (
        <li key={d.id} className="py-4 first:pt-0 last:pb-0">
          <h4 className="flex flex-wrap items-center gap-2 font-medium">
            {decisionCitation(d)}
            {d.outcome && <Badge variant={outcomeBadgeVariant(d.outcome)}>{outcomeLabels[d.outcome]}</Badge>}
            {d.effectiveVisibility === "private" && (
              <Badge variant="outline" className="font-normal" title="Yalnız giriş yapan kullanıcılar görür">
                <Lock /> Özel
              </Badge>
            )}
          </h4>
          {d.gazetteDate && (
            <p className="text-sm text-muted-foreground">
              Resmî Gazete: {formatDate(d.gazetteDate)}
              {d.gazetteNumber && `, sayı ${d.gazetteNumber}`}
            </p>
          )}
          {d.summary && <p className="reading mt-2 whitespace-pre-line">{d.summary}</p>}
          {d.html && (
            <details className="mt-2">
              <summary className="cursor-pointer text-sm text-primary">Kararın metni</summary>
              <RichTextView html={d.html} className="mt-2 border-l-2 pl-4" />
            </details>
          )}
          {d.sourceUrl && (
            <a
              href={d.sourceUrl}
              target="_blank"
              rel="noopener noreferrer nofollow"
              className="mt-2 inline-flex items-center gap-1 text-sm text-primary underline-offset-2 hover:underline print:hidden"
            >
              <ExternalLink className="size-3.5" /> Kaynakta aç
            </a>
          )}
        </li>
      ))}
    </ol>
  );
}
