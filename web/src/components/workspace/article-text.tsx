import { Fragment, type CSSProperties, type ReactNode } from "react";
import Link from "next/link";
import { StickyNote } from "lucide-react";
import type { ChangeMark, CitationSpan, ParagraphDto } from "@/api/generated/model";
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

/** Değişikliği Değişiklikler sekmesindeki kaydıyla eşleştiren anahtar (sürüm:fıkra:bent). */
export const changeKey = (change: { versionId: string; paragraphId: string; itemId: string | null }) =>
  `${change.versionId}:${change.paragraphId}:${change.itemId ?? ""}`;

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

/**
 * Metinden kelime çıkarılan yer: metin içeriği boş (seçimin metindeki konumu bozulmasın), "mülga" yazısı CSS ile
 * gösterilir. Tıklanınca (okuma ekranı) Değişiklikler sekmesinde o değişiklik açılır.
 */
function DeletionMarker({ change }: { change: ChangeMark }) {
  return (
    <span
      data-change-key={changeKey(change)}
      aria-label="mülga: buradan metin çıkarıldı"
      title="Buradan metin çıkarıldı; önceki metin için tıklayın"
      className="mx-0.5 cursor-pointer align-super text-[0.65em] font-medium text-change after:content-['mülga'] hover:underline print:hidden"
    />
  );
}

/**
 * Metnin [from, to) aralığını vurgularla ve son değişiklikle böler; üst üste binen vurgularda sonra eklenen görünür.
 * Değişiklikle gelen kelimeler kırmızı; yerine bir şey gelmeden çıkarılan yerde "mülga" işareti.
 */
function withMarks(text: string, from: number, to: number, marks: TextMark[], change?: ChangeMark): ReactNode[] {
  const inside = marks.filter((m) => m.start < to && m.start + m.length > from);
  const ranges = (change?.inserted ?? []).filter((r) => r.start < to && r.start + r.length > from);
  // Metnin sonundaki çıkarma metnin son parçasından sonra gösterilir.
  const deletions = (change?.deletedAt ?? []).filter((at) => at >= from && at < to);
  const deletedAtEnd = to === text.length && (change?.deletedAt ?? []).includes(text.length);
  if (inside.length === 0 && ranges.length === 0 && deletions.length === 0 && !deletedAtEnd) return [text.slice(from, to)];
  const points = new Set([from, to]);
  for (const m of inside) {
    points.add(Math.max(from, m.start));
    points.add(Math.min(to, m.start + m.length));
  }
  for (const r of ranges) {
    points.add(Math.max(from, r.start));
    points.add(Math.min(to, r.start + r.length));
  }
  for (const at of deletions) points.add(at);
  const sorted = [...points].sort((a, b) => a - b);
  const pieces = sorted.slice(0, -1).map((start, i) => {
    const end = sorted[i + 1];
    const mark = inside.findLast((m) => m.start <= start && m.start + m.length >= end);
    const changed = ranges.some((r) => r.start <= start && r.start + r.length >= end);
    let piece: ReactNode = text.slice(start, end);
    if (mark)
      piece = (
        <mark data-highlight-id={mark.highlightId} className={cn("rounded-[2px] text-inherit", mark.className)}>
          {piece}
        </mark>
      );
    if (changed)
      piece = (
        <span
          data-change-key={changeKey(change!)}
          title="Değişiklikle gelen metin; önceki metin için tıklayın"
          className="cursor-pointer text-change hover:underline"
        >
          {piece}
        </span>
      );
    const notes = inside.filter((m) => m.note && m.start + m.length === end);
    return (
      <Fragment key={start}>
        {deletions.includes(start) && <DeletionMarker change={change!} />}
        {piece}
        {notes.map((m) => (
          <NoteMarker key={m.highlightId} mark={m} />
        ))}
      </Fragment>
    );
  });
  if (deletedAtEnd) pieces.push(<DeletionMarker key="son" change={change!} />);
  return pieces;
}

/**
 * Metni atıf bağlantıları, vurgular ve son değişiklikle böler: "… [13 üncü maddede] yazılı …" → metin, bağlantı,
 * metin. Metin sonradan değiştiyse (eski dizin) taşan ya da çakışan atıf atlanır.
 */
function renderText(text: string, spans: CitationSpan[] = [], marks: TextMark[] = [], change?: ChangeMark): ReactNode {
  if (spans.length === 0 && marks.length === 0 && !change) return text;
  const parts: ReactNode[] = [];
  let position = 0;
  for (const span of [...spans].sort((a, b) => a.start - b.start)) {
    if (span.start < position || span.start + span.length > text.length) continue;
    parts.push(...withMarks(text, position, span.start, marks, change));
    const { target } = span;
    parts.push(
      <Link
        key={`atif-${span.start}`}
        href={`/${target.lawSlug}/${target.articleSlug}`}
        title={`${target.lawLabel} Madde ${target.number}${target.title ? ` — ${target.title}` : ""}`}
        className="text-primary underline decoration-primary/40 decoration-dotted underline-offset-4 hover:decoration-solid"
      >
        {withMarks(text, span.start, span.start + span.length, marks, change)}
      </Link>,
    );
    position = span.start + span.length;
  }
  parts.push(...withMarks(text, position, text.length, marks, change));
  return parts;
}

/** Kaldırılan fıkra ya da bentin eski yerinde duran satır: "(2) Mülga". */
function RemovedLine({ change, label }: { change: ChangeMark; label: string }) {
  return (
    <>
      <span className={cn(change.itemId === null ? "text-muted-foreground" : "shrink-0")}>{label}</span>
      <button
        type="button"
        data-change-key={changeKey(change)}
        title="Kaldırıldı; önceki metin için tıklayın"
        className="w-fit cursor-pointer text-left text-change hover:underline"
      >
        Mülga
      </button>
    </>
  );
}

/**
 * Madde metni: numaralı fıkralar, bentler ve alt bentler. `marked` içindeki fıkralar vurgulanır (değişenler);
 * `citations` verilirse metindeki madde atıfları bağlantı olur; `marks` kişisel vurgulardır; `changes` her
 * fıkra/bentin son değişikliğidir (gelen kelimeler kırmızı, kaldırılan fıkra/bent eski yerinde "Mülga").
 * Her fıkra/bent metni `data-paragraph-id` / `data-item-id` taşır: seçimin metindeki konumu buradan hesaplanır
 * (öğenin metin içeriği fıkra/bent metninin aynısıdır).
 */
export function ParagraphList({
  paragraphs,
  marked,
  citations,
  marks,
  changes,
}: {
  paragraphs: ParagraphDto[];
  marked?: Set<string>;
  citations?: CitationSpan[];
  marks?: Map<string, TextMark[]>;
  changes?: ChangeMark[];
}) {
  const byText = new Map<string, CitationSpan[]>();
  for (const span of citations ?? []) {
    const key = blockKey(span.paragraphId, span.itemId);
    byText.set(key, [...(byText.get(key) ?? []), span]);
  }
  const changeOf = new Map<string, ChangeMark>();
  // Kaldırılanlar, yeni metinde ardından geldikleri fıkranın ("" en baş) ya da bentin anahtarıyla.
  const removedAfter = new Map<string, ChangeMark[]>();
  for (const change of changes ?? []) {
    if (change.kind !== "removed") {
      changeOf.set(blockKey(change.paragraphId, change.itemId), change);
      continue;
    }
    const key =
      change.itemId === null ? (change.anchorParagraphId ?? "") : blockKey(change.paragraphId, change.anchorItemId);
    removedAfter.set(key, [...(removedAfter.get(key) ?? []), change]);
  }
  const render = (text: string, paragraphId: string, itemId: string | null) =>
    renderText(
      text,
      byText.get(blockKey(paragraphId, itemId)),
      marks?.get(blockKey(paragraphId, itemId)),
      changeOf.get(blockKey(paragraphId, itemId)),
    );
  const removedParagraphs = (afterParagraphId: string) =>
    (removedAfter.get(afterParagraphId) ?? []).map((change) => (
      <li key={changeKey(change)} className="grid grid-cols-[1.9rem_1fr] md:grid-cols-[2.25rem_1fr]">
        <RemovedLine change={change} label={change.label} />
      </li>
    ));
  const removedItems = (paragraphId: string, afterItemId: string | null) =>
    (removedAfter.get(blockKey(paragraphId, afterItemId)) ?? []).map((change) => (
      <li key={changeKey(change)} className="flex gap-1.5 md:gap-2">
        {/* Etiketin fıkra numarasından sonraki kısmı: "(1) c)" → "c)". */}
        <RemovedLine change={change} label={change.label.split(" ").at(-1) ?? ""} />
      </li>
    ));

  return (
    <ol className="reading space-y-2">
      {removedParagraphs("")}
      {paragraphs.map((p) => (
        <Fragment key={p.id}>
          <li
            // Telefonda fıkra numarası ve bent girintisi daha dar: satıra daha çok kelime sığsın.
            className={cn(
              "grid grid-cols-[1.9rem_1fr] md:grid-cols-[2.25rem_1fr]",
              marked?.has(p.id) && "-mx-1 rounded bg-accent px-1",
            )}
          >
            <span className="text-muted-foreground">{p.number != null ? `(${p.number})` : ""}</span>
            <div>
              {p.text && <p data-paragraph-id={p.id}>{render(p.text, p.id, null)}</p>}
              {(p.items.length > 0 || removedAfter.has(blockKey(p.id, null))) && (
                <ul className="mt-1 space-y-1">
                  {removedItems(p.id, null)}
                  {p.items.map((item) => (
                    <Fragment key={item.id}>
                      {/* Seviye 0: bentlerden sonra fıkrayı bağlayan düz metin; 1 bent, 2 alt bent. */}
                      <li
                        className="flex gap-1.5 pl-[calc(var(--level)*1rem)] md:gap-2 md:pl-[calc(var(--level)*1.5rem)]"
                        style={{ "--level": Math.max(item.level - 1, 0) } as CSSProperties}
                      >
                        {item.label && <span className="shrink-0">{item.label}</span>}
                        <span data-paragraph-id={p.id} data-item-id={item.id}>
                          {render(item.text, p.id, item.id)}
                        </span>
                      </li>
                      {removedItems(p.id, item.id)}
                    </Fragment>
                  ))}
                </ul>
              )}
            </div>
          </li>
          {removedParagraphs(p.id)}
        </Fragment>
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
