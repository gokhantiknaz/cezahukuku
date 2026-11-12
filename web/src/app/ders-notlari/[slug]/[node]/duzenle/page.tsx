import { Suspense } from "react";
import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { EditLectureNote } from "@/components/content/lecture-note-form";
import { getLectureNote } from "@/lib/server-api";

export const metadata: Metadata = { title: "Başlığı düzenle", robots: { index: false } };

/** Ders başlığının metni, belgeleri, görünürlüğü ve madde bağlantısı; yeri "Taşı" ile değişir. */
export default function Page({ params }: PageProps<"/ders-notlari/[slug]/[node]/duzenle">) {
  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-6">
      <h1 className="text-3xl">Başlığı düzenle</h1>
      {/* Adres parametresi çalışma anında okunur. */}
      <Suspense fallback={<p className="mt-6 text-muted-foreground">Yükleniyor…</p>}>
        <Editor params={params} />
      </Suspense>
    </div>
  );
}

async function Editor({ params }: { params: Promise<{ slug: string; node: string }> }) {
  const { slug, node } = await params;
  // Başlık adresteki dersin başlığı değilse (yanlış ders adresi, düz not, silinmiş) okuma sayfası gibi bulunamadı.
  const note = await getLectureNote(node);
  if (!note?.node || note.node.course.slug !== slug) notFound();
  return <EditLectureNote slug={node} />;
}
