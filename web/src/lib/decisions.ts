import type { Court, DecisionDto, NormReviewOutcome } from "@/api/generated/model";
import { formatDate } from "./dates";

export const courtLabels: Record<Court, string> = {
  yargitay: "Yargıtay",
  anayasaMahkemesi: "Anayasa Mahkemesi",
  aihm: "Avrupa İnsan Hakları Mahkemesi",
  danistay: "Danıştay",
  bolgeAdliyeMahkemesi: "Bölge Adliye Mahkemesi",
  uyusmazlikMahkemesi: "Uyuşmazlık Mahkemesi",
  diger: "Diğer",
};

export const outcomeLabels: Record<NormReviewOutcome, string> = {
  iptal: "İptal",
  kismenIptal: "Kısmen iptal",
  ret: "Ret",
  diger: "Diğer",
};

/** Atıf biçiminde karar künyesi: "Yargıtay Ceza Genel Kurulu, 12.03.2020, E. 2019/1-100, K. 2020/1". */
export function decisionCitation(d: Pick<DecisionDto, "court" | "chamber" | "decisionDate" | "caseNumber" | "decisionNumber">) {
  const court = [courtLabels[d.court], d.chamber].filter(Boolean).join(" ");
  const parts = [
    court,
    d.decisionDate ? formatDate(d.decisionDate) : null,
    d.caseNumber ? `E. ${d.caseNumber}` : null,
    d.decisionNumber ? `K. ${d.decisionNumber}` : null,
  ];
  return parts.filter(Boolean).join(", ");
}

/** İptal (kuralın yürürlükten kalkması) öne çıkar; ret ve diğerleri sakin görünür. */
export const outcomeBadgeVariant = (outcome: NormReviewOutcome) =>
  outcome === "iptal" || outcome === "kismenIptal" ? ("default" as const) : ("secondary" as const);
