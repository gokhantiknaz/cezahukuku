"use client";

import { useEffect, useMemo, useRef, useState, type KeyboardEvent, type PointerEvent } from "react";
import { createPortal } from "react-dom";
import { useQueryClient } from "@tanstack/react-query";
import { Check, Trash2 } from "lucide-react";
import { toast } from "sonner";
import {
  getListHighlightsQueryKey,
  useCreateHighlight,
  useDeleteHighlight,
  useListHighlights,
  useUpdateHighlight,
} from "@/api/generated/personal/personal";
import type { CitationSpan, HighlightColor, ParagraphDto, TextPosition } from "@/api/generated/model";
import { useHydrated } from "@/hooks/use-hydrated";
import { useCurrentUser } from "@/lib/auth";
import { canAnnotate, highlightClassName, highlightColors } from "@/lib/highlights";
import { cn } from "@/lib/utils";
import { groupMarks, ParagraphList } from "./article-text";

/** Araç çubuğu: yeni seçim için renkler ya da tıklanan vurgu için renk değiştir / sil. */
type Toolbar =
  | { kind: "selection"; top: number; left: number; segments: TextPosition[] }
  | { kind: "highlight"; top: number; left: number; highlightId: string; color: HighlightColor };

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

/** Araç çubuğu seçimin/vurgunun üst ortasında; sayfanın üstüne taşarsa altında. */
function toolbarPosition(rect: DOMRect) {
  const above = rect.top - 52;
  return { top: above > 8 ? above : rect.bottom + 8, left: rect.left + rect.width / 2 };
}

/**
 * Madde metni + kişisel vurgular. Vurgulama yetkisi olan kullanıcı metinde bir yer seçince renk araç çubuğu
 * açılır; var olan vurguya tıklayınca rengi değiştirilir ya da silinir. Vurgular gösterilen sürümün metnine göre
 * yerleştirilir; madde değiştiği için bulunamayanlar metinde görünmez (sayısı altta belirtilir).
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
  const { data: user } = useCurrentUser();
  const enabled = useHydrated() && canAnnotate(user);
  const containerRef = useRef<HTMLDivElement>(null);
  const toolbarRef = useRef<HTMLDivElement>(null);
  const [toolbar, setToolbar] = useState<Toolbar | null>(null);

  const queryClient = useQueryClient();
  const { data } = useListHighlights(articleId, { versionId }, { query: { enabled } });
  const highlights = useMemo(() => (data?.status === 200 ? data.data : []), [data]);
  const refresh = () => queryClient.invalidateQueries({ queryKey: getListHighlightsQueryKey(articleId).slice(0, 1) });
  const create = useCreateHighlight();
  const update = useUpdateHighlight();
  const remove = useDeleteHighlight();

  const marks = useMemo(
    () =>
      groupMarks(
        highlights.flatMap((h) =>
          h.segments.map((s) => ({
            paragraphId: s.paragraphId,
            itemId: s.itemId,
            mark: { highlightId: h.id, start: s.start, length: s.length, className: highlightClassName(h.color) },
          })),
        ),
      ),
    [highlights],
  );
  const staleCount = highlights.filter((h) => h.isStale).length;

  // Kaydırınca, Esc'ye basınca ya da metin ve araç çubuğu dışına tıklayınca kapanır.
  useEffect(() => {
    if (!toolbar) return;
    const close = () => setToolbar(null);
    const onKey = (event: globalThis.KeyboardEvent) => event.key === "Escape" && close();
    const onPointerDown = (event: globalThis.PointerEvent) => {
      const target = event.target as Node;
      if (!toolbarRef.current?.contains(target) && !containerRef.current?.contains(target)) close();
    };
    window.addEventListener("scroll", close, true);
    window.addEventListener("keydown", onKey);
    document.addEventListener("pointerdown", onPointerDown);
    return () => {
      window.removeEventListener("scroll", close, true);
      window.removeEventListener("keydown", onKey);
      document.removeEventListener("pointerdown", onPointerDown);
    };
  }, [toolbar]);

  function handlePointerUp(event: PointerEvent | KeyboardEvent) {
    const container = containerRef.current;
    const selection = window.getSelection();
    if (!container || !selection) return;

    if (!selection.isCollapsed && selection.rangeCount > 0) {
      const range = selection.getRangeAt(0);
      const segments = selectionSegments(range, container);
      setToolbar(segments.length > 0 ? { kind: "selection", ...toolbarPosition(range.getBoundingClientRect()), segments } : null);
      return;
    }

    const mark = event.target instanceof Element ? event.target.closest("mark[data-highlight-id]") : null;
    const highlight = mark && highlights.find((h) => h.id === mark.getAttribute("data-highlight-id"));
    setToolbar(
      highlight
        ? {
            kind: "highlight",
            ...toolbarPosition(mark.getBoundingClientRect()),
            highlightId: highlight.id,
            color: highlight.color,
          }
        : null,
    );
  }

  async function apply(color: HighlightColor) {
    if (!toolbar) return;
    try {
      if (toolbar.kind === "selection") {
        await create.mutateAsync({ data: { articleVersionId: versionId, color, segments: toolbar.segments } });
        window.getSelection()?.removeAllRanges();
      } else {
        await update.mutateAsync({ id: toolbar.highlightId, data: { color } });
      }
      await refresh();
      setToolbar(null);
    } catch {
      toast.error("Vurgu kaydedilemedi.");
    }
  }

  async function removeHighlight(id: string) {
    try {
      await remove.mutateAsync({ id });
      await refresh();
      setToolbar(null);
    } catch {
      toast.error("Vurgu silinemedi.");
    }
  }

  const busy = create.isPending || update.isPending || remove.isPending;

  return (
    <>
      <div
        ref={containerRef}
        onPointerUp={enabled ? handlePointerUp : undefined}
        onKeyUp={enabled ? (e) => e.shiftKey && handlePointerUp(e) : undefined}
        className={cn(enabled && "[&_mark[data-highlight-id]]:cursor-pointer")}
      >
        <ParagraphList paragraphs={paragraphs} citations={citations} marks={marks} />
      </div>
      {staleCount > 0 && (
        <p className="mt-3 text-sm text-muted-foreground">
          {staleCount} vurgunuz bu metinde bulunamadı: madde değiştiği için vurguladığınız ifade artık yok.
        </p>
      )}

      {toolbar &&
        createPortal(
          <div
            ref={toolbarRef}
            role="toolbar"
            aria-label={toolbar.kind === "selection" ? "Vurgula" : "Vurguyu düzenle"}
            style={{ top: toolbar.top, left: toolbar.left }}
            // Düğmeye basmak metindeki seçimi kaldırmasın.
            onMouseDown={(e) => e.preventDefault()}
            className="fixed z-50 flex -translate-x-1/2 animate-in items-center gap-1 rounded-full border bg-popover p-1.5 shadow-lg duration-100 fade-in zoom-in-95"
          >
            {highlightColors.map((color) => {
              const current = toolbar.kind === "highlight" && toolbar.color === color.key;
              return (
                <button
                  key={color.key}
                  type="button"
                  disabled={busy}
                  onClick={() => apply(color.key)}
                  title={current ? `${color.label} (şu anki renk)` : `${color.label} ile vurgula`}
                  aria-label={`${color.label} ile vurgula`}
                  aria-pressed={toolbar.kind === "highlight" ? current : undefined}
                  className={cn(
                    "flex size-7 items-center justify-center rounded-full border border-black/10 transition-transform hover:scale-110",
                    color.className,
                    current && "ring-2 ring-primary ring-offset-1 ring-offset-popover",
                  )}
                >
                  {current && <Check className="size-4 text-foreground" aria-hidden />}
                </button>
              );
            })}
            {toolbar.kind === "highlight" && (
              <>
                <span className="mx-0.5 h-5 w-px bg-border" aria-hidden />
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => removeHighlight(toolbar.highlightId)}
                  title="Vurguyu kaldır"
                  aria-label="Vurguyu kaldır"
                  className="flex size-7 items-center justify-center rounded-full text-muted-foreground hover:bg-accent hover:text-destructive"
                >
                  <Trash2 className="size-4" />
                </button>
              </>
            )}
          </div>,
          document.body,
        )}
    </>
  );
}
