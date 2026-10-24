/**
 * Excel'den (ya da Google E-Tablolar'dan) kopyalanan hücreler: satırlar satır sonuyla, hücreler sekmeyle ayrılır;
 * hücrede satır sonu ya da tırnak varsa hücre tırnak içine alınır ("" = tek tırnak). İlk satır başlıksa atılır.
 */
export function parseTable(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let cell = "";
  let quoted = false;
  const source = text.replace(/\r\n?/g, "\n");

  for (let i = 0; i < source.length; i++) {
    const c = source[i];
    if (quoted) {
      if (c === '"' && source[i + 1] === '"') {
        cell += '"';
        i++;
      } else if (c === '"') {
        quoted = false;
      } else {
        cell += c;
      }
    } else if (c === '"' && cell === "") {
      quoted = true;
    } else if (c === "\t") {
      row.push(cell);
      cell = "";
    } else if (c === "\n") {
      row.push(cell);
      rows.push(row);
      row = [];
      cell = "";
    } else {
      cell += c;
    }
  }
  if (cell !== "" || row.length > 0) {
    row.push(cell);
    rows.push(row);
  }
  return rows.map((r) => r.map((c) => c.trim())).filter((r) => r.some((c) => c !== ""));
}

/**
 * İlk satır başlık mı? İlk hücrede rakam yoksa ("Madde no") ya da satırda sütun adı geçiyorsa ("5237 | 765 | Not").
 * Tahmindir; kullanıcı "İlk satır başlık" seçeneğiyle değiştirebilir.
 */
export function guessHeaderRow(rows: string[][]) {
  if (rows.length === 0) return false;
  const first = rows[0];
  return !/\d/.test(first[0] ?? "") || first.some((cell) => /^(madde|madde no|no|kanun|not|açıklama|başlık|metin|karşılık)$/i.test(cell));
}

/**
 * "Madde no | Başlık | Metin" tablosunu içe aktarma metnine çevirir: her madde için başlık satırı ve
 * "Madde 81- metin" satırı. Fıkra numaraları ("(1)", "(2)") ve bentler ("a)") metinde olduğu gibi ayrıştırılır.
 */
export function articlesTableToText(rows: string[][]) {
  return rows
    .map(([number = "", title = "", text = ""]) => {
      const no = number.replace(/^madde\s*/i, "").trim();
      const body = text.replace(/\n{2,}/g, "\n").trim();
      return [title.trim(), `Madde ${no}- ${body}`].filter(Boolean).join("\n");
    })
    .join("\n");
}
