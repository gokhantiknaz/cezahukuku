import { Suspense } from "react";
import type { Metadata } from "next";
import Form from "next/form";
import Link from "next/link";
import { ArrowRight, ChevronLeft, ChevronRight, Search } from "lucide-react";
import { KindBadge } from "@/components/search/kind-badge";
import { Snippet } from "@/components/search/snippet";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { formatDate } from "@/lib/dates";
import { isSearchKind, searchKindLabels, searchKindOrder } from "@/lib/search";
import { getLibrary, searchContent } from "@/lib/server-api";
import { cn } from "@/lib/utils";

export const metadata: Metadata = { title: "Arama", robots: { index: false } };

const PAGE_SIZE = 20;

const first = (value: string | string[] | undefined) => (Array.isArray(value) ? value[0] : value);

/** Detaylı arama: /ara?q=…&tur=gerekce&kanun=tck&sayfa=2 */
export default function SearchPage({ searchParams }: PageProps<"/ara">) {
  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-6">
      <h1 className="text-3xl">Arama</h1>
      <Suspense fallback={<p className="mt-6 text-muted-foreground">Aranıyor…</p>}>
        <SearchContent searchParams={searchParams} />
      </Suspense>
    </div>
  );
}

async function SearchContent({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const params = await searchParams;
  const q = (first(params.q) ?? "").trim();
  const tur = first(params.tur);
  const kind = isSearchKind(tur) ? tur : undefined;
  const lawSlug = first(params.kanun) || undefined;
  const page = Math.max(1, Number.parseInt(first(params.sayfa) ?? "1", 10) || 1);

  const [library, result] = await Promise.all([
    getLibrary(),
    q
      ? searchContent({
          q,
          kinds: kind ? [kind] : undefined,
          laws: lawSlug ? [lawSlug] : undefined,
          offset: (page - 1) * PAGE_SIZE,
          limit: PAGE_SIZE,
        })
      : null,
  ]);
  const laws = library.groups.flatMap((g) => g.laws);

  // Filtre bağlantıları mevcut aramayı korur; tür değişince ilk sayfaya döner.
  const href = (changes: Record<string, string | undefined>) => {
    const next = new URLSearchParams();
    const values = { q, tur: kind, kanun: lawSlug, ...changes };
    for (const [key, value] of Object.entries(values)) if (value) next.set(key, value);
    return `/ara?${next}`;
  };
  const allCount = result?.counts.reduce((sum, c) => sum + c.count, 0) ?? 0;
  const pages = result ? Math.ceil(result.total / PAGE_SIZE) : 0;

  return (
    <>
      <Form key={`${q}|${lawSlug}`} action="/ara" className="mt-4 flex flex-wrap gap-2" role="search">
        <label className="relative w-full sm:w-auto sm:min-w-60 sm:flex-1">
          <span className="sr-only">Aranacak ifade</span>
          <Search className="absolute top-1/2 left-3 size-5 -translate-y-1/2 text-muted-foreground" aria-hidden />
          <Input
            name="q"
            type="search"
            defaultValue={q}
            autoFocus={!q}
            placeholder="Madde, konu, karar… (örn. kasten öldürme, tck 81)"
            className="h-11 bg-card pl-10 text-base"
          />
        </label>
        <select
          name="kanun"
          defaultValue={lawSlug ?? ""}
          aria-label="Kanun"
          // Uzun kanun adları kutuyu ekrandan taşırmasın.
          className="h-11 min-w-0 flex-1 rounded-md border bg-card px-3 text-base sm:max-w-xs sm:flex-none"
        >
          <option value="">Bütün kanunlar</option>
          {laws.map((law) => (
            <option key={law.id} value={law.slug}>
              {law.shortName || law.number} — {law.name}
            </option>
          ))}
        </select>
        <Button type="submit" size="lg" className="h-11">
          Ara
        </Button>
      </Form>

      {!q && <SearchHelp />}

      {result?.jump && (
        <Link
          href={result.jump.url}
          className="mt-5 flex items-center justify-between gap-3 rounded-lg border border-primary/40 bg-accent px-4 py-3 text-accent-foreground hover:bg-accent/70"
        >
          <span>
            <span className="block text-sm">Maddeye git</span>
            <span className="text-lg font-medium">{result.jump.label}</span>
          </span>
          <ArrowRight aria-hidden />
        </Link>
      )}

      {result && (
        <>
          {allCount > 0 && (
            <nav aria-label="Sonuç türü" className="mt-5 flex flex-wrap gap-2">
              <FilterChip href={href({ tur: undefined, sayfa: undefined })} active={!kind} label="Tümü" count={allCount} />
              {searchKindOrder
                .map((k) => ({ kind: k, count: result.counts.find((c) => c.kind === k)?.count ?? 0 }))
                .filter((c) => c.count > 0 || c.kind === kind)
                .map((c) => (
                  <FilterChip
                    key={c.kind}
                    href={href({ tur: c.kind, sayfa: undefined })}
                    active={kind === c.kind}
                    label={searchKindLabels[c.kind]}
                    count={c.count}
                  />
                ))}
            </nav>
          )}

          {result.hits.length === 0 ? (
            !result.jump && (
              <div className="mt-6 rounded-lg border bg-card p-4">
                <p className="font-medium">“{q}” için sonuç bulunamadı.</p>
                <p className="mt-1 text-sm text-muted-foreground">
                  Daha az sözcükle ya da kanun filtresini kaldırarak deneyin. Türkçe karakter kullanmadan da
                  yazabilirsiniz (oldurme → öldürme).
                </p>
              </div>
            )
          ) : (
            <>
              <p className="mt-4 text-sm text-muted-foreground">{result.total} sonuç</p>
              <ol className="mt-2 space-y-3">
                {result.hits.map((hit, i) => (
                  <li key={`${hit.url}-${i}`} className="rounded-lg border bg-card p-4">
                    <div className="flex flex-wrap items-center gap-2 text-sm">
                      <KindBadge kind={hit.kind} />
                      {hit.context && <span className="text-muted-foreground">{hit.context}</span>}
                      {hit.date && <span className="text-muted-foreground">· {formatDate(hit.date)}</span>}
                    </div>
                    <Link href={hit.url} className="mt-1 block text-lg font-medium text-primary hover:underline">
                      {hit.title}
                    </Link>
                    <Snippet text={hit.snippet} className="reading mt-1 text-[15px] leading-relaxed" />
                  </li>
                ))}
              </ol>
              {pages > 1 && (
                <nav className="mt-5 flex items-center justify-between" aria-label="Sayfalar">
                  {page > 1 ? (
                    <Button variant="outline" asChild>
                      <Link href={href({ sayfa: String(page - 1) })}>
                        <ChevronLeft /> Önceki
                      </Link>
                    </Button>
                  ) : (
                    <span />
                  )}
                  <span className="text-sm text-muted-foreground">
                    Sayfa {page} / {pages}
                  </span>
                  {page < pages ? (
                    <Button variant="outline" asChild>
                      <Link href={href({ sayfa: String(page + 1) })}>
                        Sonraki <ChevronRight />
                      </Link>
                    </Button>
                  ) : (
                    <span />
                  )}
                </nav>
              )}
            </>
          )}
        </>
      )}
    </>
  );
}

function FilterChip({ href, active, label, count }: { href: string; active: boolean; label: string; count: number }) {
  return (
    <Link
      href={href}
      aria-current={active ? "true" : undefined}
      className={cn(
        "rounded-full border px-3 py-1 text-sm",
        active ? "border-primary bg-primary text-primary-foreground" : "bg-card hover:bg-accent",
      )}
    >
      {label} <span className={active ? "opacity-80" : "text-muted-foreground"}>({count})</span>
    </Link>
  );
}

function SearchHelp() {
  const examples = [
    ["kasten öldürme", "iki sözcüğün ikisi de geçen içerik"],
    ["\"hapis cezası\"", "tam ifade, sözcükler yan yana"],
    ["hırsızlık VEYA yağma", "sözcüklerden biri"],
    ["öldürme -taksir", "\"taksir\" geçmeyenler (DEĞİL de yazılabilir)"],
    ["(müebbet VE ihmal) -taksir", "parantezle gruplama"],
    ["tck 81 · 5237/81 · cmk geçici 1", "doğrudan maddeye git"],
  ];
  return (
    <section className="mt-6 rounded-lg border bg-card p-4">
      <h2 className="font-medium">Nasıl aranır?</h2>
      <p className="mt-1 text-sm text-muted-foreground">
        Sözcüğün ekli biçimleri de bulunur (öldürmenin, öldüren → öldürme); Türkçe karakter kullanmadan da
        yazabilirsiniz. Hızlı arama için her sayfada <kbd className="rounded border px-1">Ctrl</kbd>+
        <kbd className="rounded border px-1">K</kbd>.
      </p>
      <dl className="mt-3 grid gap-x-4 gap-y-1.5 text-sm sm:grid-cols-[auto_1fr]">
        {examples.map(([query, meaning]) => (
          <div key={query} className="contents">
            <dt>
              <Link href={`/ara?q=${encodeURIComponent(query.split(" · ")[0])}`} className="font-mono text-primary hover:underline">
                {query}
              </Link>
            </dt>
            <dd className="text-muted-foreground">{meaning}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}
