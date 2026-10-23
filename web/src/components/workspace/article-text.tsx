import type { ParagraphDto } from "@/api/generated/model";

/**
 * Fıkraları mevzuat biçiminde düz metne çevirir ("(1) ...", "a) ..."). Panelde düzenlenecek metin ve "Kopyala"
 * için kullanılır; backend'deki ayrıştırıcı (ArticleTextParser) bu biçimi aynı yapıya geri çevirir.
 */
export function paragraphsToText(paragraphs: ParagraphDto[]) {
  return paragraphs
    .flatMap((p) => [
      p.number != null ? `(${p.number}) ${p.text}`.trimEnd() : p.text,
      ...p.items.map((i) => (i.label ? `${i.label} ${i.text}` : i.text)),
    ])
    .filter((line) => line.length > 0)
    .join("\n");
}

/** Madde metni: numaralı fıkralar, bentler ve alt bentler. */
export function ParagraphList({ paragraphs }: { paragraphs: ParagraphDto[] }) {
  return (
    <ol className="reading space-y-2">
      {paragraphs.map((p) => (
        <li key={p.id} className="grid grid-cols-[2.25rem_1fr]">
          <span className="text-muted-foreground">{p.number != null ? `(${p.number})` : ""}</span>
          <div>
            {p.text && <p>{p.text}</p>}
            {p.items.length > 0 && (
              <ul className="mt-1 space-y-1">
                {p.items.map((item) => (
                  // Seviye 0: bentlerden sonra fıkrayı bağlayan düz metin; 1 bent, 2 alt bent.
                  <li
                    key={item.id}
                    className="flex gap-2"
                    style={{ paddingLeft: `${Math.max(item.level - 1, 0) * 1.5}rem` }}
                  >
                    {item.label && <span className="shrink-0">{item.label}</span>}
                    <span>{item.text}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </li>
      ))}
    </ol>
  );
}
