import { Suspense } from "react";
import type { Metadata } from "next";
import { DocumentViewer } from "@/components/attachments/document-viewer";

export const metadata: Metadata = { title: "Belge", robots: { index: false } };

export default function Page({ params }: PageProps<"/belgelerim/[id]">) {
  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-6">
      {/* Adres parametresi ve ?sayfa= çalışma anında okunur. */}
      <Suspense fallback={<p className="text-muted-foreground">Yükleniyor…</p>}>
        <Viewer params={params} />
      </Suspense>
    </div>
  );
}

async function Viewer({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <DocumentViewer id={id} />;
}
