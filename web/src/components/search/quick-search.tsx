"use client";

import { useEffect, useRef, useState, type KeyboardEvent as ReactKeyboardEvent, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { keepPreviousData } from "@tanstack/react-query";
import { ArrowRight, CornerDownLeft, Search } from "lucide-react";
import { useSearch } from "@/api/generated/search/search";
import type { SearchKind } from "@/api/generated/model";
import { Dialog, DialogContent, DialogTitle } from "@/components/ui/dialog";
import { useDebouncedValue } from "@/hooks/use-debounced-value";
import { cn } from "@/lib/utils";
import { KindBadge } from "./kind-badge";
import { Snippet } from "./snippet";

type Item = { url: string; title: string; kind?: SearchKind; context?: string | null; snippet?: string };

/**
 * Hızlı arama (Ctrl+K / ⌘K): yazdıkça ilk sonuçlar; "tck 81" gibi göndermede en üstte "maddeye git".
 * Ok tuşlarıyla seçilip Enter ile açılır; sonuç seçili değilken Enter detaylı aramaya (/ara) gider.
 * Başlıktaki arama kutusu da bunu açar.
 */
export function QuickSearch({ children }: { children: (open: () => void) => ReactNode }) {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        setOpen(true);
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  return (
    <>
      {children(() => setOpen(true))}
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent showCloseButton={false} className="top-[12%] translate-y-0 gap-0 overflow-hidden p-0 sm:max-w-2xl">
          <DialogTitle className="sr-only">Hızlı arama</DialogTitle>
          {/* Her açılışta boş başlasın. */}
          {open && <QuickSearchBody onClose={() => setOpen(false)} />}
        </DialogContent>
      </Dialog>
    </>
  );
}

function QuickSearchBody({ onClose }: { onClose: () => void }) {
  const router = useRouter();
  const [text, setText] = useState("");
  const [active, setActive] = useState(0);
  const listRef = useRef<HTMLUListElement>(null);
  const query = useDebouncedValue(text.trim(), 200);
  const enabled = query.length >= 2;
  const { data, isFetching } = useSearch({ q: query, limit: 8 }, { query: { enabled, placeholderData: keepPreviousData } });
  const result = enabled && data?.status === 200 ? data.data : null;

  const items: Item[] = [
    ...(result?.jump ? [{ url: result.jump.url, title: result.jump.label }] : []),
    ...(result?.hits ?? []).map((h) => ({ url: h.url, title: h.title, kind: h.kind, context: h.context, snippet: h.snippet })),
  ];
  const selected = Math.min(active, Math.max(items.length - 1, 0));

  function go(url: string) {
    onClose();
    router.push(url);
  }

  function onKeyDown(event: ReactKeyboardEvent) {
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      const next = event.key === "ArrowDown" ? Math.min(selected + 1, items.length - 1) : Math.max(selected - 1, 0);
      setActive(next);
      listRef.current?.children[next]?.scrollIntoView({ block: "nearest" });
    } else if (event.key === "Enter") {
      event.preventDefault();
      if (items[selected] && query === text.trim()) go(items[selected].url);
      else if (text.trim()) go(`/ara?q=${encodeURIComponent(text.trim())}`);
    }
  }

  return (
    <div onKeyDown={onKeyDown}>
      <label className="flex items-center gap-3 border-b px-4">
        <Search className="size-5 shrink-0 text-muted-foreground" aria-hidden />
        <input
          autoFocus
          value={text}
          onChange={(e) => {
            setText(e.target.value);
            setActive(0);
          }}
          placeholder="Madde, konu, karar ara… (tck 81, kasten öldürme)"
          aria-label="Ara"
          className="h-14 w-full bg-transparent text-lg outline-none placeholder:text-muted-foreground"
        />
        {isFetching && <span className="text-xs text-muted-foreground">aranıyor…</span>}
      </label>

      {enabled && result && items.length === 0 && !isFetching && (
        <p className="px-4 py-6 text-center text-muted-foreground">“{query}” için sonuç yok.</p>
      )}
      {items.length > 0 && (
        <ul ref={listRef} role="listbox" aria-label="Sonuçlar" className="max-h-[60vh] overflow-y-auto py-1">
          {items.map((item, i) => (
            <li key={`${item.url}-${i}`} role="option" aria-selected={i === selected}>
              <button
                type="button"
                onMouseMove={() => setActive(i)}
                onClick={() => go(item.url)}
                className={cn("block w-full px-4 py-2.5 text-left", i === selected && "bg-accent")}
              >
                <span className="flex items-center gap-2">
                  {item.kind ? <KindBadge kind={item.kind} /> : <ArrowRight className="size-4 text-primary" aria-hidden />}
                  <span className="truncate font-medium">{item.title}</span>
                  {item.context && <span className="shrink-0 text-sm text-muted-foreground">{item.context}</span>}
                </span>
                {item.snippet && <Snippet text={item.snippet} className="mt-0.5 line-clamp-2 font-reading text-sm" />}
              </button>
            </li>
          ))}
        </ul>
      )}

      <div className="flex items-center justify-between gap-2 border-t bg-muted/40 px-4 py-2 text-xs text-muted-foreground">
        {/* Klavye ipuçları dokunmatik ekranda anlamsız. */}
        <span className="flex items-center gap-1 pointer-coarse:hidden">
          <kbd className="rounded border bg-card px-1">↑</kbd>
          <kbd className="rounded border bg-card px-1">↓</kbd> seç ·
          <kbd className="rounded border bg-card px-1">
            <CornerDownLeft className="inline size-3" />
          </kbd>
          aç · <kbd className="rounded border bg-card px-1">Esc</kbd> kapat
        </span>
        {text.trim() && (
          <button type="button" className="ml-auto py-1 text-primary hover:underline" onClick={() => go(`/ara?q=${encodeURIComponent(text.trim())}`)}>
            {result && result.total > 0 ? `Tüm sonuçlar (${result.total})` : "Detaylı arama"} →
          </button>
        )}
      </div>
    </div>
  );
}
