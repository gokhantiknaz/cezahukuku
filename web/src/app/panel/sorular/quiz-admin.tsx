"use client";

import { useState, type FormEvent, type ReactNode } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Check, Lock, Pencil, Plus, Trash2, X } from "lucide-react";
import { toast } from "sonner";
import { useSearchArticles } from "@/api/generated/admin-laws/admin-laws";
import { useGetLibrary } from "@/api/generated/library/library";
import {
  getListQuizQuestionsQueryKey,
  useCreateQuizQuestion,
  useDeleteQuizQuestion,
  useListQuizQuestions,
  useUpdateQuizQuestion,
} from "@/api/generated/quiz/quiz";
import type { LawSummary, QuizQuestionDto, Visibility } from "@/api/generated/model";
import { ConfirmDelete } from "@/components/panel/confirm-delete";
import { emptyToNull, Field, fieldMessages, FormMessages, OptionSelect, otherMessages, visibilityLabels } from "@/components/panel/form";
import { FormDialog } from "@/components/panel/law-dialogs";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { DialogFooter } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { useDebouncedValue } from "@/hooks/use-debounced-value";

/** Kendini Test Et soruları: çoktan seçmeli, kanuna (isteğe bağlı maddeye) bağlı. */
export function QuizAdmin() {
  const queryClient = useQueryClient();
  const { data: library } = useGetLibrary();
  const laws = library?.data.groups.flatMap((g) => g.laws) ?? [];
  const [lawId, setLawId] = useState("");
  const { data } = useListQuizQuestions(lawId ? { lawId } : undefined);
  const remove = useDeleteQuizQuestion();
  const questions = data?.status === 200 ? data.data : null;
  const refresh = () => queryClient.invalidateQueries({ queryKey: getListQuizQuestionsQueryKey().slice(0, 1) });

  return (
    <div className="mt-4 space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <select value={lawId} onChange={(e) => setLawId(e.target.value)} aria-label="Kanun" className="h-9 w-full min-w-0 rounded-md border bg-card px-3 sm:w-auto sm:max-w-md">
          <option value="">Bütün kanunlar</option>
          {laws.map((law) => (
            <option key={law.id} value={law.id}>
              {law.shortName || law.number} — {law.name}
            </option>
          ))}
        </select>
        {laws.length > 0 && (
          <QuestionDialog
            laws={laws}
            defaultLawId={lawId || laws[0].id}
            onSaved={refresh}
            trigger={
              <Button>
                <Plus /> Soru ekle
              </Button>
            }
          />
        )}
      </div>
      {!questions ? (
        <p className="text-muted-foreground">Yükleniyor…</p>
      ) : questions.length === 0 ? (
        <p className="rounded-lg border bg-card p-4 text-muted-foreground">Henüz soru yok.</p>
      ) : (
        <ul className="divide-y rounded-lg border bg-card">
          {questions.map((q) => (
            <li key={q.id} className="flex flex-wrap items-start gap-3 px-4 py-3">
              <div className="min-w-0 flex-1">
                <p className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
                  {q.lawLabel}
                  {q.article && <span>Madde {q.article.number}</span>}
                  {q.visibility === "private" && (
                    <Badge variant="outline">
                      <Lock /> Özel
                    </Badge>
                  )}
                </p>
                <p className="font-medium">{q.text}</p>
                <ul className="mt-1 space-y-0.5 text-sm">
                  {q.options.map((option, i) => (
                    <li key={i} className="flex items-center gap-1.5">
                      {i === q.correctIndex ? <Check className="size-4 text-green-700" /> : <X className="size-4 text-muted-foreground/50" />}
                      {option}
                    </li>
                  ))}
                </ul>
              </div>
              <div className="flex gap-1">
                <QuestionDialog
                  laws={laws}
                  question={q}
                  onSaved={refresh}
                  trigger={
                    <Button variant="ghost" size="icon-sm" aria-label="Soruyu düzenle" title="Soruyu düzenle">
                      <Pencil />
                    </Button>
                  }
                />
                <ConfirmDelete
                  title="Soru silinsin mi?"
                  description={q.text}
                  onConfirm={async () => {
                    await remove.mutateAsync({ id: q.id });
                    await refresh();
                    toast.success("Soru silindi.");
                  }}
                >
                  <Button variant="ghost" size="icon-sm" aria-label="Soruyu sil" title="Soruyu sil">
                    <Trash2 />
                  </Button>
                </ConfirmDelete>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function QuestionDialog({
  laws,
  question,
  defaultLawId,
  onSaved,
  trigger,
}: {
  laws: LawSummary[];
  question?: QuizQuestionDto;
  defaultLawId?: string;
  onSaved: () => Promise<unknown>;
  trigger: ReactNode;
}) {
  return (
    <FormDialog title={question ? "Soruyu düzenle" : "Yeni soru"} trigger={trigger}>
      {(close) => (
        <QuestionForm
          laws={laws}
          question={question}
          defaultLawId={defaultLawId}
          onDone={async () => {
            await onSaved();
            close();
          }}
        />
      )}
    </FormDialog>
  );
}

function QuestionForm({
  laws,
  question,
  defaultLawId,
  onDone,
}: {
  laws: LawSummary[];
  question?: QuizQuestionDto;
  defaultLawId?: string;
  onDone: () => Promise<void>;
}) {
  const [lawId, setLawId] = useState(question?.lawId ?? defaultLawId ?? laws[0]?.id ?? "");
  const [article, setArticle] = useState(question?.article ? { id: question.article.articleId, label: `Madde ${question.article.number} — ${question.article.title}` } : null);
  const [text, setText] = useState(question?.text ?? "");
  const [options, setOptions] = useState<string[]>(question?.options ?? ["", "", "", ""]);
  const [correctIndex, setCorrectIndex] = useState(question?.correctIndex ?? 0);
  const [explanation, setExplanation] = useState(question?.explanation ?? "");
  const [visibility, setVisibility] = useState<Visibility>(question?.visibility ?? "public");
  const create = useCreateQuizQuestion();
  const update = useUpdateQuizQuestion();
  const error = create.error ?? update.error;
  const law = laws.find((l) => l.id === lawId);

  async function submit(event: FormEvent) {
    event.preventDefault();
    const filled = options.map((o) => o.trim());
    const data = {
      lawId,
      articleId: article?.id ?? null,
      text,
      options: filled.filter((o) => o.length > 0),
      // Boş şıklar atılınca doğru şıkkın sırası kayar.
      correctIndex: filled.slice(0, correctIndex).filter((o) => o.length > 0).length,
      explanation: emptyToNull(explanation),
      visibility,
    };
    if (question) await update.mutateAsync({ id: question.id, data });
    else await create.mutateAsync({ data });
    toast.success(question ? "Soru güncellendi." : "Soru eklendi.");
    await onDone();
  }

  return (
    <form onSubmit={(e) => submit(e).catch(() => {})} className="max-h-[70vh] space-y-4 overflow-y-auto pr-1">
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Kanun" htmlFor="quiz-law" error={error} field="lawId">
          <select
            id="quiz-law"
            value={lawId}
            onChange={(e) => {
              setLawId(e.target.value);
              setArticle(null);
            }}
            className="h-9 w-full rounded-md border bg-background px-3"
          >
            {laws.map((l) => (
              <option key={l.id} value={l.id}>
                {l.shortName || l.number} — {l.name}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Görünürlük" htmlFor="quiz-visibility">
          <OptionSelect id="quiz-visibility" value={visibility} options={visibilityLabels} onChange={setVisibility} />
        </Field>
      </div>
      <Field label="Madde (isteğe bağlı)" htmlFor="quiz-article" error={error} field="articleId">
        {article ? (
          <div className="flex items-center justify-between gap-2 rounded-md border px-3 py-1.5 text-sm">
            {article.label}
            <Button type="button" size="xs" variant="ghost" onClick={() => setArticle(null)}>
              Kaldır
            </Button>
          </div>
        ) : (
          <ArticlePicker lawSlug={law?.slug} lawLabel={law?.shortName || law?.number} onPick={setArticle} />
        )}
      </Field>
      <Field label="Soru" htmlFor="quiz-text" error={error} field="text">
        <Textarea id="quiz-text" value={text} onChange={(e) => setText(e.target.value)} rows={3} required autoFocus />
      </Field>
      <fieldset className="space-y-2">
        <legend className="text-sm font-medium">Şıklar (doğru olanı işaretleyin; boş şıklar atılır)</legend>
        {options.map((option, i) => (
          <div key={i} className="flex items-center gap-2">
            <input
              type="radio"
              name="correct"
              checked={correctIndex === i}
              onChange={() => setCorrectIndex(i)}
              aria-label={`${String.fromCharCode(65 + i)} doğru`}
              className="size-4 accent-primary"
            />
            <Input
              value={option}
              onChange={(e) => setOptions((o) => o.map((x, j) => (j === i ? e.target.value : x)))}
              placeholder={`${String.fromCharCode(65 + i)} şıkkı`}
              maxLength={500}
            />
          </div>
        ))}
        {options.length < 6 && (
          <Button type="button" size="sm" variant="ghost" onClick={() => setOptions((o) => [...o, ""])}>
            <Plus /> Şık ekle
          </Button>
        )}
        {["options", "correctIndex"].flatMap((field) => fieldMessages(error, field)).map((message) => (
          <p key={message} className="text-xs text-destructive">
            {message}
          </p>
        ))}
      </fieldset>
      <Field label="Açıklama (yanıttan sonra gösterilir)" htmlFor="quiz-explanation" error={error} field="explanation">
        <Textarea id="quiz-explanation" value={explanation} onChange={(e) => setExplanation(e.target.value)} rows={3} />
      </Field>
      <FormMessages messages={otherMessages(error, ["lawId", "articleId", "text", "options", "correctIndex", "explanation"])} />
      <DialogFooter>
        <Button type="submit" disabled={create.isPending || update.isPending}>
          Kaydet
        </Button>
      </DialogFooter>
    </form>
  );
}

/** Madde seçici: "81" ya da "kasten" yazınca seçilen kanunun maddeleri listelenir. */
function ArticlePicker({
  lawSlug,
  lawLabel,
  onPick,
}: {
  lawSlug?: string;
  lawLabel?: string;
  onPick: (article: { id: string; label: string }) => void;
}) {
  const [text, setText] = useState("");
  const query = useDebouncedValue(text.trim(), 250);
  // Numara yazılınca "TCK 81" göndermesi (doğrudan madde), sözcük yazılınca başlıkta arama; sonuç seçili kanuna süzülür.
  const q = /^\d/.test(query) && lawLabel ? `${lawLabel} ${query}` : query;
  const { data } = useSearchArticles({ q }, { query: { enabled: query.length > 0 } });
  const hits = data?.status === 200 ? data.data.filter((h) => h.lawSlug === lawSlug).slice(0, 8) : [];
  return (
    <div>
      <Input id="quiz-article" value={text} onChange={(e) => setText(e.target.value)} placeholder="Madde numarası ya da başlığı" />
      {hits.length > 0 && (
        <ul className="mt-1 max-h-48 overflow-y-auto rounded-md border bg-popover text-sm">
          {hits.map((hit) => (
            <li key={hit.articleId}>
              <button
                type="button"
                className="block w-full px-3 py-1.5 text-left hover:bg-accent"
                onClick={() => onPick({ id: hit.articleId, label: `Madde ${hit.number} — ${hit.title}` })}
              >
                Madde {hit.number} — {hit.title}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
