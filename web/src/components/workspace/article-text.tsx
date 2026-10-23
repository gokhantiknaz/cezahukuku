import type { ReactNode } from "react";
import Link from "next/link";
import type { CitationSpan, ParagraphDto } from "@/api/generated/model";
import { cn } from "@/lib/utils";

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

/** Kimlikler dışında fıkranın metni (iki metni karşılaştırmak için). */
export const paragraphKey = (p: ParagraphDto) =>
  JSON.stringify([p.number, p.text, p.items.map((i) => [i.level, i.label, i.text])]);

const citationKey = (paragraphId: string, itemId: string | null) => `${paragraphId}:${itemId ?? ""}`;

/** Metni atıf bağlantılarıyla böler: "… [13 üncü maddede] yazılı …" → metin, bağlantı, metin. */
function withCitations(text: string, spans: CitationSpan[] | undefined): ReactNode {
  if (!spans || spans.length === 0) return text;
  const parts: ReactNode[] = [];
  let position = 0;
  for (const span of [...spans].sort((a, b) => a.start - b.start)) {
    // Metin sonradan değiştiyse (eski dizin) taşan ya da çakışan atıf atlanır.
    if (span.start < position || span.start + span.length > text.length) continue;
    parts.push(text.slice(position, span.start));
    const { target } = span;
    parts.push(
      <Link
        key={span.start}
        href={`/${target.lawSlug}/${target.articleSlug}`}
        title={`${target.lawLabel} Madde ${target.number}${target.title ? ` — ${target.title}` : ""}`}
        className="text-primary underline decoration-primary/40 decoration-dotted underline-offset-4 hover:decoration-solid"
      >
        {text.slice(span.start, span.start + span.length)}
      </Link>,
    );
    position = span.start + span.length;
  }
  parts.push(text.slice(position));
  return parts;
}

/**
 * Madde metni: numaralı fıkralar, bentler ve alt bentler. `marked` içindeki fıkralar vurgulanır (değişenler);
 * `citations` verilirse metindeki madde atıfları bağlantı olur.
 */
export function ParagraphList({
  paragraphs,
  marked,
  citations,
}: {
  paragraphs: ParagraphDto[];
  marked?: Set<string>;
  citations?: CitationSpan[];
}) {
  const byText = new Map<string, CitationSpan[]>();
  for (const span of citations ?? []) {
    const key = citationKey(span.paragraphId, span.itemId);
    byText.set(key, [...(byText.get(key) ?? []), span]);
  }

  return (
    <ol className="reading space-y-2">
      {paragraphs.map((p) => (
        <li
          key={p.id}
          className={cn("grid grid-cols-[2.25rem_1fr]", marked?.has(p.id) && "-mx-1 rounded bg-accent px-1")}
        >
          <span className="text-muted-foreground">{p.number != null ? `(${p.number})` : ""}</span>
          <div>
            {p.text && <p>{withCitations(p.text, byText.get(citationKey(p.id, null)))}</p>}
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
                    <span>{withCitations(item.text, byText.get(citationKey(p.id, item.id)))}</span>
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
