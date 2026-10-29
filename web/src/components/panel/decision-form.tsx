"use client";

import { useState, type ReactNode } from "react";
import { toast } from "sonner";
import { useCreateDecision, useGetDecision, useUpdateDecision } from "@/api/generated/admin-laws/admin-laws";
import type { AdminDecisionDetail, Court, DecisionDto, DecisionKind, NormReviewOutcome, Visibility } from "@/api/generated/model";
import { RichTextEditor } from "@/components/rich-text/rich-text-editor";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { courtLabels, outcomeLabels } from "@/lib/decisions";
import { DecisionArticleSuggestions } from "./decision-article-suggestions";
import { emptyToNull, Field, FormMessages, OptionSelect, otherMessages, visibilityLabels } from "./form";
import { useLibraryRefresh } from "./use-library-refresh";

const kindLabels: Record<DecisionKind, string> = {
  ictihat: "İçtihat (Yargıtay, AİHM, BAM…)",
  normDenetimi: "Norm denetimi (AYM iptal/ret)",
};

const chamberSuggestions = [
  "Ceza Genel Kurulu",
  "Büyük Genel Kurul",
  "1. Ceza Dairesi",
  "2. Ceza Dairesi",
  "3. Ceza Dairesi",
  "4. Ceza Dairesi",
  "5. Ceza Dairesi",
  "Genel Kurul",
  "Büyük Daire",
  "İkinci Bölüm",
];

const decisionFields = [
  "court",
  "chamber",
  "caseNumber",
  "decisionNumber",
  "outcome",
  "gazetteNumber",
  "summary",
  "html",
  "sourceUrl",
];

/**
 * Karar ekleme/düzenleme penceresi. `decisionId` verilirse karar yüklenip düzenlenir; verilmezse yeni karar
 * `articleIds` maddelerine bağlanarak oluşturulur.
 */
export function DecisionDialog({
  decisionId,
  articleIds,
  trigger,
  onSaved,
}: {
  decisionId?: string;
  articleIds?: string[];
  trigger: ReactNode;
  onSaved?: (decision: AdminDecisionDetail) => void;
}) {
  const [open, setOpen] = useState(false);
  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>{decisionId ? "Kararı düzenle" : "Yeni karar"}</DialogTitle>
          <DialogDescription>
            Karar bir kez girilir; aynı karar birden çok maddeye bağlanabilir.
          </DialogDescription>
        </DialogHeader>
        {open &&
          (decisionId ? (
            <LoadedDecisionForm
              decisionId={decisionId}
              onDone={(saved) => {
                setOpen(false);
                if (saved) onSaved?.(saved);
              }}
            />
          ) : (
            <DecisionForm
              articleIds={articleIds}
              onDone={(saved) => {
                setOpen(false);
                if (saved) onSaved?.(saved);
              }}
            />
          ))}
      </DialogContent>
    </Dialog>
  );
}

function LoadedDecisionForm({ decisionId, onDone }: { decisionId: string; onDone: (saved?: AdminDecisionDetail) => void }) {
  const query = useGetDecision(decisionId);
  if (query.data?.status !== 200) return <p className="text-muted-foreground">Yükleniyor…</p>;
  const { decision, articles } = query.data.data;
  return (
    <>
      {articles.length > 0 && (
        <p className="text-sm text-muted-foreground">
          Bağlı maddeler: {articles.map((a) => `${a.lawLabel} ${a.number}`).join(", ")}
        </p>
      )}
      <DecisionForm decision={decision} onDone={onDone} />
    </>
  );
}

function DecisionForm({
  decision,
  articleIds,
  onDone,
}: {
  decision?: DecisionDto;
  articleIds?: string[];
  onDone: (saved?: AdminDecisionDetail) => void;
}) {
  const refresh = useLibraryRefresh();
  const [form, setForm] = useState({
    kind: decision?.kind ?? ("ictihat" as DecisionKind),
    court: decision?.court ?? ("yargitay" as Court),
    chamber: decision?.chamber ?? "",
    caseNumber: decision?.caseNumber ?? "",
    decisionNumber: decision?.decisionNumber ?? "",
    decisionDate: decision?.decisionDate ?? "",
    outcome: decision?.outcome ?? ("iptal" as NormReviewOutcome),
    gazetteDate: decision?.gazetteDate ?? "",
    gazetteNumber: decision?.gazetteNumber ?? "",
    summary: decision?.summary ?? "",
    html: decision?.html ?? "",
    sourceUrl: decision?.sourceUrl ?? "",
    visibility: decision?.visibility ?? ("public" as Visibility),
  });
  const set = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) =>
    setForm((f) => ({ ...f, [key]: e.target.value }));
  // Metindeki atıflardan seçilen maddeler; kaydedince bağlanır (güncellemede var olan bağlantılara eklenir).
  const [suggested, setSuggested] = useState<string[]>([]);
  const create = useCreateDecision();
  const update = useUpdateDecision();
  const error = create.error ?? update.error;
  const isReview = form.kind === "normDenetimi";

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const data = {
      court: form.court,
      chamber: emptyToNull(form.chamber),
      kind: form.kind,
      caseNumber: emptyToNull(form.caseNumber),
      decisionNumber: emptyToNull(form.decisionNumber),
      decisionDate: emptyToNull(form.decisionDate),
      outcome: isReview ? form.outcome : null,
      gazetteDate: isReview ? emptyToNull(form.gazetteDate) : null,
      gazetteNumber: isReview ? emptyToNull(form.gazetteNumber) : null,
      summary: emptyToNull(form.summary),
      html: emptyToNull(form.html),
      sourceUrl: emptyToNull(form.sourceUrl),
      visibility: form.visibility,
      articleIds: [...new Set([...(decision ? [] : (articleIds ?? [])), ...suggested])],
    };
    const saved = decision
      ? await update.mutateAsync({ id: decision.id, data })
      : await create.mutateAsync({ data });
    await refresh();
    toast.success(decision ? "Karar güncellendi." : "Karar eklendi.");
    onDone(saved.status === 200 || saved.status === 201 ? saved.data : undefined);
  }

  return (
    <form onSubmit={(e) => submit(e).catch(() => {})} className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Tür" htmlFor="decision-kind">
          <OptionSelect
            id="decision-kind"
            value={form.kind}
            options={kindLabels}
            // Norm denetimi Anayasa Mahkemesine aittir; tür seçilince mahkeme de ayarlanır.
            onChange={(kind) =>
              setForm((f) => ({ ...f, kind, court: kind === "normDenetimi" ? "anayasaMahkemesi" : f.court }))
            }
          />
        </Field>
        <Field label="Mahkeme" htmlFor="decision-court" error={error} field="court">
          <OptionSelect
            id="decision-court"
            value={form.court}
            options={courtLabels}
            onChange={(court) => setForm((f) => ({ ...f, court }))}
          />
        </Field>
      </div>
      <div className="grid gap-4 sm:grid-cols-[1fr_9rem_9rem_10rem]">
        <Field label="Daire / kurul" htmlFor="decision-chamber" error={error} field="chamber">
          <Input id="decision-chamber" value={form.chamber} onChange={set("chamber")} list="chamber-suggestions" />
          <datalist id="chamber-suggestions">
            {chamberSuggestions.map((c) => (
              <option key={c} value={c} />
            ))}
          </datalist>
        </Field>
        <Field label="Esas no" htmlFor="decision-case" error={error} field="caseNumber">
          <Input id="decision-case" value={form.caseNumber} onChange={set("caseNumber")} placeholder="2019/1-100" />
        </Field>
        <Field label="Karar no" htmlFor="decision-number" error={error} field="decisionNumber">
          <Input id="decision-number" value={form.decisionNumber} onChange={set("decisionNumber")} placeholder="2020/123" />
        </Field>
        <Field label="Karar tarihi" htmlFor="decision-date">
          <Input id="decision-date" type="date" value={form.decisionDate} onChange={set("decisionDate")} />
        </Field>
      </div>
      {isReview && (
        <div className="grid gap-4 sm:grid-cols-3">
          <Field label="Sonuç" htmlFor="decision-outcome" error={error} field="outcome">
            <OptionSelect
              id="decision-outcome"
              value={form.outcome}
              options={outcomeLabels}
              onChange={(outcome) => setForm((f) => ({ ...f, outcome }))}
            />
          </Field>
          <Field label="RG tarihi" htmlFor="decision-gazette-date">
            <Input id="decision-gazette-date" type="date" value={form.gazetteDate} onChange={set("gazetteDate")} />
          </Field>
          <Field label="RG sayısı" htmlFor="decision-gazette-number" error={error} field="gazetteNumber">
            <Input id="decision-gazette-number" value={form.gazetteNumber} onChange={set("gazetteNumber")} />
          </Field>
        </div>
      )}
      <Field label="Özet" htmlFor="decision-summary" error={error} field="summary" hint="Listede görünen kısa özet.">
        <Textarea id="decision-summary" value={form.summary} onChange={set("summary")} className="min-h-24" />
      </Field>
      <div>
        <p className="mb-1.5 text-sm font-medium">Kararın metni (isteğe bağlı)</p>
        <RichTextEditor value={form.html} onChange={(html) => setForm((f) => ({ ...f, html }))} label="Kararın metni" />
      </div>
      <DecisionArticleSuggestions
        text={`${form.summary}\n${form.html}`}
        decisionId={decision?.id}
        fixedIds={decision ? [] : (articleIds ?? [])}
        selected={suggested}
        onChange={setSuggested}
      />
      <div className="grid gap-4 sm:grid-cols-[1fr_16rem]">
        <Field label="Kaynak bağlantısı" htmlFor="decision-source" error={error} field="sourceUrl">
          <Input
            id="decision-source"
            value={form.sourceUrl}
            onChange={set("sourceUrl")}
            placeholder="https://karararama.yargitay.gov.tr/…"
          />
        </Field>
        <Field label="Görünürlük" htmlFor="decision-visibility">
          <OptionSelect
            id="decision-visibility"
            value={form.visibility}
            options={visibilityLabels}
            onChange={(visibility) => setForm((f) => ({ ...f, visibility }))}
          />
        </Field>
      </div>
      <FormMessages messages={otherMessages(error, decisionFields)} />
      <DialogFooter>
        <Button type="submit" disabled={create.isPending || update.isPending}>
          {decision ? "Kaydet" : "Kararı ekle"}
        </Button>
      </DialogFooter>
    </form>
  );
}
