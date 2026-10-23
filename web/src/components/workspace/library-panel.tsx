"use client";

import { useState } from "react";
import {
  Archive,
  ArrowLeft,
  BookOpen,
  ChevronDown,
  ChevronRight,
  FileText,
  Gavel,
  Scale,
  ScrollText,
  Search,
  type LucideIcon,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible";
import { Input } from "@/components/ui/input";
import { ScrollArea } from "@/components/ui/scroll-area";
import type { ArticleSummary, LawGroup, LawGroupKind, LawNode, LawTree } from "@/lib/library-types";
import { matchesTr } from "@/lib/turkish";
import { cn } from "@/lib/utils";

const groupIcons: Record<LawGroupKind, LucideIcon> = {
  temel: Scale,
  ozel: FileText,
  kabahat: Gavel,
  tuzuk: ScrollText,
  mulga: Archive,
};

type LibraryPanelProps = {
  groups: LawGroup[];
  /** Açık kanunun ağacı; null ise kütüphane listesi gösterilir. */
  tree: LawTree | null;
  selectedArticleId: string | null;
  onOpenLaw: (lawId: string) => void;
  onBackToLibrary: () => void;
  onSelectArticle: (articleId: string) => void;
};

/** Sol panel: önce kütüphane (kanun grupları), bir kanuna girilince o kanunun kitap/bölüm/madde ağacı. */
export function LibraryPanel({ tree, ...props }: LibraryPanelProps) {
  return tree ? <LawTreeView key={tree.id} tree={tree} {...props} /> : <LibraryList {...props} />;
}

function PanelHeader({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex items-center gap-2 bg-primary px-3 py-2.5 text-lg text-primary-foreground">{children}</div>
  );
}

function LibraryList({ groups, onOpenLaw }: Pick<LibraryPanelProps, "groups" | "onOpenLaw">) {
  return (
    <div className="flex h-full flex-col">
      <PanelHeader>
        <BookOpen className="size-5" aria-hidden />
        Kütüphane
      </PanelHeader>
      <ScrollArea className="min-h-0 flex-1">
        <ul className="divide-y px-2">
          {groups.map((group) => {
            const Icon = groupIcons[group.kind];
            return (
              <li key={group.id} className="py-3">
                <div className="flex items-center gap-2 px-2 text-primary">
                  <Icon className="size-5 shrink-0" aria-hidden />
                  <span className="text-base font-medium">{group.title}</span>
                </div>
                <ul className="mt-1.5">
                  {group.laws.map((law) => (
                    <li key={law.id}>
                      <button
                        type="button"
                        onClick={() => onOpenLaw(law.id)}
                        className="flex w-full items-start gap-2 rounded-md px-2 py-1.5 pl-9 text-left hover:bg-accent hover:text-accent-foreground"
                      >
                        <span>
                          {law.name} <span className="text-muted-foreground">({law.number})</span>
                        </span>
                      </button>
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

function LawTreeView({
  tree,
  selectedArticleId,
  onBackToLibrary,
  onSelectArticle,
}: Omit<LibraryPanelProps, "tree" | "groups" | "onOpenLaw"> & { tree: LawTree }) {
  const [query, setQuery] = useState("");
  const filtering = query.trim().length > 0;

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
        <span className="truncate">{tree.shortName || tree.name}</span>
      </PanelHeader>

      <div className="space-y-2 border-b p-2">
        <p className="px-1 text-sm text-muted-foreground">
          {tree.name} ({tree.number})
        </p>
        <label className="relative block">
          <Search className="absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
          <Input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Madde no veya başlık ara…"
            className="pl-8"
            aria-label="Bu kanunda madde ara"
          />
        </label>
      </div>

      <ScrollArea className="min-h-0 flex-1">
        <div className="p-2">
          {filtering ? (
            <FilteredArticles
              tree={tree}
              query={query}
              selectedArticleId={selectedArticleId}
              onSelectArticle={onSelectArticle}
            />
          ) : (
            <>
              {/* Kanun düzeyi içerikler M3'te açılacak. */}
              <div className="mb-2 space-y-0.5 border-b pb-2 text-muted-foreground">
                <p className="flex items-center gap-2 px-2 py-1" title="Yakında">
                  <FileText className="size-4" aria-hidden /> Ders Notları
                </p>
                <p className="flex items-center gap-2 px-2 py-1" title="Yakında">
                  <FileText className="size-4" aria-hidden /> Genel Gerekçe
                </p>
              </div>
              {tree.nodes.length === 0 && (
                <p className="px-2 py-4 text-sm text-muted-foreground">Bu kanunun maddeleri henüz girilmedi.</p>
              )}
              {tree.nodes.map((node) => (
                <NodeItem
                  key={node.id}
                  node={node}
                  depth={0}
                  selectedArticleId={selectedArticleId}
                  onSelectArticle={onSelectArticle}
                />
              ))}
            </>
          )}
        </div>
      </ScrollArea>
    </div>
  );
}

function containsArticle(node: LawNode, articleId: string | null): boolean {
  if (!articleId) return false;
  return node.articles.some((a) => a.id === articleId) || node.children.some((c) => containsArticle(c, articleId));
}

function NodeItem({
  node,
  depth,
  selectedArticleId,
  onSelectArticle,
}: {
  node: LawNode;
  depth: number;
  selectedArticleId: string | null;
  onSelectArticle: (id: string) => void;
}) {
  const [open, setOpen] = useState(() => containsArticle(node, selectedArticleId));
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
        <span className="leading-tight">
          <span className="block">{node.number}</span>
          <span className="block text-sm text-muted-foreground">{node.title}</span>
        </span>
      </CollapsibleTrigger>
      <CollapsibleContent>
        {isEmpty && (
          <p className="py-1 text-sm text-muted-foreground" style={{ paddingLeft: `${(depth + 1) * 12 + 24}px` }}>
            Henüz madde girilmedi.
          </p>
        )}
        {node.children.map((child) => (
          <NodeItem
            key={child.id}
            node={child}
            depth={depth + 1}
            selectedArticleId={selectedArticleId}
            onSelectArticle={onSelectArticle}
          />
        ))}
        <ArticleList
          articles={node.articles}
          indent={(depth + 1) * 12 + 4}
          selectedArticleId={selectedArticleId}
          onSelectArticle={onSelectArticle}
        />
      </CollapsibleContent>
    </Collapsible>
  );
}

function ArticleList({
  articles,
  indent,
  selectedArticleId,
  onSelectArticle,
}: {
  articles: ArticleSummary[];
  indent: number;
  selectedArticleId: string | null;
  onSelectArticle: (id: string) => void;
}) {
  if (articles.length === 0) return null;
  return (
    <ul style={{ paddingLeft: `${indent}px` }}>
      {articles.map((article) => {
        const selected = article.id === selectedArticleId;
        return (
          <li key={article.id}>
            <button
              type="button"
              onClick={() => onSelectArticle(article.id)}
              aria-current={selected ? "page" : undefined}
              className={cn(
                "grid w-full grid-cols-[4.5rem_1fr] gap-1 rounded-md px-2 py-1 text-left text-sm",
                selected ? "bg-primary text-primary-foreground" : "hover:bg-accent hover:text-accent-foreground",
              )}
            >
              <span>Madde {article.number}</span>
              <span className="truncate" title={article.title}>
                {article.title}
              </span>
            </button>
          </li>
        );
      })}
    </ul>
  );
}

function flattenArticles(nodes: LawNode[]): ArticleSummary[] {
  return nodes.flatMap((node) => [...flattenArticles(node.children), ...node.articles]);
}

/** Kanun içi anlık filtre: tüm madde listesi istemcide olduğundan sunucuya gidilmez. */
function FilteredArticles({
  tree,
  query,
  selectedArticleId,
  onSelectArticle,
}: {
  tree: LawTree;
  query: string;
  selectedArticleId: string | null;
  onSelectArticle: (id: string) => void;
}) {
  const normalizedNumber = query.trim().replace(/^(m(adde)?\.?\s*)/i, "");
  const results = flattenArticles(tree.nodes).filter(
    (a) => a.number === normalizedNumber || matchesTr(a.title, query) || matchesTr(`madde ${a.number}`, query),
  );

  if (results.length === 0) return <p className="px-2 py-4 text-sm text-muted-foreground">Eşleşen madde yok.</p>;
  return (
    <ArticleList
      articles={results}
      indent={0}
      selectedArticleId={selectedArticleId}
      onSelectArticle={onSelectArticle}
    />
  );
}
