import { Suspense } from "react";
import type { Metadata } from "next";
import { LectureNoteList, NewLectureNoteButton } from "@/components/content/lecture-notes";
import { getLectureNotes } from "@/lib/server-api";

export const metadata: Metadata = {
  title: "Ders Notları",
  description: "Ceza hukuku ders notları: genel konular, kanun grupları, kanunlar ve maddeler üzerine anlatımlar.",
};

export default function LectureNotesPage({ searchParams }: PageProps<"/ders-notlari">) {
  return (
    <div className="mx-auto w-full max-w-3xl px-4 py-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-3xl">Ders Notları</h1>
          <p className="mt-1 text-muted-foreground">Ceza hukuku konu anlatımları; kanun ya da maddeye bağlı olanlar da burada.</p>
        </div>
        <NewLectureNoteButton />
      </div>
      {/* Giriş yapan kullanıcı üyelere özel notları da görür (oturum çerezi çalışma anında okunur). */}
      <Suspense fallback={<p className="mt-6 text-muted-foreground">Yükleniyor…</p>}>
        <Notes searchParams={searchParams} />
      </Suspense>
    </div>
  );
}

/** ?kanun=tck ya da ?grup=temel-ceza-kanunlari ile süzülmüş açılır. */
async function Notes({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const [notes, query] = await Promise.all([getLectureNotes(), searchParams]);
  const first = (value: string | string[] | undefined) => (Array.isArray(value) ? value[0] : value);
  const law = first(query.kanun);
  const group = first(query.grup);
  const scope = law ? `kanun:${law}` : group ? `grup:${group}` : "";
  return <LectureNoteList notes={notes} initialScope={scope} />;
}
