"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams, usePathname, useRouter } from "next/navigation";
import {
  ArrowLeft,
  BookOpen,
  ChevronDown,
  ChevronRight,
  FileText,
  Gavel,
  Lock,
  Scale,
  Search,
  type LucideIcon,
} from "lucide-react";
import type {
  ArticleSummary,
  EditorialNoteKind,
  LawGroupWithLaws,
  LawNodeTree,
  LawTreeResponse,
} from "@/api/generated/model";
import { Button } from "@/components/ui/button";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible";
import { Input } from "@/components/ui/input";
import { ScrollArea } from "@/components/ui/scroll-area";
import { matchesTr } from "@/lib/turkish";
import { cn } from "@/lib/utils";

// Bilinen grupların simgesi; yönetim panelinden eklenen yeni gruplar varsayılan simgeyle görünür.
const groupIcons: Record<string, LucideIcon> = {
  temel: Scale,
  ozel: FileText,
  kabahatler: Gavel,
};

type LibraryPanelProps = {
  groups: LawGroupWithLaws[];
  /** Açık kanunun ağacı; null ise kütüphane listesi gösterilir. */
  tree: LawTreeResponse | null;
};

/**
 * Sol panel: kütüphane (kanun grupları) ya da açık kanunun kitap/bölüm/madde ağacı. "← Kütüphane" yalnız paneli
 * kütüphaneye çevirir; okunan madde ortada açık kalır.
 */
export function LibraryPanel({ groups, tree }: LibraryPanelProps) {
  const [showLibrary, setShowLibrary] = useState(false);
  // Başka bir kanuna geçilince panel yeniden o kanunun ağacını göstersin.
  const [lastLawId, setLastLawId] = useState(tree?.law.id);
  if (tree?.law.id !== lastLawId) {
    setLastLawId(tree?.law.id);
    setShowLibrary(false);
  }

  return tree && !showLibrary ? (
    <LawTreeView key={tree.law.id} tree={tree} onBackToLibrary={() => setShowLibrary(true)} />
  ) : (
    <LibraryList groups={groups} />
  );
}

function PanelHeader({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex items-center gap-2 bg-primary px-3 py-2.5 text-lg text-primary-foreground">{children}</div>
  );
}

/** Ağaç yüklenirken gösterilen iskelet (sunucudan akarken düzen kaymasın). */
export function LibraryPanelSkeleton() {
  return (
    <div className="flex h-full flex-col">
      <PanelHeader>
        <BookOpen className="size-5" aria-hidden />
        Kütüphane
      </PanelHeader>
      <div className="space-y-3 p-4" aria-busy="true" aria-label="Yükleniyor">
        {[70, 55, 80, 60, 45].map((width) => (
          <div key={width} className="h-4 animate-pulse rounded bg-muted" style={{ width: `${width}%` }} />
        ))}
      </div>
    </div>
  );
}

function PrivateMark() {
  return <Lock className="size-3.5 shrink-0 opacity-70" aria-label="Yalnız giriş yapanlar görür" />;
}

function LibraryList({ groups }: { groups: LawGroupWithLaws[] }) {
  const visibleGroups = groups.filter((group) => group.laws.length > 0);

  return (
    <div className="flex h-full flex-col">
      <PanelHeader>
        <BookOpen className="size-5" aria-hidden />
        Kütüphane
      </PanelHeader>
      <ScrollArea className="min-h-0 flex-1">
        {visibleGroups.length === 0 && (
          <p className="px-4 py-4 text-sm text-muted-foreground">Kütüphanede henüz kanun yok.</p>
        )}
        <ul className="divide-y px-2">
          {visibleGroups.map((group) => {
            const Icon = groupIcons[group.slug] ?? BookOpen;
            return (
              <li key={group.id} className="py-3">
                <div className="flex items-center gap-2 px-2 text-primary">
                  <Icon className="size-5 shrink-0" aria-hidden />
                  <span className="text-base font-medium">{group.title}</span>
                </div>
                <ul className="mt-1.5">
                  {group.laws.map((law) => (
                    <li key={law.id}>
                      <Link
                        href={`/${law.slug}`}
                        className="flex w-full items-start gap-2 rounded-md px-2 py-1.5 pl-9 text-left hover:bg-accent hover:text-accent-foreground"
                      >
                        <span>
                          {law.name} <span className="text-muted-foreground">({law.number})</span>
                        </span>
                        {law.visibility === "private" && <PrivateMark />}
                      </Link>
                    </li>
                  ))}
                </ul>
              </li>
            );
          })}
        </ul>
      </ScrollArea>
    </div>
  );
}

function LawTreeView({ tree, onBackToLibrary }: { tree: LawTreeResponse; onBackToLibrary: () => void }) {
  const [query, setQuery] = useState("");
  const params = useParams<{ article?: string }>();
  const router = useRouter();
  const selectedSlug = params.article ?? null;
  const filtering = query.trim().length > 0;

  /** Enter (telefonda "Git"): numarası tutan maddeye, yoksa ilk eşleşene gider. */
  function jump(event: React.FormEvent) {
    event.preventDefault();
    const { exact, results } = filterArticles(tree, query);
    const target = exact ?? results[0];
    if (target) router.push(`/${tree.law.slug}/${target.slug}`);
  }
  const { law } = tree;
  const isEmpty = tree.nodes.length === 0 && tree.articles.length === 0;

  return (
    <div className="flex h-full flex-col">
      <PanelHeader>
        <Button
          variant="ghost"
          size="icon-sm"
          onClick={onBackToLibrary}
          className="text-primary-foreground hover:bg-white/15 hover:text-primary-foreground"
          aria-label="Kütüphaneye dön"
          title="Kütüphaneye dön"
        >
          <ArrowLeft />
        </Button>
        <Link href={`/${law.slug}`} className="truncate hover:underline">
          {law.shortName || law.name}
        </Link>
      </PanelHeader>

      <div className="space-y-2 border-b p-2">
        <p className="px-1 text-sm text-muted-foreground">
          {law.name} ({law.number})
        </p>
        <form onSubmit={jump} role="search">
          <label className="relative block">
            <Search className="absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
            <Input
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Madde no veya başlık ara…"
              enterKeyHint="go"
              className="h-10 pl-8 text-base md:h-8 md:text-sm"
              aria-label="Bu kanunda madde ara"
            />
          </label>
        </form>
      </div>

      <ScrollArea className="min-h-0 flex-1">
        <div className="p-2">
          {filtering ? (
            <FilteredArticles tree={tree} query={query} selectedSlug={selectedSlug} />
          ) : (
            <>
              {/* Kanun düzeyindeki notlar; girilmemişse soluk durur. */}
              <div className="mb-2 space-y-0.5 border-b pb-2">
                <LawNoteLink tree={tree} kind="dersNotu" slug="ders-notlari" label="Ders Notları" />
                <LawNoteLink tree={tree} kind="genelGerekce" slug="genel-gerekce" label="Genel Gerekçe" />
              </div>
              {isEmpty && (
                <p className="px-2 py-4 text-sm text-muted-foreground">Bu kanunun maddeleri henüz girilmedi.</p>
              )}
              <ArticleList articles={tree.articles} lawSlug={law.slug} indent={0} selectedSlug={selectedSlug} />
              {tree.nodes.map((node) => (
                <NodeItem key={node.id} node={node} lawSlug={law.slug} depth={0} selectedSlug={selectedSlug} />
              ))}
            </>
          )}
        </div>
      </ScrollArea>
    </div>
  );
}

function LawNoteLink({
  tree,
  kind,
  slug,
  label,
}: {
  tree: LawTreeResponse;
  kind: EditorialNoteKind;
  slug: string;
  label: string;
}) {
  const pathname = usePathname();
  const href = `/${tree.law.slug}/${slug}`;
  if (!tree.noteKinds.includes(kind)) {
    return (
      <p className="flex items-center gap-2 px-2 py-1 text-muted-foreground/70" title="Henüz girilmedi">
        <FileText className="size-4" aria-hidden /> {label}
      </p>
    );
  }
  const selected = pathname === href;
  return (
    <Link
      href={href}
      aria-current={selected ? "page" : undefined}
      className={cn(
        "flex items-center gap-2 rounded-md px-2 py-1",
        selected ? "bg-primary text-primary-foreground" : "hover:bg-accent hover:text-accent-foreground",
      )}
    >
      <FileText className="size-4" aria-hidden /> {label}
    </Link>
  );
}

function containsArticle(node: LawNodeTree, slug: string | null): boolean {
  if (!slug) return false;
  return node.articles.some((a) => a.slug === slug) || node.children.some((c) => containsArticle(c, slug));
}

function NodeItem({
  node,
  lawSlug,
  depth,
  selectedSlug,
}: {
  node: LawNodeTree;
  lawSlug: string;
  depth: number;
  selectedSlug: string | null;
}) {
  const [open, setOpen] = useState(() => containsArticle(node, selectedSlug));
  const isEmpty = node.children.length === 0 && node.articles.length === 0;

  return (
    <Collapsible open={open} onOpenChange={setOpen}>
      <CollapsibleTrigger
        className="flex w-full items-start gap-1.5 rounded-md px-1 py-1.5 text-left hover:bg-accent"
        style={{ paddingLeft: `${depth * 12 + 4}px` }}
      >
        {open ? (
          <ChevronDown className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden />
        ) : (
          <ChevronRight className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden />
        )}
        <span className="min-w-0 flex-1 leading-tight">
          <span className="block">{node.number}</span>
          <span className="block text-sm text-muted-foreground">{node.title}</span>
        </span>
        {node.effectiveVisibility === "private" && <PrivateMark />}
      </CollapsibleTrigger>
      <CollapsibleContent>
        {isEmpty && (
          <p className="py-1 text-sm text-muted-foreground" style={{ paddingLeft: `${(depth + 1) * 12 + 24}px` }}>
            Henüz madde girilmedi.
          </p>
        )}
        <ArticleList
          articles={node.articles}
          lawSlug={lawSlug}
          indent={(depth + 1) * 12 + 4}
          selectedSlug={selectedSlug}
        />
        {node.children.map((child) => (
          <NodeItem key={child.id} node={child} lawSlug={lawSlug} depth={depth + 1} selectedSlug={selectedSlug} />
        ))}
      </CollapsibleContent>
    </Collapsible>
  );
}

function ArticleList({
  articles,
  lawSlug,
  indent,
  selectedSlug,
}: {
  articles: ArticleSummary[];
  lawSlug: string;
  indent: number;
  selectedSlug: string | null;
}) {
  if (articles.length === 0) return null;
  return (
    <ul style={{ paddingLeft: `${indent}px` }}>
      {articles.map((article) => {
        const selected = article.slug === selectedSlug;
        return (
          <li key={article.id}>
            <Link
              href={`/${lawSlug}/${article.slug}`}
              aria-current={selected ? "page" : undefined}
              className={cn(
                "grid w-full grid-cols-[4.5rem_1fr_auto] items-center gap-1 rounded-md px-2 py-1 text-left text-sm",
                selected ? "bg-primary text-primary-foreground" : "hover:bg-accent hover:text-accent-foreground",
              )}
            >
              <span>Madde {article.number}</span>
              <span className="truncate" title={article.title}>
                {article.title}
              </span>
              {article.effectiveVisibility === "private" ? <PrivateMark /> : <span />}
            </Link>
          </li>
        );
      })}
    </ul>
  );
}

function flattenArticles(nodes: LawNodeTree[]): ArticleSummary[] {
  return nodes.flatMap((node) => [...node.articles, ...flattenArticles(node.children)]);
}

/** Kanun içi anlık filtre: tüm madde listesi istemcide olduğundan sunucuya gidilmez. */
function FilteredArticles({
  tree,
  query,
  selectedSlug,
}: {
  tree: LawTreeResponse;
  query: string;
  selectedSlug: string | null;
}) {
  const { results } = filterArticles(tree, query);

  if (results.length === 0) return <p className="px-2 py-4 text-sm text-muted-foreground">Eşleşen madde yok.</p>;
  return <ArticleList articles={results} lawSlug={tree.law.slug} indent={0} selectedSlug={selectedSlug} />;
}

/** Kanunda madde süzgeci: "81", "m. 81", "madde 81" numaraya; başka metin başlığa bakar. */
export function filterArticles(tree: LawTreeResponse, query: string) {
  const normalizedNumber = query.trim().replace(/^(m(adde)?\.?\s*)/i, "");
  const all = [...tree.articles, ...flattenArticles(tree.nodes)];
  const results = all.filter(
    (a) => a.number === normalizedNumber || matchesTr(a.title, query) || matchesTr(`madde ${a.number}`, query),
  );
  return { exact: all.find((a) => a.number.toLocaleLowerCase("tr") === normalizedNumber.toLocaleLowerCase("tr")), results };
}
