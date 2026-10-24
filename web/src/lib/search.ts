import type { SearchKind } from "@/api/generated/model";

export const searchKindLabels: Record<SearchKind, string> = {
  article: "Madde metni",
  gerekce: "Gerekçe",
  aciklama: "Açıklama",
  dersNotu: "Ders notu",
  genelGerekce: "Genel gerekçe",
  ictihat: "İçtihat",
  normDenetimi: "Norm denetimi",
  note: "Notum",
  highlight: "Vurgum",
};

/** Filtre düğmelerinin sırası. */
export const searchKindOrder: SearchKind[] = [
  "article",
  "gerekce",
  "aciklama",
  "dersNotu",
  "genelGerekce",
  "ictihat",
  "normDenetimi",
  "note",
  "highlight",
];

export const isSearchKind = (value: string | undefined): value is SearchKind =>
  !!value && (searchKindOrder as string[]).includes(value);

// API kesitte eşleşen sözcükleri bu iki karakter arasına koyar (SearchQueries.MatchStart/MatchEnd).
const MATCH_START = "\u0002";
const MATCH_END = "\u0003";

/** Kesiti düz ve eşleşen parçalara böler: "a \u0002b\u0003 c" → [{a}, {b, match}, {c}]. */
export function splitSnippet(snippet: string) {
  const parts: { text: string; match: boolean }[] = [];
  for (const [i, chunk] of snippet.split(MATCH_START).entries()) {
    if (i === 0) {
      if (chunk) parts.push({ text: chunk, match: false });
      continue;
    }
    const [match, rest = ""] = chunk.split(MATCH_END);
    if (match) parts.push({ text: match, match: true });
    if (rest) parts.push({ text: rest, match: false });
  }
  return parts;
}
