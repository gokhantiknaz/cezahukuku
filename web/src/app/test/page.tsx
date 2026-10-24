import { Suspense } from "react";
import type { Metadata } from "next";
import { QuizRunner } from "@/components/content/quiz-runner";
import { getLibrary } from "@/lib/server-api";

export const metadata: Metadata = {
  title: "Kendini Test Et",
  description: "Ceza hukuku bilginizi çoktan seçmeli sorularla sınayın.",
};

export default function QuizPage() {
  return (
    <div className="mx-auto w-full max-w-3xl px-4 py-6">
      <h1 className="text-3xl">Kendini Test Et</h1>
      <p className="mt-1 text-muted-foreground">Rastgele sorular; her yanıttan sonra doğru şık, açıklama ve ilgili madde.</p>
      <Suspense fallback={<p className="mt-6 text-muted-foreground">Yükleniyor…</p>}>
        <Quiz />
      </Suspense>
    </div>
  );
}

async function Quiz() {
  const library = await getLibrary();
  return <QuizRunner laws={library.groups.flatMap((g) => g.laws)} />;
}
