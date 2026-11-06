import { Suspense } from "react";
import type { Metadata } from "next";
import { EditLectureNote } from "@/components/content/lecture-note-form";

export const metadata: Metadata = { title: "Ders notunu düzenle", robots: { index: false } };

export default function Page({ params }: PageProps<"/ders-notlari/[slug]/duzenle">) {
  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-6">
      <h1 className="text-3xl">Ders notunu düzenle</h1>
      {/* Adres parametresi çalışma anında okunur. */}
      <Suspense fallback={<p className="mt-6 text-muted-foreground">Yükleniyor…</p>}>
        <Editor params={params} />
      </Suspense>
    </div>
  );
}

async function Editor({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  return <EditLectureNote slug={slug} />;
}
