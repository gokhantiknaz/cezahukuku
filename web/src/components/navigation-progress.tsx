"use client";

import { useEffect, useRef, useState } from "react";
import { usePathname, useSearchParams } from "next/navigation";
import { ScalesLoader } from "./scales-loader";

/** Gösterge en az bu kadar görünür (hızlı geçişlerde yanıp sönmesin). Sayfanın kendisini geciktirmez. */
const MIN_VISIBLE_MS = 1000;
/** Gezinme hiç tamamlanmazsa (iptal, hata) gösterge en geç bu sürede kapanır. */
const MAX_VISIBLE_MS = 15_000;

/** Sol tık ile aynı sekmede, sitenin başka bir sayfasına giden bağlantı mı? */
function isPageNavigation(event: MouseEvent) {
  if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return false;
  const anchor = event.target instanceof Element ? event.target.closest("a[href]") : null;
  if (!(anchor instanceof HTMLAnchorElement) || anchor.hasAttribute("download")) return false;
  if (anchor.target && anchor.target !== "_self") return false;
  const url = new URL(anchor.href);
  return (
    url.origin === window.location.origin &&
    (url.pathname !== window.location.pathname || url.search !== window.location.search)
  );
}

/**
 * Sayfa geçişlerinde başlığın altında terazili "Yükleniyor" göstergesi ve en üstte ince ilerleme çubuğu.
 * Bağlantı tıklanınca açılır, adres değişince (en az MIN_VISIBLE_MS sonra) kapanır. useSearchParams nedeniyle
 * <Suspense> içinde kullanılır.
 */
export function NavigationProgress() {
  const pathname = usePathname();
  const search = useSearchParams().toString();
  const currentUrl = search ? `${pathname}?${search}` : pathname;
  const currentUrlRef = useRef(currentUrl);
  const [pending, setPending] = useState<{ from: string; startedAt: number } | null>(null);

  useEffect(() => {
    currentUrlRef.current = currentUrl;
  }, [currentUrl]);

  useEffect(() => {
    const onClick = (event: MouseEvent) => {
      if (isPageNavigation(event)) setPending({ from: currentUrlRef.current, startedAt: Date.now() });
    };
    document.addEventListener("click", onClick);
    return () => document.removeEventListener("click", onClick);
  }, []);

  useEffect(() => {
    if (!pending) return;
    const arrived = pending.from !== currentUrl;
    const delay = arrived ? Math.max(0, MIN_VISIBLE_MS - (Date.now() - pending.startedAt)) : MAX_VISIBLE_MS;
    const timer = setTimeout(() => setPending(null), delay);
    return () => clearTimeout(timer);
  }, [pending, currentUrl]);

  if (!pending) return null;
  return (
    <div role="status" aria-live="polite" className="pointer-events-none fixed inset-x-0 top-0 z-50 print:hidden">
      <div className="h-0.5 overflow-hidden bg-primary/15">
        <div className="animate-nav-progress h-full w-1/3 bg-primary" />
      </div>
      <div className="mx-auto mt-24 flex w-fit animate-in items-center gap-3 rounded-full border bg-card/95 py-1.5 pr-5 pl-3 shadow-lg duration-200 fade-in slide-in-from-top-2">
        <ScalesLoader className="size-9" duration={1.6} />
        <span className="font-serif font-bold tracking-wide text-primary">Yükleniyor…</span>
      </div>
    </div>
  );
}
