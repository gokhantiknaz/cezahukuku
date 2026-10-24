"use client";

import {
  useEffect,
  useMemo,
  useRef,
  useState,
  type ComponentProps,
  type KeyboardEvent,
  type PointerEvent,
  type Ref,
} from "react";
import { createPortal } from "react-dom";
import { Check, NotebookPen, Trash2 } from "lucide-react";
import type { CitationSpan, HighlightColor, ParagraphDto, PersonalNoteDto, TextPosition } from "@/api/generated/model";
import { Button } from "@/components/ui/button";
import { highlightClassName, highlightColors } from "@/lib/highlights";
import { cn } from "@/lib/utils";
import { groupMarks, ParagraphList } from "./article-text";
import { NoteForm } from "./note-form";
import { usePersonalLayer } from "./use-personal-layer";

/** Seçimin ya da vurgunun ekrandaki yeri (araç çubuğu üstüne, not düzenleyici altına yerleşir). */
type Anchor = { top: number; bottom: number; centerX: number };

/**
 * Metin üstündeki katman: yeni seçim için renkler, tıklanan vurgu için renk / not / sil, ya da not düzenleyici.
 */
type Overlay =
  | { kind: "selection"; anchor: Anchor; segments: TextPosition[] }
  | { kind: "highlight"; anchor: Anchor; highlightId: string; confirmDelete?: boolean }
  | { kind: "note"; anchor: Anchor; highlightId: string };

const toAnchor = (rect: DOMRect): Anchor => ({ top: rect.top, bottom: rect.bottom, centerX: rect.left + rect.width / 2 });

/** Fıkra/bent öğesinin başından (node, offset) noktasına kadar olan metnin uzunluğu. */
function offsetIn(block: Element, node: Node, offset: number) {
  const range = document.createRange();
  range.selectNodeContents(block);
  range.setEnd(node, offset);
  return range.toString().length;
}

/**
 * Seçimi fıkra/bent parçalarına böler (konumlar her metnin kendi başlangıcına göre). Baştaki ve sondaki boşluklar
 * atılır; yalnız boşluk kalan parça alınmaz.
 */
function selectionSegments(range: Range, container: Element): TextPosition[] {
  const segments: TextPosition[] = [];
  for (const block of container.querySelectorAll("[data-paragraph-id]")) {
    if (!range.intersectsNode(block)) continue;
    const text = block.textContent ?? "";
    let start = block.contains(range.startContainer) ? offsetIn(block, range.startContainer, range.startOffset) : 0;
    let end = block.contains(range.endContainer) ? offsetIn(block, range.endContainer, range.endOffset) : text.length;
    while (start < end && /\s/.test(text[start])) start++;
    while (end > start && /\s/.test(text[end - 1])) end--;
    if (end <= start) continue;
    segments.push({
      paragraphId: block.getAttribute("data-paragraph-id")!,
      itemId: block.getAttribute("data-item-id"),
      start,
      length: end - start,
    });
  }
  return segments;
}

/**
 * Madde metni + kişisel vurgular ve notlar. Vurgulama yetkisi olan kullanıcı metinde bir yer seçince renk araç
 * çubuğu açılır ("Not ekle" seçimi sarıyla vurgulayıp not düzenleyiciyi açar); var olan vurguya tıklayınca rengi
 * değiştirilir, notu yazılır ya da vurgu silinir. Notlu vurgunun sonunda not simgesi durur. Vurgular gösterilen
 * sürümün metnine göre yerleştirilir; madde değiştiği için bulunamayanlar metinde görünmez (sayısı altta belirtilir).
 */
export function HighlightableText({
  articleId,
  versionId,
  paragraphs,
  citations,
}: {
  articleId: string;
  versionId: string;
  paragraphs: ParagraphDto[];
  citations: CitationSpan[];
}) {
  const layer = usePersonalLayer(articleId, versionId);
  const { enabled, highlights, noteOf, busy } = layer;
  const containerRef = useRef<HTMLDivElement>(null);
  const overlayRef = useRef<HTMLDivElement>(null);
  const [overlay, setOverlay] = useState<Overlay | null>(null);

  const marks = useMemo(
    () =>
      groupMarks(
        highlights.flatMap((h) =>
          h.segments.map((s, i) => ({
            paragraphId: s.paragraphId,
            itemId: s.itemId,
            mark: {
              highlightId: h.id,
              start: s.start,
              length: s.length,
              className: highlightClassName(h.color),
              note: i === h.segments.length - 1 ? noteOf.get(h.id)?.text : undefined,
            },
          })),
        ),
      ),
    [highlights, noteOf],
  );
  const staleCount = highlights.filter((h) => h.isStale).length;

  // Araç çubuğu kaydırınca, Esc'ye basınca ya da metin ve çubuk dışına tıklayınca kapanır. Not düzenleyici yalnız
  // kendi düğmeleri ve Esc ile kapanır (yazılan not kaybolmasın).
  useEffect(() => {
    if (!overlay || overlay.kind === "note") return;
    const close = () => setOverlay(null);
    const onKey = (event: globalThis.KeyboardEvent) => event.key === "Escape" && close();
    const onPointerDown = (event: globalThis.PointerEvent) => {
      const target = event.target as Node;
      if (!overlayRef.current?.contains(target) && !containerRef.current?.contains(target)) close();
    };
    window.addEventListener("scroll", close, true);
    window.addEventListener("keydown", onKey);
    document.addEventListener("pointerdown", onPointerDown);
    return () => {
      window.removeEventListener("scroll", close, true);
      window.removeEventListener("keydown", onKey);
      document.removeEventListener("pointerdown", onPointerDown);
    };
  }, [overlay]);

  function handlePointerUp(event: PointerEvent | KeyboardEvent) {
    const container = containerRef.current;
    const selection = window.getSelection();
    if (!container || !selection || overlay?.kind === "note") return;

    if (!selection.isCollapsed && selection.rangeCount > 0) {
      const range = selection.getRangeAt(0);
      const segments = selectionSegments(range, container);
      setOverlay(segments.length > 0 ? { kind: "selection", anchor: toAnchor(range.getBoundingClientRect()), segments } : null);
      return;
    }

    const target = event.target instanceof Element ? event.target : null;
    const marker = target?.closest("[data-note-for]");
    if (marker) {
      setOverlay({ kind: "note", anchor: toAnchor(marker.getBoundingClientRect()), highlightId: marker.getAttribute("data-note-for")! });
      return;
    }
    const mark = target?.closest("mark[data-highlight-id]");
    const highlightId = mark?.getAttribute("data-highlight-id");
    setOverlay(mark && highlightId ? { kind: "highlight", anchor: toAnchor(mark.getBoundingClientRect()), highlightId } : null);
  }

  /** Not simgesi klavyeyle de açılır; bağlantı içindeyse tıklama sayfayı değiştirmesin. */
  function handleMarkerKey(event: KeyboardEvent) {
    const marker = event.target instanceof Element ? event.target.closest("[data-note-for]") : null;
    if (!marker || (event.key !== "Enter" && event.key !== " ")) return;
    event.preventDefault();
    setOverlay({ kind: "note", anchor: toAnchor(marker.getBoundingClientRect()), highlightId: marker.getAttribute("data-note-for")! });
  }

  async function createFromSelection(color: HighlightColor, withNote: boolean) {
    if (overlay?.kind !== "selection") return;
    const { anchor, segments } = overlay;
    const created = await layer.createHighlight(versionId, color, segments);
    if (!created) return;
    window.getSelection()?.removeAllRanges();
    setOverlay(withNote ? { kind: "note", anchor, highlightId: created.id } : null);
  }

  async function recolor(highlightId: string, color: HighlightColor) {
    if (await layer.recolor(highlightId, color)) setOverlay(null);
  }

  async function removeHighlight(highlightId: string) {
    if (await layer.removeHighlight(highlightId)) setOverlay(null);
  }

  async function saveNote(highlightId: string, existing: PersonalNoteDto | undefined, text: string) {
    if (await layer.saveNote(highlightId, existing, text)) setOverlay(null);
  }

  async function removeNote(note: PersonalNoteDto) {
    if (await layer.removeNote(note)) setOverlay(null);
  }

  const current = overlay && overlay.kind !== "selection" ? highlights.find((h) => h.id === overlay.highlightId) : undefined;

  return (
    <>
      <div
        ref={containerRef}
        onPointerUp={enabled ? handlePointerUp : undefined}
        onKeyUp={enabled ? (e) => e.shiftKey && handlePointerUp(e) : undefined}
        onKeyDown={enabled ? handleMarkerKey : undefined}
        // Not simgesi atıf bağlantısının içinde kalırsa tıklamak sayfayı değiştirmesin.
        onClickCapture={(e) => e.target instanceof Element && e.target.closest("[data-note-for]") && e.preventDefault()}
        className={cn(enabled && "[&_mark[data-highlight-id]]:cursor-pointer")}
      >
        <ParagraphList paragraphs={paragraphs} citations={citations} marks={marks} />
      </div>
      {staleCount > 0 && (
        <p className="mt-3 text-sm text-muted-foreground">
          {staleCount} vurgunuz bu metinde bulunamadı: madde değiştiği için vurguladığınız ifade artık yok.
        </p>
      )}

      {overlay &&
        createPortal(
          overlay.kind === "note" ? (
            current && (
              <NoteEditor
                // Başka bir vurgunun notu açılınca düzenleyici yeniden kurulsun (metin alanı sıfırlansın).
                key={current.id}
                ref={overlayRef}
                anchor={overlay.anchor}
                quote={current.quote}
                note={noteOf.get(current.id)}
                busy={busy}
                onSave={(text) => saveNote(current.id, noteOf.get(current.id), text)}
                onDelete={(note) => removeNote(note)}
                onClose={() => setOverlay(null)}
              />
            )
          ) : (
            <div
              ref={overlayRef}
              role="toolbar"
              aria-label={overlay.kind === "selection" ? "Vurgula" : "Vurguyu düzenle"}
              style={{ top: overlay.anchor.top - 52 > 8 ? overlay.anchor.top - 52 : overlay.anchor.bottom + 8, left: overlay.anchor.centerX }}
              // Düğmeye basmak metindeki seçimi kaldırmasın.
              onMouseDown={(e) => e.preventDefault()}
              className="fixed z-50 flex -translate-x-1/2 animate-in items-center gap-1 rounded-full border bg-popover p-1.5 shadow-lg duration-100 fade-in zoom-in-95"
            >
              {overlay.kind === "highlight" && overlay.confirmDelete ? (
                <>
                  <span className="px-2 text-sm">Vurgu notuyla birlikte silinsin mi?</span>
                  <Button size="sm" variant="destructive" disabled={busy} onClick={() => removeHighlight(overlay.highlightId)}>
                    Sil
                  </Button>
                  <Button size="sm" variant="ghost" onClick={() => setOverlay({ ...overlay, confirmDelete: false })}>
                    Vazgeç
                  </Button>
                </>
              ) : (
                <>
                  {highlightColors.map((color) => {
                    const selected = overlay.kind === "highlight" && current?.color === color.key;
                    return (
                      <button
                        key={color.key}
                        type="button"
                        disabled={busy}
                        onClick={() =>
                          overlay.kind === "selection"
                            ? createFromSelection(color.key, false)
                            : recolor(overlay.highlightId, color.key)
                        }
                        title={selected ? `${color.label} (şu anki renk)` : `${color.label} ile vurgula`}
                        aria-label={`${color.label} ile vurgula`}
                        aria-pressed={overlay.kind === "highlight" ? selected : undefined}
                        className={cn(
                          "flex size-7 items-center justify-center rounded-full border border-black/10 transition-transform hover:scale-110",
                          color.className,
                          selected && "ring-2 ring-primary ring-offset-1 ring-offset-popover",
                        )}
                      >
                        {selected && <Check className="size-4 text-foreground" aria-hidden />}
                      </button>
                    );
                  })}
                  <span className="mx-0.5 h-5 w-px bg-border" aria-hidden />
                  <ToolbarButton
                    label={overlay.kind === "highlight" && noteOf.has(overlay.highlightId) ? "Notu aç" : "Not ekle"}
                    disabled={busy}
                    onClick={() =>
                      overlay.kind === "selection"
                        ? createFromSelection("yellow", true)
                        : setOverlay({ kind: "note", anchor: overlay.anchor, highlightId: overlay.highlightId })
                    }
                  >
                    <NotebookPen className="size-4" />
                  </ToolbarButton>
                  {overlay.kind === "highlight" && (
                    <ToolbarButton
                      label="Vurguyu kaldır"
                      disabled={busy}
                      destructive
                      onClick={() =>
                        noteOf.has(overlay.highlightId)
                          ? setOverlay({ ...overlay, confirmDelete: true })
                          : removeHighlight(overlay.highlightId)
                      }
                    >
                      <Trash2 className="size-4" />
                    </ToolbarButton>
                  )}
                </>
              )}
            </div>
          ),
          document.body,
        )}
    </>
  );
}

function ToolbarButton({
  label,
  destructive,
  ...props
}: { label: string; destructive?: boolean } & ComponentProps<"button">) {
  return (
    <button
      type="button"
      title={label}
      aria-label={label}
      className={cn(
        "flex size-7 items-center justify-center rounded-full text-muted-foreground hover:bg-accent",
        destructive ? "hover:text-destructive" : "hover:text-primary",
      )}
      {...props}
    />
  );
}

/** Vurguya bağlı notun düzenleyicisi: vurgunun altında (yer yoksa üstünde) açılan kart. */
function NoteEditor({
  ref,
  anchor,
  quote,
  note,
  busy,
  onSave,
  onDelete,
  onClose,
}: {
  ref: Ref<HTMLDivElement>;
  anchor: Anchor;
  quote: string;
  note: PersonalNoteDto | undefined;
  busy: boolean;
  onSave: (text: string) => void;
  onDelete: (note: PersonalNoteDto) => void;
  onClose: () => void;
}) {
  const width = Math.min(360, window.innerWidth - 16);
  const left = Math.min(Math.max(anchor.centerX - width / 2, 8), window.innerWidth - width - 8);
  const below = anchor.bottom + 8;
  const style = below + 260 < window.innerHeight ? { top: below } : { bottom: window.innerHeight - anchor.top + 8 };

  return (
    <div
      ref={ref}
      role="dialog"
      aria-label="Not"
      style={{ ...style, left, width }}
      className="fixed z-50 animate-in space-y-3 rounded-lg border bg-popover p-3 text-popover-foreground shadow-xl duration-100 fade-in zoom-in-95"
    >
      <p className="line-clamp-2 border-l-4 border-highlight-yellow pl-2 text-sm text-muted-foreground italic">“{quote}”</p>
      <NoteForm
        initialText={note?.text}
        busy={busy}
        onSave={onSave}
        onCancel={onClose}
        onDelete={note ? () => onDelete(note) : undefined}
      />
    </div>
  );
}
