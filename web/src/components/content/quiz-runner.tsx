"use client";

import { useState } from "react";
import Link from "next/link";
import { Check, RotateCcw, X } from "lucide-react";
import { getQuiz } from "@/api/generated/quiz/quiz";
import type { LawSummary, QuizQuestionDto } from "@/api/generated/model";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

const COUNTS = [5, 10, 20];

/**
 * Kendini Test Et: kanun ve soru sayısı seçilir, rastgele sorular tek tek sorulur. Yanıttan sonra doğru şık,
 * açıklama ve ilgili madde gösterilir; sonda puan.
 */
export function QuizRunner({ laws }: { laws: LawSummary[] }) {
  const [lawSlug, setLawSlug] = useState("");
  const [count, setCount] = useState(10);
  const [questions, setQuestions] = useState<QuizQuestionDto[] | null>(null);
  const [index, setIndex] = useState(0);
  const [answers, setAnswers] = useState<number[]>([]);
  const [loading, setLoading] = useState(false);

  async function start() {
    setLoading(true);
    try {
      const response = await getQuiz({ law: lawSlug || undefined, count });
      setQuestions(response.status === 200 ? response.data : []);
      setIndex(0);
      setAnswers([]);
    } finally {
      setLoading(false);
    }
  }

  if (!questions) {
    return (
      <div className="mt-5 space-y-4 rounded-lg border bg-card p-5">
        <label className="block space-y-1.5">
          <span className="font-medium">Kanun</span>
          <select
            value={lawSlug}
            onChange={(e) => setLawSlug(e.target.value)}
            className="block h-10 w-full rounded-md border bg-background px-3"
          >
            <option value="">Bütün kanunlar</option>
            {laws.map((law) => (
              <option key={law.id} value={law.slug}>
                {law.shortName || law.number} — {law.name}
              </option>
            ))}
          </select>
        </label>
        <fieldset className="space-y-1.5">
          <legend className="font-medium">Soru sayısı</legend>
          <div className="flex gap-2">
            {COUNTS.map((c) => (
              <Button key={c} type="button" variant={count === c ? "default" : "outline"} onClick={() => setCount(c)} aria-pressed={count === c}>
                {c}
              </Button>
            ))}
          </div>
        </fieldset>
        <Button size="lg" onClick={start} disabled={loading}>
          {loading ? "Sorular hazırlanıyor…" : "Teste başla"}
        </Button>
      </div>
    );
  }

  if (questions.length === 0) {
    return (
      <div className="mt-5 rounded-lg border bg-card p-5">
        <p>Bu seçim için henüz soru eklenmedi.</p>
        <Button className="mt-3" variant="outline" onClick={() => setQuestions(null)}>
          Geri dön
        </Button>
      </div>
    );
  }

  const finished = index >= questions.length;
  const correct = answers.filter((a, i) => a === questions[i].correctIndex).length;

  if (finished) {
    return (
      <div className="mt-5 space-y-4 rounded-lg border bg-card p-5">
        <p className="text-2xl">
          {questions.length} sorudan <span className="font-bold text-primary">{correct}</span> doğru
        </p>
        <ol className="space-y-2">
          {questions.map((q, i) => {
            const right = answers[i] === q.correctIndex;
            return (
              <li key={q.id} className="flex gap-2">
                {right ? <Check className="mt-0.5 size-5 shrink-0 text-green-700" /> : <X className="mt-0.5 size-5 shrink-0 text-destructive" />}
                <span>
                  {q.text}
                  {!right && <span className="block text-sm text-muted-foreground">Doğru yanıt: {q.options[q.correctIndex]}</span>}
                  {q.article && (
                    <Link href={`/${q.article.lawSlug}/${q.article.articleSlug}`} className="block text-sm text-primary hover:underline">
                      {q.article.lawLabel} Madde {q.article.number} — {q.article.title}
                    </Link>
                  )}
                </span>
              </li>
            );
          })}
        </ol>
        <div className="flex gap-2">
          <Button onClick={start}>
            <RotateCcw /> Yeni test
          </Button>
          <Button variant="outline" onClick={() => setQuestions(null)}>
            Ayarları değiştir
          </Button>
        </div>
      </div>
    );
  }

  const question = questions[index];
  const answer = answers[index];
  const answered = answer !== undefined;
  return (
    <div className="mt-5 space-y-4 rounded-lg border bg-card p-5">
      <div className="flex items-center justify-between text-sm text-muted-foreground">
        <span>
          Soru {index + 1} / {questions.length} · {question.lawLabel}
        </span>
        <span>{correct} doğru</span>
      </div>
      <div className="h-1.5 overflow-hidden rounded-full bg-muted">
        <div className="h-full bg-primary transition-all" style={{ width: `${(index / questions.length) * 100}%` }} />
      </div>
      <p className="reading text-xl">{question.text}</p>
      <ul className="space-y-2" role="radiogroup" aria-label="Şıklar">
        {question.options.map((option, i) => {
          const isCorrect = i === question.correctIndex;
          const chosen = answer === i;
          return (
            <li key={i}>
              <button
                type="button"
                role="radio"
                aria-checked={chosen}
                disabled={answered}
                onClick={() => setAnswers((a) => [...a.slice(0, index), i])}
                className={cn(
                  "flex w-full items-center gap-3 rounded-md border px-4 py-3 text-left text-base",
                  !answered && "hover:border-primary hover:bg-accent",
                  answered && isCorrect && "border-green-700 bg-highlight-green",
                  answered && chosen && !isCorrect && "border-destructive bg-accent",
                )}
              >
                <span className="flex size-7 shrink-0 items-center justify-center rounded-full border font-medium">
                  {String.fromCharCode(65 + i)}
                </span>
                <span className="flex-1">{option}</span>
                {answered && isCorrect && <Check className="size-5 text-green-700" aria-label="Doğru" />}
                {answered && chosen && !isCorrect && <X className="size-5 text-destructive" aria-label="Yanlış" />}
              </button>
            </li>
          );
        })}
      </ul>
      {answered && (
        <div className="space-y-2 rounded-md bg-muted/50 p-3">
          <p className="font-medium">{answer === question.correctIndex ? "Doğru!" : "Yanlış."}</p>
          {question.explanation && <p className="reading text-[16px] whitespace-pre-wrap">{question.explanation}</p>}
          {question.article && (
            <Link href={`/${question.article.lawSlug}/${question.article.articleSlug}`} target="_blank" className="text-sm text-primary hover:underline">
              {question.article.lawLabel} Madde {question.article.number} — {question.article.title} (yeni sekmede)
            </Link>
          )}
        </div>
      )}
      <div className="flex justify-end">
        <Button disabled={!answered} onClick={() => setIndex(index + 1)}>
          {index + 1 === questions.length ? "Sonucu gör" : "Sonraki soru"}
        </Button>
      </div>
    </div>
  );
}
