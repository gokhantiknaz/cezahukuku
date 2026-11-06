"use client";

import { useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { ExternalLink, History, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import {
  useCreateArticle,
  useDeleteArticle,
  useGetArticleVersion,
  useUpdateArticle,
} from "@/api/generated/admin-laws/admin-laws";
import { useGetArticle } from "@/api/generated/library/library";
import type { ArticleSummary, LawNodeTree, Visibility } from "@/api/generated/model";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { formatDate } from "@/lib/dates";
import { cn } from "@/lib/utils";
import { ConfirmDelete } from "./confirm-delete";
import { emptyToNull, Field, FormMessages, OptionSelect, otherMessages, toSortOrder, visibilityLabels } from "./form";
import { NodeSelect } from "./node-editor";
import { ArticleDecisions } from "./article-decisions";
import { ArticleLinks } from "./article-links";
import { NotesEditor } from "./notes-editor";
import { useLibraryRefresh } from "./use-library-refresh";
import { VersionEditor } from "./version-editor";

const articleFields = ["number", "title", "nodeId"];

export function ArticleEditor({
  lawId,
  lawSlug,
  nodes,
  article,
  nodeId,
  onSaved,
  onDeleted,
}: {
  lawId: string;
  lawSlug: string;
  nodes: LawNodeTree[];
  /** Düzenlenen madde; yoksa yeni madde. */
  article?: ArticleSummary;
  /** Yeni maddenin bağlanacağı düğüm. */
  nodeId?: string | null;
  onSaved: (articleId: string) => void;
  onDeleted: () => void;
}) {
  const refresh = useLibraryRefresh();
  const [form, setForm] = useState({
    nodeId: article ? article.nodeId : (nodeId ?? null),
    number: article?.number ?? "",
    title: article?.title ?? "",
    visibility: article?.visibility ?? ("public" as Visibility),
    sortOrder: article ? String(article.sortOrder) : "",
  });
  const create = useCreateArticle();
  const update = useUpdateArticle();
  const remove = useDeleteArticle();
  const error = create.error ?? update.error;

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const data = { ...form, title: emptyToNull(form.title), sortOrder: toSortOrder(form.sortOrder) };
    const saved = article
      ? await update.mutateAsync({ id: article.id, data })
      : await create.mutateAsync({ lawId, data });
    await refresh();
    toast.success(article ? "Madde güncellendi." : "Madde eklendi; şimdi metnini girin.");
    if (saved.status === 200 || saved.status === 201) onSaved(saved.data.id);
  }

  return (
    <div className="space-y-6">
      <form onSubmit={(e) => submit(e).catch(() => {})} className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-xl">{article ? `Madde ${article.number} — ${article.title}` : "Yeni madde"}</h2>
          {article && (
            <Button variant="ghost" size="sm" asChild>
              <Link href={`/${lawSlug}/${article.slug}`} target="_blank">
                <ExternalLink /> Okuma ekranında aç
              </Link>
            </Button>
          )}
        </div>
        <div className="grid gap-4 sm:grid-cols-[8rem_1fr]">
          <Field label="Numara" htmlFor="article-number" error={error} field="number">
            <Input
              id="article-number"
              value={form.number}
              onChange={(e) => setForm((f) => ({ ...f, number: e.target.value }))}
              placeholder="81, 103/A, Geçici 2"
              required
            />
          </Field>
          <Field label="Başlık" htmlFor="article-title" error={error} field="title">
            <Input
              id="article-title"
              value={form.title}
              onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
              placeholder="Kasten öldürme"
            />
          </Field>
        </div>
        <Field label="Bulunduğu yer" htmlFor="article-node" error={error} field="nodeId">
          <NodeSelect
            id="article-node"
            nodes={nodes}
            value={form.nodeId}
            onChange={(node) => setForm((f) => ({ ...f, nodeId: node }))}
          />
        </Field>
        <div className="grid gap-4 sm:grid-cols-[1fr_8rem]">
          <Field label="Görünürlük" htmlFor="article-visibility">
            <OptionSelect
              id="article-visibility"
              value={form.visibility}
              options={visibilityLabels}
              onChange={(visibility) => setForm((f) => ({ ...f, visibility }))}
            />
          </Field>
          <Field label="Sıra" htmlFor="article-order" hint="Boşsa sona.">
            <Input
              id="article-order"
              type="number"
              value={form.sortOrder}
              onChange={(e) => setForm((f) => ({ ...f, sortOrder: e.target.value }))}
            />
          </Field>
        </div>
        <FormMessages messages={otherMessages(error, articleFields)} />
        <div className="flex justify-between gap-2">
          <Button type="submit" disabled={create.isPending || update.isPending}>
            {article ? "Kaydet" : "Maddeyi ekle"}
          </Button>
          {article && (
            <ConfirmDelete
              title={`Madde ${article.number} silinsin mi?`}
              description="Madde ve metni okuyuculardan kaldırılır. Kayıtlar veritabanında saklanır."
              onConfirm={async () => {
                await remove.mutateAsync({ id: article.id });
                await refresh();
                toast.success("Madde silindi.");
                onDeleted();
              }}
            >
              <Button type="button" variant="ghost">
                <Trash2 /> Sil
              </Button>
            </ConfirmDelete>
          )}
        </div>
      </form>

      {article && <ArticleVersions key={article.id} lawSlug={lawSlug} article={article} />}
      {article && <ArticleNotes lawSlug={lawSlug} article={article} />}
    </div>
  );
}

/** Maddenin notları ve kararları (madde ayrıntısıyla aynı sorgudan). */
function ArticleNotes({ lawSlug, article }: { lawSlug: string; article: ArticleSummary }) {
  const detailQuery = useGetArticle(lawSlug, article.slug);
  if (detailQuery.data?.status !== 200) return null;
  return (
    <>
      <div className="border-t pt-5">
        <NotesEditor target={{ level: "article", id: article.id }} notes={detailQuery.data.data.notes} />
      </div>
      <div className="border-t pt-5">
        <ArticleDecisions articleId={article.id} decisions={detailQuery.data.data.decisions} />
      </div>
      <div className="border-t pt-5">
        <ArticleLinks articleId={article.id} links={detailQuery.data.data.links} />
      </div>
    </>
  );
}

/** Sürümlerin nasıl çalıştığı: panelde açılıp kapanan kısa açıklama. */
function VersionGuide() {
  return (
    <details className="rounded-md border bg-muted/30 px-3 py-2 text-sm">
      <summary className="cursor-pointer font-medium">Sürümler nasıl çalışır?</summary>
      <ul className="mt-2 list-disc space-y-1.5 pl-5 text-muted-foreground">
        <li>
          <strong className="text-foreground">Değişiklik gir:</strong> kanun maddeyi değiştirdiğinde. Yürürlükteki metin
          kopyalanır; yalnız değişen kelimeleri düzeltir, kaldırılan fıkra ya da bendi silersiniz. Kaydedince yeni metin
          yeni bir sürüm olur, eski metin silinmez: yürürlük başlangıcından bir gün önce kapanır. Okuma ekranında değişen
          kelimeler kırmızı, kaldırılan fıkra/bent “Mülga” görünür; tıklayınca Değişiklikler sekmesinde önceki hâli açılır.
          Suç tarihi seçen okuyucu o tarihteki metni görür.
        </li>
        <li>
          <strong className="text-foreground">Düzelt:</strong> yazım hatası, yanlış tarih gibi düzeltmeler için bir sürümü
          seçin. Sürüm yerinde değişir; Değişiklikler sekmesine yeni kayıt düşmez.
        </li>
        <li>
          <strong className="text-foreground">Önceki metni ekle:</strong> madde değişmiş hâliyle girildiyse (ör. içe
          aktarılan güncel metin), değişiklikten önceki metni sonradan eklemek için.
        </li>
        <li>
          Madde tümüyle yürürlükten kalktıysa yeni sürümde <strong className="text-foreground">Mülga</strong> kutusunu
          işaretleyin.
        </li>
      </ul>
    </details>
  );
}

/**
 * Maddenin metin sürümleri. Ne yapılacağı açıkça seçilir: değişiklik gir (yeni sürüm), önceki metni ekle ya da bir
 * sürümü düzelt; seçim yapılana kadar düzenleyici açılmaz (yürürlükteki metni farkında olmadan yerinde değiştirmemek
 * için). Metni olmayan maddede doğrudan ilk metin yazılır. Okuma ekranından ?surum=onceki ile önceki metin açılır.
 */
function ArticleVersions({ lawSlug, article }: { lawSlug: string; article: ArticleSummary }) {
  const searchParams = useSearchParams();
  const [selected, setSelected] = useState<string | null>(() =>
    searchParams.get("surum") === "onceki" && searchParams.get("madde") === article.slug ? "older" : null,
  );
  const detailQuery = useGetArticle(lawSlug, article.slug);
  const detail = detailQuery.data?.status === 200 ? detailQuery.data.data : undefined;
  const versions = detail?.versions ?? [];
  const oldest = versions.at(-1);

  const current = versions.length === 0 ? "new" : selected === "older" && !oldest?.validFrom ? null : selected;
  // Değişiklik yürürlükteki metinden, önceki metin ilk kayıtlı metinden başlar; fıkra kimlikleri oradan taşınır.
  const baseId =
    current === "new"
      ? (detail?.version?.id ?? versions[0]?.id ?? null)
      : current === "older"
        ? (oldest?.id ?? null)
        : current;
  const baseQuery = useGetArticleVersion(baseId ?? "", { query: { enabled: baseId !== null } });
  const base = baseQuery.data?.status === 200 ? baseQuery.data.data : undefined;

  if (!detail || (baseId !== null && !base)) {
    return <p className="text-muted-foreground">Metin yükleniyor…</p>;
  }

  const banner =
    versions.length === 0
      ? null
      : current === "new"
        ? "Değişiklik giriyorsunuz. Metin yürürlükteki sürümden kopyalandı: değişen kelimeleri düzeltin, kaldırılan fıkra ya da bendi silin; yürürlük başlangıcını ve değiştiren düzenlemeyi girin. Kaydedince önceki metin Değişiklikler sekmesinde kalır."
        : current === "older"
          ? "Değişiklikten önceki metni giriyorsunuz. Metin ilk kayıtlı sürümden kopyalandı: değişiklikten önceki hâline getirin ve yürürlük başlangıcını girin."
          : current
            ? "Seçili sürümü düzeltiyorsunuz: kaydedince bu sürümün metni yerinde değişir, Değişiklikler sekmesine yeni kayıt düşmez. Kanun maddeyi değiştirdiyse bunun yerine “Değişiklik gir”i kullanın."
            : null;

  return (
    <section className="space-y-4 border-t pt-5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-lg">Madde metni ve sürümleri</h3>
        {versions.length > 0 && (
          <div className="flex flex-wrap gap-2">
            <Button variant={current === "new" ? "default" : "outline"} size="sm" onClick={() => setSelected("new")}>
              <Plus /> Değişiklik gir
            </Button>
            {oldest?.validFrom && (
              <Button
                variant={current === "older" ? "default" : "outline"}
                size="sm"
                onClick={() => setSelected("older")}
              >
                <History /> Önceki metni ekle
              </Button>
            )}
          </div>
        )}
      </div>
      {versions.length > 0 && <VersionGuide />}

      {versions.length > 0 && (
        <div className="space-y-1.5">
          <p className="text-sm text-muted-foreground">Kayıtlı sürümler — düzeltmek için seçin:</p>
          <ul className="flex flex-wrap gap-2">
          {versions.map((v) => (
            <li key={v.id}>
              <button
                type="button"
                onClick={() => setSelected(v.id)}
                className={cn(
                  "rounded-md border px-3 py-1.5 text-left text-sm",
                  v.id === current ? "border-primary bg-accent text-accent-foreground" : "hover:bg-accent",
                )}
              >
                {v.validFrom ? formatDate(v.validFrom) : "…"} – {v.validTo ? formatDate(v.validTo) : "…"}
                {v.validTo === null && (
                  <Badge variant="secondary" className="ml-2">
                    yürürlükte
                  </Badge>
                )}
                {v.isRepealed && (
                  <Badge variant="outline" className="ml-2">
                    mülga
                  </Badge>
                )}
              </button>
            </li>
          ))}
          </ul>
        </div>
      )}

      {banner && (
        <p className="rounded-md border-l-4 border-primary bg-accent px-3 py-2 text-sm text-accent-foreground">{banner}</p>
      )}

      {current && (
        <VersionEditor
          key={`${current}:${baseId}`}
          articleId={article.id}
          mode={current === "new" || current === "older" ? current : "edit"}
          base={base}
          // Değişiklik ya da önceki metin kaydedilince seçim ekranına dönülür; düzeltmede sürüm açık kalır.
          onSaved={(id) => setSelected(current === "new" || current === "older" ? null : id)}
          onDeleted={() => setSelected(null)}
        />
      )}
    </section>
  );
}
