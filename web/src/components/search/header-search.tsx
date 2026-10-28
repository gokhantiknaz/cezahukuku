"use client";

import { Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import { QuickSearch } from "./quick-search";

/** Başlıktaki arama kutusu (dar ekranda simge): tıklayınca hızlı arama açılır (her sayfada Ctrl+K ile de). */
export function HeaderSearch() {
  return (
    <QuickSearch>
      {(open) => (
        <>
          <button
            type="button"
            onClick={open}
            className="hidden h-10 w-full items-center gap-3 rounded-md border bg-background px-3 text-left text-base text-muted-foreground hover:border-primary/40 lg:flex"
          >
            <Search className="size-5 shrink-0" aria-hidden />
            <span className="flex-1 truncate">Madde, konu, karar veya kanun ara…</span>
            <kbd className="rounded border bg-card px-1.5 text-xs">Ctrl K</kbd>
          </button>
          <Button variant="ghost" size="icon-lg" onClick={open} className="lg:hidden" aria-label="Ara" title="Ara">
            <Search className="size-6" />
          </Button>
        </>
      )}
    </QuickSearch>
  );
}
