import { Suspense } from "react";
import type { Metadata } from "next";
import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { ChevronLeft, Lock } from "lucide-react";
import { CourseHome, LectureNoteScopeBadges } from "@/components/content/course-view";
import { LectureNoteFileList } from "@/components/content/lecture-note-files";
import { LectureNoteAdminActions } from "@/components/content/lecture-notes";
import { RichTextView } from "@/components/rich-text/rich-text-view";
import { Badge } from "@/components/ui/badge";
import { formatDate } from "@/lib/dates";
import { getLectureCourse, getLectureNote } from "@/lib/server-api";

/** Adres bir ders ya da düz ders notudur (ikisi aynı adres alanında, tekil). */
async function load(slug: string) {
  const [course, note] = await Promise.all([getLectureCourse(slug), getLectureNote(slug)]);
  return { course, note };
}

export async function generateMetadata({ params }: PageProps<"/ders-notlari/[slug]">): Promise<Metadata> {
  const { slug } = await params;
  const { course, note } = await load(slug);
  if (course) {
    return {
      title: `${course.title} — Ders Notları`,
      description: course.description || undefined,
      robots: course.visibility === "private" ? { index: false } : undefined,
    };
  }
  if (!note) return { title: "Ders notu bulunamadı" };
  return { title: `${note.title} — Ders Notları`, robots: note.visibility === "private" ? { index: false } : undefined };
}

export default function LectureSlugPage({ params }: PageProps<"/ders-notlari/[slug]">) {
  return (
    <Suspense fallback={<p className="mx-auto w-full max-w-3xl px-4 py-6 text-muted-foreground">Yükleniyor…</p>}>
      <SlugContent params={params} />
    </Suspense>
  );
}

async function SlugContent({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const { course, note } = await load(slug);
  if (course) return <CourseHome course={course} />;
  if (!note) notFound();
  // Bir dersin başlığı her zaman dersteki adresinde okunur (içindekiler, ders yolu, önceki/sonraki).
  if (note.node) redirect(`/ders-notlari/${note.node.course.slug}/${note.slug}`);

  return (
    <div className="mx-auto w-full max-w-3xl px-4 py-6">
      <Link href="/ders-notlari" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-primary print:hidden">
        <ChevronLeft className="size-4" aria-hidden /> Ders Notları
      </Link>
      <article>
        <header className="mt-2 flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0">
            <h1 className="text-3xl leading-tight md:text-4xl">{note.title}</h1>
            <p className="mt-2 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
              {/* Bağlantı: kanun grubu → kanun → madde; hiçbiri yoksa genel ders notu. */}
              <LectureNoteScopeBadges scope={note.scope} general />
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
        {note.html && <RichTextView html={note.html} className="mt-6 rounded-lg border bg-card p-4 md:p-6" />}
        <LectureNoteFileList files={note.files} />
      </article>
    </div>
  );
}
