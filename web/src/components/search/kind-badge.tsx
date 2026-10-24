import type { SearchKind } from "@/api/generated/model";
import { Badge } from "@/components/ui/badge";
import { searchKindLabels } from "@/lib/search";
import { cn } from "@/lib/utils";

/** Sonuç türü etiketi; kişisel kayıtlar (notum, vurgum) ayrı görünür. */
export function KindBadge({ kind, className }: { kind: SearchKind; className?: string }) {
  const personal = kind === "note" || kind === "highlight" || kind === "document" || kind === "voiceNote";
  return (
    <Badge
      variant={personal ? "outline" : "secondary"}
      className={cn(personal && "border-primary/40 text-primary", className)}
    >
      {searchKindLabels[kind]}
    </Badge>
  );
}
