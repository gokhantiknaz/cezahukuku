import { Suspense } from "react";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArticleSkeleton } from "@/components/workspace/article-view";
import { NoteList } from "@/components/workspace/note-list";
import { ScrollArea } from "@/components/ui/scroll-area";
import { getLaw, getLawNotes } from "@/lib/server-api";

export const lawNoteSections = {
  genelGerekce: { slug: "genel-gerekce", title: "Genel Gerekçe", empty: "Bu kanunun genel gerekçesi henüz girilmedi." },
  dersNotu: { slug: "ders-notlari", title: "Ders Notları", empty: "Bu kanuna henüz ders notu girilmedi." },
} as const;

type LawNoteKind = keyof typeof lawNoteSections;

/** Kanun düzeyindeki notlar sayfası (/tck/genel-gerekce, /tck/ders-notlari); kanun çalışma alanının içinde açılır. */
export function LawNotesPage({ params, kind }: { params: Promise<{ law: string }>; kind: LawNoteKind }) {
  return (
    <Suspense fallback={<ArticleSkeleton />}>
      <LawNotesContent params={params} kind={kind} />
    </Suspense>
  );
}

async function LawNotesContent({ params, kind }: { params: Promise<{ law: string }>; kind: LawNoteKind }) {
  const { law: lawSlug } = await params;
  const [tree, notes] = await Promise.all([getLaw(lawSlug), getLawNotes(lawSlug, kind)]);
  if (!tree || !notes) notFound();
  const section = lawNoteSections[kind];

  return (
    <ScrollArea className="h-full">
      <article className="mx-auto max-w-5xl px-4 py-5 sm:px-6 lg:pr-14">
        <p className="text-sm text-muted-foreground">
          <Link href={`/${tree.law.slug}`} className="hover:underline">
            {tree.law.shortName || tree.law.number}
          </Link>{" "}
          › {section.title}
        </p>
        <h1 className="mt-3 text-4xl leading-tight">{section.title}</h1>
        <h2 className="text-2xl leading-tight text-primary">{tree.law.name}</h2>
        <section className="mt-5 rounded-lg border bg-card p-4">
          <NoteList notes={notes} empty={section.empty} />
        </section>
      </article>
    </ScrollArea>
  );
}
