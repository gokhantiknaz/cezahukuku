import type { ArticleSummary, LawNodeTree, LawTreeResponse } from "@/api/generated/model";

export type TreeRow =
  | { type: "node"; node: LawNodeTree; depth: number }
  | { type: "article"; article: ArticleSummary; depth: number };

/**
 * Ağacı düz satırlara çevirir. Aynı üst öğedeki düğüm ve maddeler tek sıra uzayını paylaşır (backend'deki
 * LawOutline ile aynı kural: sıraya göre, eşitlikte önce madde).
 */
export function treeRows(tree: LawTreeResponse): TreeRow[] {
  const rows: TreeRow[] = [];
  const visit = (nodes: LawNodeTree[], articles: ArticleSummary[], depth: number) => {
    const entries = [
      ...articles.map((article) => ({ order: article.sortOrder, rank: 0, article, node: null })),
      ...nodes.map((node) => ({ order: node.sortOrder, rank: 1, article: null, node })),
    ].sort((a, b) => a.order - b.order || a.rank - b.rank);

    for (const entry of entries) {
      if (entry.article) {
        rows.push({ type: "article", article: entry.article, depth });
      } else if (entry.node) {
        rows.push({ type: "node", node: entry.node, depth });
        visit(entry.node.children, entry.node.articles, depth + 1);
      }
    }
  };
  visit(tree.nodes, tree.articles, 0);
  return rows;
}

/** Düğüm seçim listeleri için: tüm düğümler ağaç sırasıyla ve derinlikleriyle. */
export function flattenNodes(nodes: LawNodeTree[], depth = 0): { node: LawNodeTree; depth: number }[] {
  return [...nodes]
    .sort((a, b) => a.sortOrder - b.sortOrder)
    .flatMap((node) => [{ node, depth }, ...flattenNodes(node.children, depth + 1)]);
}

export function findNode(nodes: LawNodeTree[], id: string): LawNodeTree | undefined {
  for (const node of nodes) {
    if (node.id === id) return node;
    const found = findNode(node.children, id);
    if (found) return found;
  }
}

export function allArticles(tree: LawTreeResponse): ArticleSummary[] {
  const fromNodes = (nodes: LawNodeTree[]): ArticleSummary[] =>
    nodes.flatMap((node) => [...node.articles, ...fromNodes(node.children)]);
  return [...tree.articles, ...fromNodes(tree.nodes)];
}

/** Düğümün kendisi ve tüm alt düğümleri (düğüm bunlardan birinin altına taşınamaz). */
export function selfAndDescendantIds(node: LawNodeTree): Set<string> {
  const ids = new Set<string>([node.id]);
  const visit = (n: LawNodeTree) => n.children.forEach((c) => (ids.add(c.id), visit(c)));
  visit(node);
  return ids;
}

export const nodeLabel = (node: Pick<LawNodeTree, "number" | "title">) => `${node.number} — ${node.title}`;
