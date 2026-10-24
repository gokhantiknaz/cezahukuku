import type { CSSProperties } from "react";
import { cn } from "@/lib/utils";

/**
 * Adalet terazisi: kol iki yana yatar, kefeler sırayla iner kalkar. Yükleme göstergesi (giriş, sayfa geçişi) ve
 * giriş ekranında süs olarak kullanılır. `duration` saniye cinsinden bir salınım; hareket azaltma tercihinde durur.
 */
export function ScalesLoader({ className, duration = 2.4 }: { className?: string; duration?: number }) {
  return (
    <svg
      viewBox="0 0 120 120"
      fill="none"
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden
      className={cn("scales text-primary", className)}
      style={{ "--scales-duration": `${duration}s` } as CSSProperties}
    >
      {/* Gövde, kaide ve tepe topuzu sabit. */}
      <path d="M60 23 V101" strokeWidth={4} />
      <path d="M50 101 H70" strokeWidth={5} />
      <path d="M38 109 H82" strokeWidth={5} />
      <circle cx="60" cy="17" r="5" fill="currentColor" stroke="none" />

      <g className="scales-beam">
        <path d="M20 30 H100" strokeWidth={4} />
        <circle cx="60" cy="30" r="3.5" fill="currentColor" stroke="none" />
      </g>
      {/* Kefeler kolun uçlarına asılı; kol dönerken dik kalıp yalnız aşağı yukarı hareket eder. */}
      <g className="scales-pan-left">
        <path d="M20 30 L8 66 M20 30 L32 66" strokeWidth={1.5} />
        <path d="M4 66 H36 A16 10 0 0 1 4 66 Z" strokeWidth={3} fill="currentColor" fillOpacity={0.15} />
      </g>
      <g className="scales-pan-right">
        <path d="M100 30 L88 66 M100 30 L112 66" strokeWidth={1.5} />
        <path d="M84 66 H116 A16 10 0 0 1 84 66 Z" strokeWidth={3} fill="currentColor" fillOpacity={0.15} />
      </g>
    </svg>
  );
}

/** Ekranı kaplayan yükleme katmanı (girişte, yönlendirme bitene kadar). */
export function LoadingOverlay({ title }: { title: string }) {
  return (
    <div
      role="status"
      aria-live="polite"
      className="fixed inset-0 z-50 flex animate-in flex-col items-center justify-center gap-3 bg-background/90 backdrop-blur-sm duration-300 fade-in"
    >
      <ScalesLoader className="size-32" />
      <p className="mt-2 font-serif text-xl font-bold tracking-wide text-primary">{title}</p>
      <p className="text-sm text-muted-foreground italic">“Adalet mülkün temelidir.”</p>
    </div>
  );
}
