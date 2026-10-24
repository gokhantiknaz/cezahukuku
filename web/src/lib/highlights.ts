import type { CurrentUserResponse, HighlightColor } from "@/api/generated/model";
import { Roles } from "./roles";

// Kişisel vurgu renkleri (globals.css'teki --highlight-* değişkenleri).
export const highlightColors: { key: HighlightColor; label: string; className: string }[] = [
  { key: "yellow", label: "Sarı", className: "bg-highlight-yellow" },
  { key: "green", label: "Yeşil", className: "bg-highlight-green" },
  { key: "blue", label: "Mavi", className: "bg-highlight-blue" },
  { key: "orange", label: "Turuncu", className: "bg-highlight-orange" },
];

export const highlightClassName = (color: HighlightColor) =>
  highlightColors.find((c) => c.key === color)?.className ?? "bg-highlight-yellow";

/** Kişisel katman (vurgu, not) super_admin ve standard rolüne açık; reader yalnız okur. */
export function canAnnotate(user: CurrentUserResponse | null | undefined) {
  return user?.roles.some((role) => role === Roles.SuperAdmin || role === Roles.Standard) ?? false;
}
