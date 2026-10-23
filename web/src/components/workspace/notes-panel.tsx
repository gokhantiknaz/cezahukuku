import { Highlighter, Mic, NotebookPen } from "lucide-react";
import { Button } from "@/components/ui/button";
import { ScrollArea } from "@/components/ui/scroll-area";

// Kişisel vurgu renkleri (globals.css'teki --highlight-* değişkenleri).
export const highlightColors = [
  { key: "yellow", label: "Sarı", className: "bg-highlight-yellow" },
  { key: "green", label: "Yeşil", className: "bg-highlight-green" },
  { key: "blue", label: "Mavi", className: "bg-highlight-blue" },
  { key: "orange", label: "Turuncu", className: "bg-highlight-orange" },
] as const;

/** Sağ panel: seçili maddedeki kişisel vurgular ve notlar (M4'te dolacak). */
export function NotesPanel() {
  return (
    <div className="flex h-full flex-col">
      <div className="flex items-center gap-2 bg-primary px-3 py-2.5 text-lg text-primary-foreground">
        <NotebookPen className="size-5" aria-hidden />
        Notlarım
      </div>
      <ScrollArea className="min-h-0 flex-1">
        <div className="space-y-5 p-4">
          <section>
            <h3 className="flex items-center gap-2 font-medium">
              <Highlighter className="size-4" aria-hidden /> Vurgu renkleri
            </h3>
            <ul className="mt-2 flex flex-wrap gap-2">
              {highlightColors.map((color) => (
                <li key={color.key} className={`rounded px-2 py-0.5 text-sm ${color.className}`}>
                  {color.label}
                </li>
              ))}
            </ul>
          </section>

          <p className="text-sm text-muted-foreground">
            Madde metninde bir yeri seçip vurgulayabilecek, seçili metne yazılı ya da sesli not bağlayabileceksiniz.
            Notlarınızı yalnızca siz görürsünüz.
          </p>

          <Button variant="outline" className="w-full" disabled title="Yakında">
            <Mic /> Sesli not al
          </Button>
        </div>
      </ScrollArea>
    </div>
  );
}
