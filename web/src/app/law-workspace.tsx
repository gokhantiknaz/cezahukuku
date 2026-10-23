"use client";

import { useState } from "react";
import { ArticleView, WelcomeView } from "@/components/workspace/article-view";
import { LibraryPanel } from "@/components/workspace/library-panel";
import { NotesPanel } from "@/components/workspace/notes-panel";
import { Workspace } from "@/components/workspace/workspace";
import { demoGroups, demoTrees, getDemoArticle } from "@/lib/demo-library";

// GEÇİCİ: seçim istemci durumunda ve veri demo kesitinden. M1'de kanun/madde URL'leri
// (ör. /tck/madde-81) ve API verisiyle değiştirilecek.
export function LawWorkspace() {
  const [lawId, setLawId] = useState<string | null>("tck");
  const [articleId, setArticleId] = useState<string | null>("tck-81");

  const tree = lawId ? (demoTrees[lawId] ?? emptyTree(lawId)) : null;
  const article = articleId ? getDemoArticle(articleId) : null;

  return (
    <Workspace
      selectionKey={articleId ?? undefined}
      navigation={
        <LibraryPanel
          groups={demoGroups}
          tree={tree}
          selectedArticleId={articleId}
          onOpenLaw={setLawId}
          onBackToLibrary={() => setLawId(null)}
          onSelectArticle={setArticleId}
        />
      }
      content={article ? <ArticleView article={article} /> : <WelcomeView />}
      notes={<NotesPanel />}
    />
  );
}

function emptyTree(lawId: string) {
  const law = demoGroups.flatMap((g) => g.laws).find((l) => l.id === lawId);
  return law ? { ...law, nodes: [] } : null;
}
