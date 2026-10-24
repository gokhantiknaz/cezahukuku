import { splitSnippet } from "@/lib/search";
import { cn } from "@/lib/utils";

/** Arama sonucundaki kesit: eşleşen sözcükler vurgulu. */
export function Snippet({ text, className }: { text: string; className?: string }) {
  if (!text) return null;
  return (
    <p className={cn("text-muted-foreground", className)}>
      {splitSnippet(text).map((part, i) =>
        part.match ? (
          <mark key={i} className="rounded-[2px] bg-highlight-yellow px-0.5 text-foreground">
            {part.text}
          </mark>
        ) : (
          <span key={i}>{part.text}</span>
        ),
      )}
    </p>
  );
}
