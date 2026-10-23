// Kütüphane ve madde görünümünün beklediği veri şekilleri. M1'de API sözleşmesi bunlara göre çıkarılacak
// ve bu tipler orval'ın ürettikleriyle değiştirilecek.

export type LawGroupKind = "temel" | "ozel" | "kabahat" | "tuzuk" | "mulga";

export type LawSummary = {
  id: string;
  number: string;
  shortName: string;
  name: string;
};

export type LawGroup = {
  id: string;
  kind: LawGroupKind;
  title: string;
  laws: LawSummary[];
};

export type ArticleSummary = {
  id: string;
  number: string;
  title: string;
};

export type LawNodeKind = "kitap" | "kisim" | "bolum" | "ayrim";

export type LawNode = {
  id: string;
  kind: LawNodeKind;
  number: string;
  title: string;
  children: LawNode[];
  articles: ArticleSummary[];
};

export type LawTree = LawSummary & { nodes: LawNode[] };

export type Paragraph = { number: number; text: string };

export type ArticleDetail = ArticleSummary & {
  law: LawSummary;
  /** Kökten maddeye kadar ağaç düğümleri (breadcrumb için). */
  path: Pick<LawNode, "id" | "kind" | "number" | "title">[];
  paragraphs: Paragraph[];
};
