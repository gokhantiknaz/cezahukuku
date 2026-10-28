"use client";

import { useEffect, useState, type ReactNode } from "react";
import { cn } from "@/lib/utils";

/**
 * Sayfanın üstüne yapışan başlık; aşağı kaydırınca yukarı çekilir, yukarı kaydırınca geri gelir. Yalnız dar ekranda
 * etkilidir: geniş ekranda sayfa kaymaz (paneller kendi içinde kayar), başlık hep görünür.
 */
export function AutoHideHeader({ className, children }: { className?: string; children: ReactNode }) {
  const [hidden, setHidden] = useState(false);

  useEffect(() => {
    let last = window.scrollY;
    const onScroll = () => {
      const y = window.scrollY;
      // Parmağın küçük titremeleri başlığı oynatmasın; en üste yakınken hep görünür.
      if (Math.abs(y - last) < 8) return;
      setHidden(y > last && y > 80);
      last = y;
    };
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  return (
    <header
      data-hidden={hidden || undefined}
      className={cn("sticky top-0 z-40 transition-transform duration-200 data-hidden:-translate-y-full", className)}
    >
      {children}
    </header>
  );
}
