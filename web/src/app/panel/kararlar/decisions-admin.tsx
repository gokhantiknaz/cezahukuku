"use client";

import { useState } from "react";
import { Lock, Pencil, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { useDeleteDecision, useListDecisions } from "@/api/generated/admin-laws/admin-laws";
import { ConfirmDelete } from "@/components/panel/confirm-delete";
import { DecisionDialog } from "@/components/panel/decision-form";
import { useLibraryRefresh } from "@/components/panel/use-library-refresh";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useDebouncedValue } from "@/hooks/use-debounced-value";
import { decisionCitation, outcomeBadgeVariant, outcomeLabels } from "@/lib/decisions";
import { cn } from "@/lib/utils";

const pageSize = 20;
const kindFilters = [
  { value: "", label: "Tümü" },
  { value: "ictihat", label: "İçtihat" },
  { value: "normDenetimi", label: "Norm denetimi" },
] as const;

/** Bütün kararlar: arama, türe göre süzme, düzenleme, silme. Maddeye bağlama madde ekranından yapılır. */
export function DecisionsAdmin() {
  const refresh = useLibraryRefresh();
  const remove = useDeleteDecision();
  const [query, setQuery] = useState("");
  const [kind, setKind] = useState<string>("");
  const [page, setPage] = useState(1);
  const q = useDebouncedValue(query.trim(), 300);
  const list = useListDecisions({ q: q || undefined, kind: kind || undefined, page, pageSize });
  const data = list.data?.status === 200 ? list.data.data : undefined;
  const pages = data ? Math.max(1, Math.ceil(data.total / pageSize)) : 1;

  return (
    <div className="mt-4 space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <Input
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setPage(1);
          }}
          placeholder="Esas/karar no, daire ya da metinde geçen ifade…"
          aria-label="Karar ara"
          className="max-w-md"
        />
        <div className="flex gap-1" role="group" aria-label="Tür">
          {kindFilters.map((filter) => (
            <Button
              key={filter.value}
              size="sm"
              variant={kind === filter.value ? "default" : "outline"}
              onClick={() => {
                setKind(filter.value);
                setPage(1);
              }}
            >
              {filter.label}
            </Button>
          ))}
        </div>
        <DecisionDialog
          trigger={
            <Button className="ml-auto">
              <Plus /> Yeni karar
            </Button>
          }
        />
      </div>

      {!data ? (
        <p className="text-muted-foreground">Yükleniyor…</p>
      ) : data.items.length === 0 ? (
        <p className="rounded-lg border bg-card p-4 text-muted-foreground">
          {q || kind ? "Eşleşen karar yok." : "Henüz karar girilmedi. Kararları madde ekranından da ekleyebilirsiniz."}
        </p>
      ) : (
        <ul className="divide-y rounded-lg border bg-card">
          {data.items.map(({ decision, articleCount }) => (
            <li key={decision.id} className="flex items-start gap-2 px-4 py-3">
              <div className="min-w-0 flex-1">
                <p className="flex flex-wrap items-center gap-2 font-medium">
                  <Badge variant="secondary">{decision.kind === "normDenetimi" ? "Norm denetimi" : "İçtihat"}</Badge>
                  {decisionCitation(decision)}
                  {decision.outcome && <Badge variant={outcomeBadgeVariant(decision.outcome)}>{outcomeLabels[decision.outcome]}</Badge>}
                  {decision.visibility === "private" && (
                    <Badge variant="outline">
                      <Lock /> Özel
                    </Badge>
                  )}
                </p>
                {decision.summary && <p className="mt-1 line-clamp-2 text-sm">{decision.summary}</p>}
                <p className={cn("mt-1 text-xs", articleCount === 0 ? "text-destructive" : "text-muted-foreground")}>
                  {articleCount === 0 ? "Hiçbir maddeye bağlı değil" : `${articleCount} maddeye bağlı`}
                </p>
              </div>
              <DecisionDialog
                decisionId={decision.id}
                trigger={
                  <Button variant="ghost" size="icon-sm" aria-label="Kararı düzenle" title="Kararı düzenle">
                    <Pencil />
                  </Button>
                }
              />
              <ConfirmDelete
                title="Karar silinsin mi?"
                description="Karar bağlı olduğu bütün maddelerden kalkar. Kayıt veritabanında saklanır."
                onConfirm={async () => {
                  await remove.mutateAsync({ id: decision.id });
                  await refresh();
                  toast.success("Karar silindi.");
                }}
              >
                <Button variant="ghost" size="icon-sm" aria-label="Kararı sil" title="Kararı sil">
                  <Trash2 />
                </Button>
              </ConfirmDelete>
            </li>
          ))}
        </ul>
      )}

      {data && pages > 1 && (
        <div className="flex items-center justify-center gap-2 text-sm">
          <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
            Önceki
          </Button>
          <span>
            {page} / {pages} ({data.total} karar)
          </span>
          <Button size="sm" variant="outline" disabled={page >= pages} onClick={() => setPage((p) => p + 1)}>
            Sonraki
          </Button>
        </div>
      )}
    </div>
  );
}
