"use client";

import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import Link from "next/link";
import { AlertCircle, AlertTriangle, CheckCircle2, ExternalLink, FileSearch, FileUp, Info } from "lucide-react";
import { toast } from "sonner";
import { useCheckCitations, useExtractText } from "@/api/generated/tools/tools";
import type { CheckSeverity, CitationCheckResult, CitationIssue } from "@/api/generated/model";
import { FormMessages, otherMessages } from "@/components/panel/form";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useHydrated } from "@/hooks/use-hydrated";
import { useCurrentUser } from "@/lib/auth";
import {
  countBySeverity,
  groupCitations,
  handoffKey,
  severityLabels,
  severityOrder,
  textMarks,
  type CitationGroup,
} from "@/lib/citation-check";
import { formatDate } from "@/lib/dates";
import { cn } from "@/lib/utils";

const sample = `Sanık hakkında TCK m. 82/1-a uyarınca hüküm kurulmuş, ayrıca TCK 53/1-c uygulanmıştır. CMK 231/5 koşulları değerlendirilmemiştir.

Yargıtay Ceza Genel Kurulunun 15.10.2019 tarihli ve 2017/1-123 E., 2019/456 K. sayılı kararında ve Yargıtay 1. CD, 12.03.2020, 2018/1234 E., 2019/567 K. sayılı kararında da belirtildiği üzere tasarlama için kararın önceden verilmesi gerekir.`;

/**
 * Atıf denetimi: metindeki madde atıfları ve karar künyeleri kütüphaneye göre denetlenir. Sonuçta solda metin
 * (atıflar bulgunun ağırlığına göre boyalı), sağda atıf başına bulgular; birine tıklanınca öbürü ona kayar.
 */
export function CitationCheck() {
  const hydrated = useHydrated();
  const { data: user } = useCurrentUser();
  const [text, setText] = useState("");
  const [date, setDate] = useState("");
  const [checked, setChecked] = useState<{ text: string; date: string; result: CitationCheckResult } | null>(null);
  const check = useCheckCitations();
  const extract = useExtractText();
  const fileRef = useRef<HTMLInputElement>(null);

  // Belgelerim'deki "Atıfları denetle" metni oturum deposuyla getirir (adres çubuğuna sığmaz).
  useEffect(() => {
    let handed: string | null = null;
    try {
      handed = sessionStorage.getItem(handoffKey);
      sessionStorage.removeItem(handoffKey);
    } catch {
      // Depo kapalıysa (gizli pencere) metin elle yapıştırılır.
    }
    // eslint-disable-next-line react-hooks/set-state-in-effect -- tarayıcı deposu yalnız istemcide okunabilir
    if (handed) setText(handed);
  }, []);

  async function run() {
    const response = await check.mutateAsync({ data: { text, referenceDate: date || null } });
    if (response.status !== 200) return;
    setChecked({ text, date, result: response.data });
  }

  async function loadFile(file: File) {
    try {
      const response = await extract.mutateAsync({ data: { file } });
      if (response.status !== 200) return;
      setText(response.data.text);
      toast.success(`“${file.name}” metni alındı.`);
    } catch (error) {
      toast.error(otherMessages(error, [])[0] ?? "Dosya okunamadı.");
    } finally {
      if (fileRef.current) fileRef.current.value = "";
    }
  }

  return (
    <div className="mt-4">
      <form
        className="space-y-3 rounded-lg border bg-card p-4"
        onSubmit={(e) => {
          e.preventDefault();
          run().catch(() => {});
        }}
      >
        <Textarea
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="Dilekçe, karar ya da makale metnini buraya yapıştırın…"
          className="h-56 font-reading text-base md:text-base"
          aria-label="Denetlenecek metin"
        />
        <div className="flex flex-wrap items-end gap-3">
          <div className="space-y-1">
            <Label htmlFor="olay-tarihi">Olay (suç) tarihi</Label>
            <Input id="olay-tarihi" type="date" value={date} onChange={(e) => setDate(e.target.value)} className="w-44" />
          </div>
          <p className="max-w-sm pb-1 text-sm text-muted-foreground">
            İsteğe bağlı: verilirse maddeler o tarihteki metne göre denetlenir, sonradan değişenler işaretlenir.
          </p>
          <div className="ml-auto flex flex-wrap gap-2">
            {hydrated && user && (
              <>
                <input
                  ref={fileRef}
                  type="file"
                  accept=".docx,.pdf,.txt"
                  className="hidden"
                  onChange={(e) => e.target.files?.[0] && loadFile(e.target.files[0])}
                />
                <Button type="button" variant="outline" disabled={extract.isPending} onClick={() => fileRef.current?.click()}>
                  <FileUp /> {extract.isPending ? "Okunuyor…" : "Dosyadan al"}
                </Button>
              </>
            )}
            <Button type="button" variant="ghost" onClick={() => setText(sample)}>
              Örnek metin
            </Button>
            <Button type="submit" disabled={!text.trim() || check.isPending}>
              <FileSearch /> {check.isPending ? "Denetleniyor…" : "Denetle"}
            </Button>
          </div>
        </div>
        <FormMessages messages={otherMessages(check.error, [])} />
      </form>

      {checked && (
        <>
          {(checked.text !== text || checked.date !== date) && (
            <p className="mt-4 text-sm text-muted-foreground">Metin ya da tarih değişti; güncel sonuç için yeniden denetleyin.</p>
          )}
          <Results key={JSON.stringify(checked)} text={checked.text} date={checked.date} result={checked.result} />
        </>
      )}

      <p className="mt-8 border-t pt-3 text-sm text-muted-foreground">
        Denetim yalnız kütüphanedeki kayıtlara göre yapılır ve yapay zekâ kullanmaz. Kütüphanede bulunamayan künye yanlış
        demek değildir; resmî karar bankasından doğrulayıp kararın tam metnini okuyun. Sonuçlar hukuki görüş değildir.
        Metniniz saklanmaz.
      </p>
    </div>
  );
}

function Results({ text, date, result }: { text: string; date: string; result: CitationCheckResult }) {
  const groups = useMemo(() => groupCitations(result), [result]);
  const marks = useMemo(() => textMarks(groups), [groups]);
  const [active, setActive] = useState<string | null>(null);
  const counts = countBySeverity(groups);
  const indexOf = (key: string) => groups.findIndex((g) => g.key === key);

  function select(key: string, scrollTo: "text" | "list") {
    setActive(key);
    const id = scrollTo === "list" ? `grup-${indexOf(key)}` : `isaret-${indexOf(key)}-0`;
    document.getElementById(id)?.scrollIntoView({ behavior: "smooth", block: "center" });
  }

  if (groups.length === 0) {
    return (
      <p className="mt-6 text-muted-foreground">
        Metinde madde atıfı (“TCK m. 81/2”, “5271 sayılı Kanunun 231 inci maddesi”) ya da karar künyesi (“Yargıtay 1. CD,
        12.03.2019, 2018/1234 E., 2019/567 K.”) bulunamadı.
      </p>
    );
  }

  return (
    <section className="mt-6" aria-label="Denetim sonucu">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="mr-2 text-xl">
          {groups.length} atıf denetlendi{date && <span className="text-muted-foreground"> · olay tarihi {formatDate(date)}</span>}
        </h2>
        {severityOrder
          .filter((s) => counts[s] > 0)
          .map((s) => (
            <span key={s} className={cn("inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5 text-sm", severityStyles[s].chip)}>
              <SeverityIcon severity={s} /> {counts[s]} {severityLabels[s].toLocaleLowerCase("tr")}
            </span>
          ))}
      </div>

      <div className="mt-4 grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,30rem)]">
        <div className="reading max-h-[75vh] overflow-y-auto whitespace-pre-wrap rounded-lg border bg-card p-4" aria-label="Metin">
          <MarkedText text={text} marks={marks} groups={groups} active={active} onSelect={(key) => select(key, "list")} />
        </div>
        <ol className="max-h-[75vh] space-y-3 overflow-y-auto pr-1">
          {groups.map((group, index) => (
            <GroupCard
              key={group.key}
              id={`grup-${index}`}
              group={group}
              active={active === group.key}
              onSelect={() => select(group.key, "text")}
            />
          ))}
        </ol>
      </div>
    </section>
  );
}

function MarkedText({
  text,
  marks,
  groups,
  active,
  onSelect,
}: {
  text: string;
  marks: ReturnType<typeof textMarks>;
  groups: CitationGroup[];
  active: string | null;
  onSelect: (key: string) => void;
}) {
  const parts: ReactNode[] = [];
  const seen = new Map<string, number>();
  let position = 0;
  for (const mark of marks) {
    if (mark.start < position) continue;
    if (mark.start > position) parts.push(text.slice(position, mark.start));
    const occurrence = seen.get(mark.key) ?? 0;
    seen.set(mark.key, occurrence + 1);
    const group = groups.findIndex((g) => g.key === mark.key);
    parts.push(
      <mark
        key={mark.start}
        id={`isaret-${group}-${occurrence}`}
        role="button"
        tabIndex={0}
        title={severityLabels[mark.severity]}
        onClick={() => onSelect(mark.key)}
        onKeyDown={(e) => (e.key === "Enter" || e.key === " ") && (e.preventDefault(), onSelect(mark.key))}
        className={cn(
          "cursor-pointer rounded-sm px-0.5 text-inherit underline decoration-2 underline-offset-4",
          severityStyles[mark.severity].mark,
          active === mark.key && "ring-2 ring-primary",
        )}
      >
        {text.slice(mark.start, mark.start + mark.length)}
      </mark>,
    );
    position = mark.start + mark.length;
  }
  parts.push(text.slice(position));
  return <>{parts}</>;
}

function GroupCard({ id, group, active, onSelect }: { id: string; group: CitationGroup; active: boolean; onSelect: () => void }) {
  return (
    <li id={id} className={cn("scroll-mt-2 rounded-lg border bg-card p-3", active && "ring-2 ring-primary")}>
      <button type="button" onClick={onSelect} className="flex w-full items-start gap-2 text-left" title="Metinde göster">
        <SeverityIcon severity={group.severity} className="mt-1 size-4" />
        <span className="font-medium">{group.label}</span>
        {group.occurrences.length > 1 && <Badge variant="secondary">×{group.occurrences.length}</Badge>}
        <Badge variant="outline" className="ml-auto shrink-0">
          {group.kind === "article" ? "Madde" : "Künye"}
        </Badge>
      </button>
      <ul className="mt-2 space-y-1.5 text-sm">
        {group.issues.map((issue) => (
          <li key={issue.code + issue.message} className="flex gap-2">
            <SeverityIcon severity={issue.severity} className="mt-0.5 size-3.5" />
            <span>
              {issue.message} {issue.url && <IssueLink issue={issue} />}
            </span>
          </li>
        ))}
      </ul>
    </li>
  );
}

const linkLabels: Record<string, string> = {
  dogrulanacak: "Resmî karar bankası",
  sonradan_degisti: "O tarihteki metin",
  mulga_tarihte: "O tarihteki metin",
  degisiklik: "Değişiklikler",
  karardan_sonra_degisti: "Değişiklikler",
  aym_iptal: "Norm denetimi",
  bulundu: "Kararı aç",
  tarih_farkli: "Kararı aç",
  daire_farkli: "Kararı aç",
  benzer: "Benzer kararı aç",
  kanun_bos: "Kanunu aç",
  madde_yok: "Kanunu aç",
};

function IssueLink({ issue }: { issue: CitationIssue }) {
  const url = issue.url!;
  const label = linkLabels[issue.code] ?? "Maddeyi aç";
  const className = "inline-flex items-center gap-1 whitespace-nowrap text-primary underline-offset-2 hover:underline";
  if (url.startsWith("/")) {
    return (
      <Link href={url} target="_blank" className={className}>
        {label} →
      </Link>
    );
  }
  return (
    <a href={url} target="_blank" rel="noopener noreferrer nofollow" className={className}>
      {label} <ExternalLink className="size-3" />
    </a>
  );
}

const severityStyles: Record<CheckSeverity, { chip: string; mark: string; icon: string }> = {
  error: { chip: "border-red-200 bg-red-50 text-red-800", mark: "bg-red-100 decoration-red-600", icon: "text-red-600" },
  warning: { chip: "border-amber-200 bg-amber-50 text-amber-900", mark: "bg-amber-100 decoration-amber-500", icon: "text-amber-600" },
  info: { chip: "border-sky-200 bg-sky-50 text-sky-900", mark: "bg-sky-100 decoration-sky-500", icon: "text-sky-600" },
  ok: { chip: "border-emerald-200 bg-emerald-50 text-emerald-900", mark: "bg-emerald-100 decoration-emerald-500", icon: "text-emerald-600" },
};

function SeverityIcon({ severity, className }: { severity: CheckSeverity; className?: string }) {
  const Icon = { error: AlertCircle, warning: AlertTriangle, info: Info, ok: CheckCircle2 }[severity];
  return <Icon className={cn("size-4 shrink-0", severityStyles[severity].icon, className)} aria-label={severityLabels[severity]} />;
}
