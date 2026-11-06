import { Suspense } from "react";
import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ChevronLeft, Lock } from "lucide-react";
import { LectureNoteAdminActions } from "@/components/content/lecture-notes";
import { RichTextView } from "@/components/rich-text/rich-text-view";
import { Badge } from "@/components/ui/badge";
import { formatDate } from "@/lib/dates";
import { getLectureNote } from "@/lib/server-api";

export async function generateMetadata({ params }: PageProps<"/ders-notlari/[slug]">): Promise<Metadata> {
  const { slug } = await params;
  const note = await getLectureNote(slug);
  if (!note) return { title: "Ders notu bulunamadı" };
  return { title: `${note.title} — Ders Notları`, robots: note.visibility === "private" ? { index: false } : undefined };
}

export default function LectureNotePage({ params }: PageProps<"/ders-notlari/[slug]">) {
  return (
    <div className="mx-auto w-full max-w-3xl px-4 py-6">
      <Link href="/ders-notlari" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-primary print:hidden">
        <ChevronLeft className="size-4" aria-hidden /> Ders Notları
      </Link>
      <Suspense fallback={<p className="mt-6 text-muted-foreground">Yükleniyor…</p>}>
        <Note params={params} />
      </Suspense>
    </div>
  );
}

async function Note({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const note = await getLectureNote(slug);
  if (!note) notFound();
  const { group, law, article } = note.scope;

  return (
    <article>
      <header className="mt-2 flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h1 className="text-3xl leading-tight md:text-4xl">{note.title}</h1>
          <p className="mt-2 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
            {/* Bağlantı: kanun grubu → kanun → madde; hiçbiri yoksa genel ders notu. */}
            {!group && !law && <Badge variant="secondary">Genel</Badge>}
            {group && (
              <Link href={`/ders-notlari?grup=${group.slug}`}>
                <Badge variant="secondary">{group.title}</Badge>
              </Link>
            )}
            {law && (
              <Link href={`/${law.slug}`} title={law.name}>
                <Badge variant="outline">{law.label}</Badge>
              </Link>
            )}
            {article && law && (
              <Link href={`/${law.slug}/${article.articleSlug}`} className="text-primary hover:underline">
                Madde {article.number}
                {article.title ? ` — ${article.title}` : ""}
              </Link>
            )}
            {note.visibility === "private" && (
              <Badge variant="outline">
                <Lock /> Üyelere
              </Badge>
            )}
            <span>Güncelleme: {formatDate(note.updatedAt.slice(0, 10))}</span>
          </p>
        </div>
        <LectureNoteAdminActions id={note.id} slug={note.slug} />
      </header>
      <RichTextView html={note.html} className="mt-6 rounded-lg border bg-card p-4 md:p-6" />
    </article>
  );
}
