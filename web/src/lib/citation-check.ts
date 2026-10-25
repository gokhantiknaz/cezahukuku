import type { CheckSeverity, CitationCheckResult, CitationIssue } from "@/api/generated/model";

export const severityOrder: CheckSeverity[] = ["error", "warning", "info", "ok"];

export const severityLabels: Record<CheckSeverity, string> = {
  error: "Hata",
  warning: "Uyarı",
  info: "Bilgi",
  ok: "Sorunsuz",
};

/** Metindeki bir atıf (madde ya da künye); aynı atıfın tekrarları tek grupta toplanır. */
export type CitationGroup = {
  key: string;
  kind: "article" | "decision";
  label: string;
  severity: CheckSeverity;
  issues: CitationIssue[];
  occurrences: { start: number; length: number }[];
};

export type TextMark = { start: number; length: number; key: string; severity: CheckSeverity };

/** Atıfları gruplar: en ağır bulgusu olan önce, eşitse metinde önce geçen önce. */
export function groupCitations(result: CitationCheckResult): CitationGroup[] {
  const groups = new Map<string, CitationGroup>();
  const add = (kind: CitationGroup["kind"], c: { key: string; label: string; severity: CheckSeverity; issues: CitationIssue[]; start: number; length: number }) => {
    const key = `${kind === "article" ? "m" : "k"}:${c.key}`;
    const group = groups.get(key);
    if (group) {
      group.occurrences.push({ start: c.start, length: c.length });
      // Aynı künyenin tekrarı farklı tarih ya da daireyle yazılmış olabilir: bulgular birleşir.
      for (const issue of c.issues) {
        if (!group.issues.some((i) => i.code === issue.code && i.message === issue.message)) group.issues.push(issue);
      }
      group.severity = heavier(group.severity, c.severity);
      return;
    }
    groups.set(key, { key, kind, label: c.label, severity: c.severity, issues: [...c.issues], occurrences: [{ start: c.start, length: c.length }] });
  };
  result.articles.forEach((a) => add("article", a));
  result.decisions.forEach((d) => add("decision", d));
  return [...groups.values()].sort(
    (a, b) => severityOrder.indexOf(a.severity) - severityOrder.indexOf(b.severity) || a.occurrences[0].start - b.occurrences[0].start,
  );
}

/** Metinde işaretlenecek yerler, sırayla; her yer kendi grubunun (tüm tekrarların) ağırlığıyla boyanır. */
export function textMarks(groups: CitationGroup[]): TextMark[] {
  return groups
    .flatMap((g) => g.occurrences.map((o) => ({ ...o, key: g.key, severity: g.severity })))
    .sort((a, b) => a.start - b.start);
}

export function countBySeverity(groups: CitationGroup[]) {
  const counts: Record<CheckSeverity, number> = { error: 0, warning: 0, info: 0, ok: 0 };
  for (const g of groups) counts[g.severity]++;
  return counts;
}

function heavier(a: CheckSeverity, b: CheckSeverity) {
  return severityOrder.indexOf(a) <= severityOrder.indexOf(b) ? a : b;
}

/** Belgelerim'den "Atıfları denetle" ile gelen metin sayfalar arasında oturum deposunda taşınır. */
export const handoffKey = "atif-denetimi:metin";
