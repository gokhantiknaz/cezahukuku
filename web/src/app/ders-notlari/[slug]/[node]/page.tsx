import { Suspense } from "react";
import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { NodeView } from "@/components/content/course-view";
import { getLectureCourse, getLectureNote } from "@/lib/server-api";

/** Başlık bu dersin başlığı değilse (yanlış ders adresi, düz not, gizli ya da silinmiş) yok sayılır. */
async function loadNode(params: Promise<{ slug: string; node: string }>) {
  const { slug, node } = await params;
  const [course, note] = await Promise.all([getLectureCourse(slug), getLectureNote(node)]);
  return course && note?.node && note.node.course.slug === slug ? { note, context: note.node, course } : null;
}

export async function generateMetadata({ params }: PageProps<"/ders-notlari/[slug]/[node]">): Promise<Metadata> {
  const found = await loadNode(params);
  if (!found) return { title: "Başlık bulunamadı" };
  const { note, context } = found;
  return {
    title: `${context.label} ${note.title} — ${context.course.title}`,
    robots: context.effectiveVisibility === "private" ? { index: false } : undefined,
  };
}

export default function LectureNodePage({ params }: PageProps<"/ders-notlari/[slug]/[node]">) {
  return (
    <Suspense fallback={<p className="mx-auto w-full max-w-3xl px-4 py-6 text-muted-foreground">Yükleniyor…</p>}>
      <Node params={params} />
    </Suspense>
  );
}

async function Node({ params }: { params: Promise<{ slug: string; node: string }> }) {
  const found = await loadNode(params);
  if (!found) notFound();
  return <NodeView note={found.note} context={found.context} course={found.course} />;
}
