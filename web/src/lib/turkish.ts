// Backend'deki CezaHukuku.Parsing.Text.TurkishText ile aynı kurallar (istemci tarafı anlık filtre için).

const foldMap: Record<string, string> = {
  ç: "c",
  ğ: "g",
  ı: "i",
  ö: "o",
  ş: "s",
  ü: "u",
  â: "a",
  î: "i",
  û: "u",
};

/** Türkçe küçük harf (İ→i, I→ı) + boşluk sadeleştirme. */
export function normalizeTr(text: string) {
  return text.normalize("NFC").toLocaleLowerCase("tr").replace(/\s+/g, " ").trim();
}

/** Aksansız hâl: "Öldürme" → "oldurme". */
export function foldTr(text: string) {
  return normalizeTr(text).replace(/[çğıöşüâîû]/g, (c) => foldMap[c] ?? c);
}

/** Aranan ifade metinde geçiyor mu? Türkçe karakter kullanılmadan yazılsa da eşleşir. */
export function matchesTr(text: string, query: string) {
  const q = foldTr(query);
  return q.length === 0 || foldTr(text).includes(q);
}
