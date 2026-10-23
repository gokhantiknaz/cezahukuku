import { Suspense } from "react";
import { notFound } from "next/navigation";
import { LibraryPanel, LibraryPanelSkeleton } from "@/components/workspace/library-panel";
import { NotesPanel } from "@/components/workspace/notes-panel";
import { Workspace } from "@/components/workspace/workspace";
import { getLaw, getLibrary } from "@/lib/server-api";

/**
 * Kanun sayfaları (/tck, /tck/madde-81) için ortak çalışma alanı. Layout maddeler arasında gezinirken yeniden
 * kurulmaz: sol ağacın açık düğümleri, kaydırma konumu ve panel genişlikleri korunur, yalnız orta içerik değişir.
 */
export default function LawLayout({ children, params }: LayoutProps<"/[law]">) {
  return (
    <Workspace
      navigation={
        // Ağaç oturum çerezine göre (private içerik) sunucuda çekildiğinden Suspense içinde akar.
        <Suspense fallback={<LibraryPanelSkeleton />}>
          <LawNavigation params={params} />
        </Suspense>
      }
      content={children}
      notes={<NotesPanel />}
    />
  );
}

async function LawNavigation({ params }: { params: Promise<{ law: string }> }) {
  const { law: lawSlug } = await params;
  const [library, tree] = await Promise.all([getLibrary(), getLaw(lawSlug)]);
  if (!tree) notFound();
  return <LibraryPanel groups={library.groups} tree={tree} />;
}
