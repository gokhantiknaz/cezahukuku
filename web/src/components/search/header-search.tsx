"use client";

import { Search } from "lucide-react";
import { QuickSearch } from "./quick-search";

/** Başlıktaki arama kutusu: tıklayınca hızlı arama açılır (her sayfada Ctrl+K ile de). */
export function HeaderSearch() {
  return (
    <QuickSearch>
      {(open) => (
        <button
          type="button"
          onClick={open}
          className="flex h-10 w-full items-center gap-3 rounded-md border bg-background px-3 text-left text-base text-muted-foreground hover:border-primary/40"
        >
          <Search className="size-5 shrink-0" aria-hidden />
          <span className="flex-1 truncate">Madde, konu, karar veya kanun ara…</span>
          <kbd className="hidden rounded border bg-card px-1.5 text-xs sm:inline">Ctrl K</kbd>
        </button>
      )}
    </QuickSearch>
  );
}
