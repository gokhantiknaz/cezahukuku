import { Suspense } from "react";
import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { ArticleSkeleton } from "@/components/workspace/article-view";
import { LawIntro } from "@/components/workspace/law-intro";
import { getLaw } from "@/lib/server-api";

export async function generateMetadata({ params }: PageProps<"/[law]">): Promise<Metadata> {
  const { law: lawSlug } = await params;
  const tree = await getLaw(lawSlug);
  if (!tree) return { title: "Kanun bulunamadı" };

  const { law } = tree;
  return {
    title: law.shortName ? `${law.shortName} — ${law.name}` : law.name,
    description: `${law.number} sayılı ${law.name}: maddeler, gerekçeler ve içtihatlar.`,
    robots: law.visibility === "private" ? { index: false } : undefined,
  };
}

export default function LawPage({ params }: PageProps<"/[law]">) {
  return (
    <Suspense fallback={<ArticleSkeleton />}>
      <LawOverview params={params} />
    </Suspense>
  );
}

async function LawOverview({ params }: { params: Promise<{ law: string }> }) {
  const { law: lawSlug } = await params;
  const tree = await getLaw(lawSlug);
  if (!tree) notFound();
  return <LawIntro tree={tree} />;
}
