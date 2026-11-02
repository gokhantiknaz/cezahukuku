"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { CircleAlert, LogIn, NotebookPen } from "lucide-react";
import { useGetNotebook } from "@/api/generated/personal/personal";
import type { HighlightColor, NotebookArticleDto } from "@/api/generated/model";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useHydrated } from "@/hooks/use-hydrated";
import { useCurrentUser } from "@/lib/auth";
import { canAnnotate, highlightClassName, highlightColors } from "@/lib/highlights";
import { matchesTr } from "@/lib/turkish";
import { cn } from "@/lib/utils";

type Sort = "library" | "recent";

const formatDate = (iso: string) => new Date(iso).toLocaleDateString("tr-TR");

/**
 * Bütün maddelerdeki vurgu ve notlarım: madde başına bir kart, kartta vurgular metin sırasıyla (notu altında) ve
 * maddenin geneline yazılan notlar. Süzgeçler istemcide: metin (alıntı, not, madde adı), renk, yalnız notlular,
 * kanun. Vurguya tıklayınca madde açılır ve o vurguya kaydırılır (#vurgu-<id>).
 */
export function Notebook() {
  const hydrated = useHydrated();
  const { data: user, isPending } = useCurrentUser();
  const enabled = hydrated && canAnnotate(user);
  const { data } = useGetNotebook({ query: { enabled } });
  const notebook = data?.status === 200 ? data.data : null;

  const [query, setQuery] = useState("");
  const [colors, setColors] = useState<HighlightColor[]>([]);
  const [notesOnly, setNotesOnly] = useState(false);
  const [law, setLaw] = useState("");
  const [sort, setSort] = useState<Sort>("library");

  const laws = useMemo(
    () => [...new Map((notebook ?? []).map(({ article }) => [article.lawSlug, article.lawLabel])).entries()],
    [notebook],
  );
  const filtered = useMemo(() => {
    if (!notebook) return [];
    const result = notebook
      .filter(({ article }) => !law || article.lawSlug === law)
      .map((entry) => filterEntry(entry, query, colors, notesOnly))
      .filter((entry) => entry.highlights.length + entry.notes.length > 0);
    return sort === "recent" ? result.sort((a, b) => b.lastChangedAt.localeCompare(a.lastChangedAt)) : result;
  }, [notebook, law, query, colors, notesOnly, sort]);

  if (!hydrated || isPending) return null;
  if (!user)
    return (
      <p className="mt-6">
        <Button asChild>
          <Link href="/giris?next=/notlarim">
            <LogIn /> Notlarınız için giriş yapın
          </Link>
        </Button>
      </p>
    );
  if (!enabled) return <p className="mt-6 text-muted-foreground">Hesabınız yalnız okuma yetkisine sahip; vurgu ve not ekleyemezsiniz.</p>;
  if (!notebook) return <p className="mt-6 text-muted-foreground">Yükleniyor…</p>;
  if (notebook.length === 0)
    return (
      <p className="mt-6 text-muted-foreground">
        Henüz vurgunuz ya da notunuz yok. Madde metninde bir yeri seçip vurgulayabilir, seçili yere ya da maddenin geneline
        not yazabilirsiniz.
      </p>
    );

  const highlightCount = filtered.reduce((sum, e) => sum + e.highlights.length, 0);
  const noteCount = filtered.reduce((sum, e) => sum + e.notes.length + e.highlights.filter((h) => h.note).length, 0);
  const toggleColor = (color: HighlightColor) =>
    setColors((current) => (current.includes(color) ? current.filter((c) => c !== color) : [...current, color]));

  return (
    <>
      <div className="mt-4 space-y-3 rounded-lg border bg-card p-3">
        <Input
          type="search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Vurgu, not ya da madde ara…"
          aria-label="Vurgu ve notlarda ara"
        />
        <div className="flex flex-wrap items-center gap-2">
          {highlightColors.map((color) => {
            const selected = colors.includes(color.key);
            return (
              <button
                key={color.key}
                type="button"
                aria-pressed={selected}
                onClick={() => toggleColor(color.key)}
                className={cn(
                  "rounded px-2.5 py-1 text-sm ring-offset-1",
                  color.className,
                  selected ? "ring-2 ring-primary" : "opacity-80 hover:opacity-100",
                )}
              >
                {color.label}
              </button>
            );
          })}
          <Button
            size="sm"
            variant={notesOnly ? "default" : "outline"}
            aria-pressed={notesOnly}
            onClick={() => setNotesOnly((v) => !v)}
          >
            <NotebookPen /> Yalnız notlar
          </Button>
          <div className="flex w-full gap-2 sm:ml-auto sm:w-auto">
            {laws.length > 1 && (
              <select
                value={law}
                onChange={(e) => setLaw(e.target.value)}
                aria-label="Kanun"
                className="h-8 min-w-0 flex-1 rounded-md border bg-card px-2 text-sm sm:flex-none"
              >
                <option value="">Bütün kanunlar</option>
                {laws.map(([slug, label]) => (
                  <option key={slug} value={slug}>
                    {label}
                  </option>
                ))}
              </select>
            )}
            <select
              value={sort}
              onChange={(e) => setSort(e.target.value as Sort)}
              aria-label="Sıralama"
              className="h-8 min-w-0 flex-1 rounded-md border bg-card px-2 text-sm sm:flex-none"
            >
              <option value="library">Kanun sırasıyla</option>
              <option value="recent">Son değişen önce</option>
            </select>
          </div>
        </div>
      </div>

      <p className="mt-3 text-sm text-muted-foreground" aria-live="polite">
        {filtered.length === 0
          ? "Süzgece uyan vurgu ya da not yok."
          : `${filtered.length} madde · ${highlightCount} vurgu · ${noteCount} not`}
      </p>

      <ul className="mt-2 space-y-3">
        {filtered.map((entry) => (
          <ArticleCard key={entry.article.articleId} entry={entry} />
        ))}
      </ul>
    </>
  );
}

/** Süzgeçlere uyan vurgu ve notlar; madde adı aranan ifadeye uyuyorsa maddedeki her şey kalır. */
function filterEntry(entry: NotebookArticleDto, query: string, colors: HighlightColor[], notesOnly: boolean): NotebookArticleDto {
  const { article } = entry;
  const articleMatches = matchesTr(`${article.lawLabel} ${article.number} ${article.title} ${article.lawName}`, query);
  const highlights = entry.highlights.filter(
    (h) =>
      (colors.length === 0 || colors.includes(h.color)) &&
      (!notesOnly || h.note) &&
      (articleMatches || matchesTr(h.quote, query) || (h.note && matchesTr(h.note.text, query))),
  );
  // Maddenin geneline yazılan notun rengi yok: renk süzgeci seçiliyken gösterilmez.
  const notes = colors.length > 0 ? [] : entry.notes.filter((n) => articleMatches || matchesTr(n.text, query));
  return { ...entry, highlights, notes };
}

function ArticleCard({ entry }: { entry: NotebookArticleDto }) {
  const { article } = entry;
  const href = `/${article.lawSlug}/${article.articleSlug}`;
  return (
    <li className="rounded-lg border bg-card">
      <div className="flex items-baseline gap-2 border-b px-4 py-2.5">
        <Link href={href} className="min-w-0 flex-1 hover:underline">
          <span className="font-medium text-primary">
            {article.lawLabel} Madde {article.number}
          </span>
          {article.title && <span> — {article.title}</span>}
        </Link>
        <span className="shrink-0 text-xs text-muted-foreground">{formatDate(entry.lastChangedAt)}</span>
      </div>
      <ul className="divide-y">
        {entry.highlights.map((highlight) => (
          <li key={highlight.id}>
            <Link href={`${href}#vurgu-${highlight.id}`} className="block px-4 py-3 hover:bg-accent/50">
              <span className={cn("rounded-[2px] box-decoration-clone px-0.5 leading-relaxed", highlightClassName(highlight.color))}>
                {highlight.quote}
              </span>
              {highlight.isStale && (
                <Badge variant="outline" className="ml-2 align-middle text-muted-foreground">
                  <CircleAlert /> güncel metinde yok
                </Badge>
              )}
              {highlight.note && (
                <span className="mt-1.5 flex gap-2 text-sm text-muted-foreground">
                  <NotebookPen className="mt-0.5 size-4 shrink-0" aria-label="Not" />
                  <span className="whitespace-pre-line">{highlight.note.text}</span>
                </span>
              )}
            </Link>
          </li>
        ))}
        {entry.notes.map((note) => (
          <li key={note.id}>
            <Link href={href} className="flex gap-2 px-4 py-3 text-sm hover:bg-accent/50">
              <NotebookPen className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-label="Madde notu" />
              <span className="whitespace-pre-line">{note.text}</span>
            </Link>
          </li>
        ))}
      </ul>
    </li>
  );
}
