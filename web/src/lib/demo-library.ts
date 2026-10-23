// GEÇİCİ: M1'de API'den gelen verilerle değiştirilip silinecek. Yalnız düzeni gösterebilmek için
// küçük bir TCK kesiti (5237 s. TCK, İkinci Kitap › İkinci Kısım › Birinci Bölüm, md. 81–85).

import type { ArticleDetail, LawGroup, LawNode, LawTree } from "./library-types";

const tck = { id: "tck", number: "5237", shortName: "TCK", name: "Türk Ceza Kanunu" };

export const demoGroups: LawGroup[] = [
  {
    id: "temel",
    kind: "temel",
    title: "Temel Ceza Kanunları",
    laws: [
      tck,
      { id: "cmk", number: "5271", shortName: "CMK", name: "Ceza Muhakemesi Kanunu" },
      {
        id: "cgtik",
        number: "5275",
        shortName: "CGTİK",
        name: "Ceza ve Güvenlik Tedbirlerinin İnfazı Hakkında Kanun",
      },
    ],
  },
  {
    id: "ozel",
    kind: "ozel",
    title: "Özel Ceza Kanunları",
    laws: [
      { id: "5607", number: "5607", shortName: "", name: "Kaçakçılıkla Mücadele Kanunu" },
      { id: "3713", number: "3713", shortName: "TMK", name: "Terörle Mücadele Kanunu" },
      {
        id: "6136",
        number: "6136",
        shortName: "",
        name: "Ateşli Silahlar ve Bıçaklar ile Diğer Aletler Hakkında Kanun",
      },
    ],
  },
  {
    id: "kabahat",
    kind: "kabahat",
    title: "Kabahatler",
    laws: [{ id: "5326", number: "5326", shortName: "", name: "Kabahatler Kanunu" }],
  },
];

const hayataKarsiSuclar: LawNode = {
  id: "tck-k2-ki2-b1",
  kind: "bolum",
  number: "Birinci Bölüm",
  title: "Hayata Karşı Suçlar",
  children: [],
  articles: [
    { id: "tck-81", number: "81", title: "Kasten öldürme" },
    { id: "tck-82", number: "82", title: "Nitelikli haller" },
    { id: "tck-83", number: "83", title: "Kasten öldürmenin ihmali davranışla işlenmesi" },
    { id: "tck-84", number: "84", title: "İntihara yönlendirme" },
    { id: "tck-85", number: "85", title: "Taksirle öldürme" },
  ],
};

export const demoTrees: Record<string, LawTree> = {
  tck: {
    ...tck,
    nodes: [
      { id: "tck-k1", kind: "kitap", number: "Birinci Kitap", title: "Genel Hükümler", children: [], articles: [] },
      {
        id: "tck-k2",
        kind: "kitap",
        number: "İkinci Kitap",
        title: "Özel Hükümler",
        articles: [],
        children: [
          {
            id: "tck-k2-ki2",
            kind: "kisim",
            number: "İkinci Kısım",
            title: "Kişilere Karşı Suçlar",
            articles: [],
            children: [hayataKarsiSuclar],
          },
        ],
      },
    ],
  },
};

const path81 = [
  { id: "tck-k2", kind: "kitap" as const, number: "İkinci Kitap", title: "Özel Hükümler" },
  { id: "tck-k2-ki2", kind: "kisim" as const, number: "İkinci Kısım", title: "Kişilere Karşı Suçlar" },
  { id: "tck-k2-ki2-b1", kind: "bolum" as const, number: "Birinci Bölüm", title: "Hayata Karşı Suçlar" },
];

export function getDemoArticle(articleId: string): ArticleDetail | null {
  const summary = hayataKarsiSuclar.articles.find((a) => a.id === articleId);
  if (!summary) return null;

  const paragraphs =
    articleId === "tck-81"
      ? [{ number: 1, text: "Bir insanı kasten öldüren kişi, müebbet hapis cezası ile cezalandırılır." }]
      : [];

  return { ...summary, law: tck, path: path81, paragraphs };
}
