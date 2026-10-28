import { Fragment, type CSSProperties, type ReactNode } from "react";
import Link from "next/link";
import { StickyNote } from "lucide-react";
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

const blockKey = (paragraphId: string, itemId: string | null) => `${paragraphId}:${itemId ?? ""}`;

/**
 * Metindeki kişisel vurgu parçası (fıkra ya da bent metninde). `note` verilirse parçanın sonunda not simgesi
 * gösterilir (vurgunun son parçasında; üstüne gelince not metni görünür).
 */
export type TextMark = { highlightId: string; start: number; length: number; className: string; note?: string };

/** Vurgunun bağlı notu: metinde yer kaplamayan (metin içeriği boş) simge; tıklanınca not açılır. */
function NoteMarker({ mark }: { mark: TextMark }) {
  return (
    <span
      role="button"
      tabIndex={0}
      data-note-for={mark.highlightId}
      title={mark.note}
      aria-label="Notu aç"
      className="mx-0.5 inline-flex -translate-y-1.5 cursor-pointer align-baseline text-primary hover:text-primary/70 print:hidden"
    >
      <StickyNote className="size-3.5" aria-hidden />
    </span>
  );
}

/** Metnin [from, to) aralığını vurgularla böler; üst üste binen vurgularda sonra eklenen görünür. */
function withMarks(text: string, from: number, to: number, marks: TextMark[]): ReactNode[] {
  const inside = marks.filter((m) => m.start < to && m.start + m.length > from);
  if (inside.length === 0) return [text.slice(from, to)];
  const points = new Set([from, to]);
  for (const m of inside) {
    points.add(Math.max(from, m.start));
    points.add(Math.min(to, m.start + m.length));
  }
  const sorted = [...points].sort((a, b) => a - b);
  return sorted.slice(0, -1).map((start, i) => {
    const end = sorted[i + 1];
    const mark = inside.findLast((m) => m.start <= start && m.start + m.length >= end);
    const piece = text.slice(start, end);
    const notes = inside.filter((m) => m.note && m.start + m.length === end);
    return (
      <Fragment key={start}>
        {mark ? (
          <mark data-highlight-id={mark.highlightId} className={cn("rounded-[2px] text-inherit", mark.className)}>
            {piece}
          </mark>
        ) : (
          piece
        )}
        {notes.map((m) => (
          <NoteMarker key={m.highlightId} mark={m} />
        ))}
      </Fragment>
    );
  });
}

/**
 * Metni atıf bağlantıları ve vurgularla böler: "… [13 üncü maddede] yazılı …" → metin, bağlantı, metin.
 * Metin sonradan değiştiyse (eski dizin) taşan ya da çakışan atıf atlanır.
 */
function renderText(text: string, spans: CitationSpan[] = [], marks: TextMark[] = []): ReactNode {
  if (spans.length === 0 && marks.length === 0) return text;
  const parts: ReactNode[] = [];
  let position = 0;
  for (const span of [...spans].sort((a, b) => a.start - b.start)) {
    if (span.start < position || span.start + span.length > text.length) continue;
    parts.push(...withMarks(text, position, span.start, marks));
    const { target } = span;
    parts.push(
      <Link
        key={`atif-${span.start}`}
        href={`/${target.lawSlug}/${target.articleSlug}`}
        title={`${target.lawLabel} Madde ${target.number}${target.title ? ` — ${target.title}` : ""}`}
        className="text-primary underline decoration-primary/40 decoration-dotted underline-offset-4 hover:decoration-solid"
      >
        {withMarks(text, span.start, span.start + span.length, marks)}
      </Link>,
    );
    position = span.start + span.length;
  }
  parts.push(...withMarks(text, position, text.length, marks));
  return parts;
}

/**
 * Madde metni: numaralı fıkralar, bentler ve alt bentler. `marked` içindeki fıkralar vurgulanır (değişenler);
 * `citations` verilirse metindeki madde atıfları bağlantı olur; `marks` kişisel vurgulardır.
 * Her fıkra/bent metni `data-paragraph-id` / `data-item-id` taşır: seçimin metindeki konumu buradan hesaplanır
 * (öğenin metin içeriği fıkra/bent metninin aynısıdır).
 */
export function ParagraphList({
  paragraphs,
  marked,
  citations,
  marks,
}: {
  paragraphs: ParagraphDto[];
  marked?: Set<string>;
  citations?: CitationSpan[];
  marks?: Map<string, TextMark[]>;
}) {
  const byText = new Map<string, CitationSpan[]>();
  for (const span of citations ?? []) {
    const key = blockKey(span.paragraphId, span.itemId);
    byText.set(key, [...(byText.get(key) ?? []), span]);
  }
  const render = (text: string, paragraphId: string, itemId: string | null) =>
    renderText(text, byText.get(blockKey(paragraphId, itemId)), marks?.get(blockKey(paragraphId, itemId)));

  return (
    <ol className="reading space-y-2">
      {paragraphs.map((p) => (
        <li
          key={p.id}
          // Telefonda fıkra numarası ve bent girintisi daha dar: satıra daha çok kelime sığsın.
          className={cn("grid grid-cols-[1.9rem_1fr] md:grid-cols-[2.25rem_1fr]", marked?.has(p.id) && "-mx-1 rounded bg-accent px-1")}
        >
          <span className="text-muted-foreground">{p.number != null ? `(${p.number})` : ""}</span>
          <div>
            {p.text && <p data-paragraph-id={p.id}>{render(p.text, p.id, null)}</p>}
            {p.items.length > 0 && (
              <ul className="mt-1 space-y-1">
                {p.items.map((item) => (
                  // Seviye 0: bentlerden sonra fıkrayı bağlayan düz metin; 1 bent, 2 alt bent.
                  <li
                    key={item.id}
                    className="flex gap-1.5 pl-[calc(var(--level)*1rem)] md:gap-2 md:pl-[calc(var(--level)*1.5rem)]"
                    style={{ "--level": Math.max(item.level - 1, 0) } as CSSProperties}
                  >
                    {item.label && <span className="shrink-0">{item.label}</span>}
                    <span data-paragraph-id={p.id} data-item-id={item.id}>
                      {render(item.text, p.id, item.id)}
                    </span>
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

/** Vurguları fıkra/bent anahtarına göre gruplar (ParagraphList `marks`). */
export function groupMarks(items: { paragraphId: string; itemId: string | null; mark: TextMark }[]) {
  const byBlock = new Map<string, TextMark[]>();
  for (const { paragraphId, itemId, mark } of items) {
    const key = blockKey(paragraphId, itemId);
    byBlock.set(key, [...(byBlock.get(key) ?? []), mark]);
  }
  return byBlock;
}
