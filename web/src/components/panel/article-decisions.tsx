"use client";

import { useState } from "react";
import { Link2, Lock, Pencil, Plus, Unlink } from "lucide-react";
import { toast } from "sonner";
import { useLinkDecision, useListDecisions, useUnlinkDecision } from "@/api/generated/admin-laws/admin-laws";
import type { DecisionDto } from "@/api/generated/model";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useDebouncedValue } from "@/hooks/use-debounced-value";
import { decisionCitation, outcomeBadgeVariant, outcomeLabels } from "@/lib/decisions";
import { DecisionDialog } from "./decision-form";
import { useLibraryRefresh } from "./use-library-refresh";

/** Maddeye bağlı kararlar: yeni karar ekle, var olanı arayıp bağla, bağlantıyı kaldır, kararı düzenle. */
export function ArticleDecisions({ articleId, decisions }: { articleId: string; decisions: DecisionDto[] }) {
  const refresh = useLibraryRefresh();
  const unlink = useUnlinkDecision();
  const [searching, setSearching] = useState(false);

  async function remove(decision: DecisionDto) {
    await unlink.mutateAsync({ articleId, decisionId: decision.id });
    await refresh();
    toast.success("Kararın bu maddeyle bağlantısı kaldırıldı.");
  }

  return (
    <section className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-lg">İçtihat ve norm denetimi kararları</h3>
        <div className="flex gap-1">
          <Button variant="outline" size="sm" onClick={() => setSearching((s) => !s)}>
            <Link2 /> Var olan kararı bağla
          </Button>
          <DecisionDialog
            articleIds={[articleId]}
            trigger={
              <Button variant="outline" size="sm">
                <Plus /> Yeni karar
              </Button>
            }
          />
        </div>
      </div>

      {searching && <DecisionSearch articleId={articleId} linkedIds={new Set(decisions.map((d) => d.id))} />}

      {decisions.length === 0 ? (
        <p className="text-sm text-muted-foreground">Bu maddeye bağlı karar yok.</p>
      ) : (
        <ul className="space-y-2">
          {decisions.map((d) => (
            <li key={d.id} className="flex items-start gap-2 rounded-md border p-3">
              <div className="min-w-0 flex-1">
                <p className="flex flex-wrap items-center gap-2 font-medium">
                  <Badge variant="secondary">{d.kind === "normDenetimi" ? "Norm denetimi" : "İçtihat"}</Badge>
                  {decisionCitation(d)}
                  {d.outcome && <Badge variant={outcomeBadgeVariant(d.outcome)}>{outcomeLabels[d.outcome]}</Badge>}
                  {d.visibility === "private" && (
                    <Badge variant="outline">
                      <Lock /> Özel
                    </Badge>
                  )}
                </p>
                {d.summary && <p className="mt-1 line-clamp-2 text-sm text-muted-foreground">{d.summary}</p>}
              </div>
              <DecisionDialog
                decisionId={d.id}
                trigger={
                  <Button variant="ghost" size="icon-sm" aria-label="Kararı düzenle" title="Kararı düzenle">
                    <Pencil />
                  </Button>
                }
              />
              <Button
                variant="ghost"
                size="icon-sm"
                aria-label="Bağlantıyı kaldır"
                title="Bu maddeyle bağlantıyı kaldır (karar silinmez)"
                onClick={() => remove(d).catch(() => toast.error("Bağlantı kaldırılamadı."))}
              >
                <Unlink />
              </Button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function DecisionSearch({ articleId, linkedIds }: { articleId: string; linkedIds: Set<string> }) {
  const refresh = useLibraryRefresh();
  const link = useLinkDecision();
  const [query, setQuery] = useState("");
  const q = useDebouncedValue(query.trim(), 300);
  const results = useListDecisions({ q: q || undefined, pageSize: 10 });
  const items = results.data?.status === 200 ? results.data.data.items : [];

  async function connect(decision: DecisionDto) {
    await link.mutateAsync({ articleId, decisionId: decision.id });
    await refresh();
    toast.success("Karar maddeye bağlandı.");
  }

  return (
    <div className="space-y-2 rounded-md border bg-muted/30 p-3">
      <Input
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        placeholder="Esas/karar no, daire ya da özette geçen ifade…"
        aria-label="Karar ara"
        autoFocus
      />
      {items.length === 0 ? (
        <p className="text-sm text-muted-foreground">{q ? "Eşleşen karar yok." : "Henüz karar girilmedi."}</p>
      ) : (
        <ul className="divide-y">
          {items.map(({ decision, articleCount }) => (
            <li key={decision.id} className="flex items-center gap-2 py-2">
              <div className="min-w-0 flex-1 text-sm">
                <p className="font-medium">{decisionCitation(decision)}</p>
                <p className="line-clamp-1 text-muted-foreground">
                  {decision.summary} · {articleCount} maddeye bağlı
                </p>
              </div>
              <Button
                size="sm"
                variant={linkedIds.has(decision.id) ? "ghost" : "outline"}
                disabled={linkedIds.has(decision.id) || link.isPending}
                onClick={() => connect(decision).catch(() => toast.error("Bağlanamadı."))}
              >
                {linkedIds.has(decision.id) ? "Bağlı" : "Bağla"}
              </Button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
