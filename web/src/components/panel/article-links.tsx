"use client";

import { useState } from "react";
import { Plus, Trash2, X } from "lucide-react";
import { toast } from "sonner";
import { useCreateArticleLink, useDeleteArticleLink, useSearchArticles } from "@/api/generated/admin-laws/admin-laws";
import type { ArticleLinkDto, ArticleLinkKind, ArticleSearchHit } from "@/api/generated/model";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useDebouncedValue } from "@/hooks/use-debounced-value";
import { ConfirmDelete } from "./confirm-delete";
import { emptyToNull, Field, fieldMessages, FormMessages, OptionSelect, otherMessages } from "./form";
import { useLibraryRefresh } from "./use-library-refresh";

const kindLabels: Record<ArticleLinkKind, string> = {
  ilgili: "İlgili madde",
  eskiKanunKarsiligi: "Eski kanun karşılığı (ör. 765)",
};

function linkLabel(link: ArticleLinkDto) {
  return link.article
    ? `${link.article.lawLabel} Madde ${link.article.number}${link.article.title ? ` — ${link.article.title}` : ""}`
    : (link.externalLabel ?? "");
}

/** Maddenin bağlantıları: ilgili maddeler ve eski kanun karşılığı. Başka maddeden gelenler de gösterilir. */
export function ArticleLinks({ articleId, links }: { articleId: string; links: ArticleLinkDto[] }) {
  const [adding, setAdding] = useState(false);

  return (
    <section className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-lg">İlgili maddeler ve eski kanun karşılığı</h3>
        {!adding && (
          <Button variant="outline" size="sm" onClick={() => setAdding(true)}>
            <Plus /> Bağlantı ekle
          </Button>
        )}
      </div>
      {adding && <LinkForm articleId={articleId} onDone={() => setAdding(false)} />}
      {links.length === 0 && !adding ? (
        <p className="text-sm text-muted-foreground">Bağlantı yok.</p>
      ) : (
        <ul className="space-y-2">
          {links.map((link) => (
            <li key={link.id} className="flex items-start gap-2 rounded-md border p-3">
              <div className="min-w-0 flex-1">
                <p className="flex flex-wrap items-center gap-2">
                  <Badge variant="secondary">{link.kind === "ilgili" ? "İlgili" : "Eski kanun"}</Badge>
                  {link.incoming && <Badge variant="outline">o maddeden bağlı</Badge>}
                  <span className="font-medium">{linkLabel(link)}</span>
                </p>
                {link.note && <p className="mt-1 text-sm text-muted-foreground">{link.note}</p>}
              </div>
              <DeleteLink link={link} />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function DeleteLink({ link }: { link: ArticleLinkDto }) {
  const refresh = useLibraryRefresh();
  const remove = useDeleteArticleLink();
  return (
    <ConfirmDelete
      title="Bağlantı kaldırılsın mı?"
      description={
        link.incoming
          ? "Bu bağlantı öbür maddeden kurulmuş; kaldırılınca iki maddeden de kalkar."
          : "Bağlantı iki maddeden de kalkar; maddeler silinmez."
      }
      onConfirm={async () => {
        await remove.mutateAsync({ id: link.id });
        await refresh();
        toast.success("Bağlantı kaldırıldı.");
      }}
    >
      <Button variant="ghost" size="icon-sm" aria-label="Bağlantıyı kaldır" title="Bağlantıyı kaldır">
        <Trash2 />
      </Button>
    </ConfirmDelete>
  );
}

function LinkForm({ articleId, onDone }: { articleId: string; onDone: () => void }) {
  const refresh = useLibraryRefresh();
  const create = useCreateArticleLink();
  const [kind, setKind] = useState<ArticleLinkKind>("ilgili");
  const [query, setQuery] = useState("");
  const [target, setTarget] = useState<ArticleSearchHit | null>(null);
  const [externalLabel, setExternalLabel] = useState("");
  const [note, setNote] = useState("");
  const q = useDebouncedValue(query.trim(), 300);
  const search = useSearchArticles({ q }, { query: { enabled: q.length > 0 && target === null } });
  const hits = (search.data?.status === 200 ? search.data.data : []).filter((h) => h.articleId !== articleId);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    await create.mutateAsync({
      articleId,
      data: {
        kind,
        targetArticleId: target?.articleId ?? null,
        externalLabel: target ? null : emptyToNull(externalLabel),
        note: emptyToNull(note),
      },
    });
    await refresh();
    toast.success("Bağlantı eklendi.");
    onDone();
  }

  return (
    <form onSubmit={(e) => submit(e).catch(() => {})} className="space-y-3 rounded-md border bg-muted/30 p-3">
      <div className="grid gap-3 sm:grid-cols-[16rem_1fr]">
        <Field label="Tür" htmlFor="link-kind">
          <OptionSelect id="link-kind" value={kind} options={kindLabels} onChange={setKind} />
        </Field>
        <div>
          <p className="mb-1.5 text-sm font-medium">Hedef madde</p>
          {target ? (
            <p className="flex items-center gap-2 rounded-md border bg-background px-3 py-1.5">
              <span className="flex-1">
                {target.lawLabel} Madde {target.number} — {target.title}
              </span>
              <Button type="button" variant="ghost" size="icon-sm" aria-label="Seçimi kaldır" onClick={() => setTarget(null)}>
                <X />
              </Button>
            </p>
          ) : (
            <>
              <Input
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="tck 82, 765 448 ya da başlıktan bir ifade…"
                aria-label="Hedef madde ara"
                autoFocus
              />
              {hits.length > 0 && (
                <ul className="mt-1 max-h-56 overflow-y-auto rounded-md border bg-background">
                  {hits.map((hit) => (
                    <li key={hit.articleId}>
                      <button
                        type="button"
                        onClick={() => setTarget(hit)}
                        className="w-full px-3 py-1.5 text-left text-sm hover:bg-accent"
                      >
                        <span className="font-medium">
                          {hit.lawLabel} Madde {hit.number}
                        </span>{" "}
                        {hit.title}
                      </button>
                    </li>
                  ))}
                </ul>
              )}
              {q.length > 0 && search.data && hits.length === 0 && (
                <p className="mt-1 text-xs text-muted-foreground">Kütüphanede bulunamadı; aşağıya adını yazabilirsiniz.</p>
              )}
            </>
          )}
          {fieldMessages(create.error, "targetArticleId").map((m) => (
            <p key={m} className="mt-1 text-xs text-destructive">
              {m}
            </p>
          ))}
        </div>
      </div>
      {!target && (
        <Field
          label="Kütüphanede yoksa maddenin adı"
          htmlFor="link-external"
          error={create.error}
          field="externalLabel"
          hint="Ör. 765 s. TCK m. 448 — kanun sonradan girilince bağlantı maddeye çevrilebilir."
        >
          <Input id="link-external" value={externalLabel} onChange={(e) => setExternalLabel(e.target.value)} />
        </Field>
      )}
      <Field label="Not (isteğe bağlı)" htmlFor="link-note" error={create.error} field="note">
        <Input id="link-note" value={note} onChange={(e) => setNote(e.target.value)} placeholder="kısmen karşılar" />
      </Field>
      <FormMessages messages={otherMessages(create.error, ["targetArticleId", "externalLabel", "note"])} />
      <div className="flex gap-2">
        <Button type="submit" disabled={create.isPending}>
          Bağlantıyı ekle
        </Button>
        <Button type="button" variant="ghost" onClick={onDone}>
          Vazgeç
        </Button>
      </div>
    </form>
  );
}
