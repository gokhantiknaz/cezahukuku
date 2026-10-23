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
      content={<WelcomeView />}
      notes={<NotesPanel />}
    />
  );
}

async function LibraryNavigation() {
  const library = await getLibrary();
  return <LibraryPanel groups={library.groups} tree={null} />;
}
