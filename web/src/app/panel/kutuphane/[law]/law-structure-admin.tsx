"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams, useSearchParams } from "next/navigation";
import { ArrowLeft, ExternalLink, FilePlus, FolderPlus, Lock } from "lucide-react";
import { ApiError } from "@/api/fetcher";
import { useGetLaw } from "@/api/generated/library/library";
import type { LawTreeResponse } from "@/api/generated/model";
import { ArticleEditor } from "@/components/panel/article-editor";
import { nodeKindLabels } from "@/components/panel/form";
import { NodeEditor } from "@/components/panel/node-editor";
import { allArticles, findNode, selfAndDescendantIds, treeRows } from "@/components/panel/tree-utils";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

type Selection =
  | { kind: "node"; id: string }
  | { kind: "article"; id: string }
  | { kind: "new-node"; parentId: string | null }
  | { kind: "new-article"; nodeId: string | null };

/** Kanunun yapısı (kitap/bölüm ağacı) solda, seçili düğüm ya da madde sağda düzenlenir. */
export function LawStructureAdmin() {
  const { law: lawSlug } = useParams<{ law: string }>();
  const { data, error } = useGetLaw(lawSlug);

  if (error) {
    // Üretilen istemci hata tipini bilmiyor; istekler apiFetch'ten ApiError olarak düşer.
    const failure: unknown = error;
    const notFound = failure instanceof ApiError && failure.status === 404;
    return <p className="mt-6 text-destructive">{notFound ? "Kanun bulunamadı." : "Yüklenemedi."}</p>;
  }
  if (data?.status !== 200) return <p className="mt-6 text-muted-foreground">Yükleniyor…</p>;
  return <StructureEditor tree={data.data} />;
}

function StructureEditor({ tree }: { tree: LawTreeResponse }) {
  const { law } = tree;
  // Okuma ekranındaki "Düzenle" bağlantısı ?madde=madde-81 ile gelir.
  const searchParams = useSearchParams();
  const [selection, setSelection] = useState<Selection | null>(() => {
    const slug = searchParams.get("madde");
    const article = slug ? allArticles(tree).find((a) => a.slug === slug) : undefined;
    return article ? { kind: "article", id: article.id } : null;
  });

  const rows = treeRows(tree);
  const selectedNode = selection?.kind === "node" ? findNode(tree.nodes, selection.id) : undefined;
  const selectedArticle = selection?.kind === "article" ? allArticles(tree).find((a) => a.id === selection.id) : undefined;
  const isSelected = (kind: "node" | "article", id: string) => selection?.kind === kind && selection.id === id;

  return (
    <div className="mt-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="icon-sm" asChild>
            <Link href="/panel/kutuphane" aria-label="Kütüphaneye dön" title="Kütüphaneye dön">
              <ArrowLeft />
            </Link>
          </Button>
          <h1 className="text-2xl">
            {law.shortName ? `${law.shortName} — ` : ""}
            {law.name}
          </h1>
        </div>
        <Button variant="ghost" size="sm" asChild>
          <Link href={`/${law.slug}`} target="_blank">
            <ExternalLink /> Okuma ekranında aç
          </Link>
        </Button>
      </div>

      <div className="mt-4 grid gap-4 lg:grid-cols-[minmax(18rem,26rem)_1fr]">
        <aside className="rounded-lg border bg-card">
          <div className="flex flex-wrap gap-1 border-b p-2">
            <Button variant="outline" size="sm" onClick={() => setSelection({ kind: "new-node", parentId: null })}>
              <FolderPlus /> Düğüm
            </Button>
            <Button variant="outline" size="sm" onClick={() => setSelection({ kind: "new-article", nodeId: null })}>
              <FilePlus /> Madde
            </Button>
          </div>
          {rows.length === 0 ? (
            <p className="p-4 text-sm text-muted-foreground">
              Kanun boş. Önce “Birinci Kitap” gibi bir düğüm ya da bölümsüz bir kanunsa doğrudan madde ekleyin.
            </p>
          ) : (
            <ul className="max-h-[70vh] overflow-y-auto p-1">
              {rows.map((row) =>
                row.type === "node" ? (
                  <li key={row.node.id} className="group flex items-center" style={{ paddingLeft: row.depth * 14 }}>
                    <button
                      type="button"
                      onClick={() => setSelection({ kind: "node", id: row.node.id })}
                      className={cn(
                        "min-w-0 flex-1 rounded-md px-2 py-1.5 text-left",
                        isSelected("node", row.node.id) ? "bg-primary text-primary-foreground" : "hover:bg-accent",
                      )}
                    >
                      <span className="block truncate font-medium">
                        {row.node.number}
                        {row.node.visibility === "private" && <Lock className="ml-1 inline size-3.5" />}
                      </span>
                      <span className="block truncate text-sm opacity-80">
                        {nodeKindLabels[row.node.kind]} · {row.node.title}
                      </span>
                    </button>
                    <div className="flex opacity-60 group-hover:opacity-100">
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        title="Altına düğüm ekle"
                        aria-label={`${row.node.number} altına düğüm ekle`}
                        onClick={() => setSelection({ kind: "new-node", parentId: row.node.id })}
                      >
                        <FolderPlus />
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        title="Altına madde ekle"
                        aria-label={`${row.node.number} altına madde ekle`}
                        onClick={() => setSelection({ kind: "new-article", nodeId: row.node.id })}
                      >
                        <FilePlus />
                      </Button>
                    </div>
                  </li>
                ) : (
                  <li key={row.article.id} style={{ paddingLeft: row.depth * 14 }}>
                    <button
                      type="button"
                      onClick={() => setSelection({ kind: "article", id: row.article.id })}
                      className={cn(
                        "grid w-full grid-cols-[5rem_1fr_auto] items-center gap-1 rounded-md px-2 py-1 text-left text-sm",
                        isSelected("article", row.article.id)
                          ? "bg-primary text-primary-foreground"
                          : "hover:bg-accent",
                      )}
                    >
                      <span>Madde {row.article.number}</span>
                      <span className="truncate">{row.article.title}</span>
                      {row.article.visibility === "private" ? <Lock className="size-3.5" /> : <span />}
                    </button>
                  </li>
                ),
              )}
            </ul>
          )}
        </aside>

        <section className="rounded-lg border bg-card p-4">
          {selection === null && (
            <p className="text-muted-foreground">
              Soldan bir düğüm ya da madde seçin, ya da yeni ekleyin. Düğümlerin yanındaki simgelerle o düğümün
              altına bölüm veya madde eklenir.
            </p>
          )}
          {selection?.kind === "new-node" && (
            <NodeEditor
              key={`new-node:${selection.parentId}`}
              lawId={law.id}
              nodes={tree.nodes}
              parentId={selection.parentId}
              onSaved={(id) => setSelection({ kind: "node", id })}
              onDeleted={() => setSelection(null)}
            />
          )}
          {selectedNode && (
            <NodeEditor
              key={`node:${selectedNode.id}`}
              lawId={law.id}
              nodes={tree.nodes}
              node={selectedNode}
              excludeFromParents={selfAndDescendantIds(selectedNode)}
              onSaved={(id) => setSelection({ kind: "node", id })}
              onDeleted={() => setSelection(null)}
            />
          )}
          {selection?.kind === "new-article" && (
            <ArticleEditor
              key={`new-article:${selection.nodeId}`}
              lawId={law.id}
              lawSlug={law.slug}
              nodes={tree.nodes}
              nodeId={selection.nodeId}
              onSaved={(id) => setSelection({ kind: "article", id })}
              onDeleted={() => setSelection(null)}
            />
          )}
          {selectedArticle && (
            <ArticleEditor
              key={`article:${selectedArticle.id}`}
              lawId={law.id}
              lawSlug={law.slug}
              nodes={tree.nodes}
              article={selectedArticle}
              onSaved={(id) => setSelection({ kind: "article", id })}
              onDeleted={() => setSelection(null)}
            />
          )}
        </section>
      </div>
    </div>
  );
}
