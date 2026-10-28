import { Suspense } from "react";
import { WelcomeView } from "@/components/workspace/article-view";
import { LibraryPanel, LibraryPanelSkeleton } from "@/components/workspace/library-panel";
import { NotesPanel } from "@/components/workspace/notes-panel";
import { Workspace } from "@/components/workspace/workspace";
import { getLibrary } from "@/lib/server-api";

export default function HomePage() {
  return (
    <Workspace
      navigation={
        <Suspense fallback={<LibraryPanelSkeleton />}>
          <LibraryNavigation />
        </Suspense>
      }
      content={
        <>
          {/* Telefonda solda panel yok: kütüphane doğrudan ana sayfada. */}
          <div className="lg:hidden">
            <Suspense fallback={<LibraryPanelSkeleton />}>
              <LibraryNavigation />
            </Suspense>
          </div>
          <div className="h-full max-lg:hidden">
            <WelcomeView />
          </div>
        </>
      }
      notes={<NotesPanel />}
    />
  );
}

async function LibraryNavigation() {
  const library = await getLibrary();
  return <LibraryPanel groups={library.groups} tree={null} />;
}
