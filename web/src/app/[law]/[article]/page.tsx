import { Suspense } from "react";
import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { ArticleSkeleton, ArticleView } from "@/components/workspace/article-view";
import { getArticle, parseDateParam } from "@/lib/server-api";

export async function generateMetadata({ params }: PageProps<"/[law]/[article]">): Promise<Metadata> {
  const { law: lawSlug, article: articleSlug } = await params;
  const detail = await getArticle(lawSlug, articleSlug);
  if (!detail) return { title: "Madde bulunamadı" };

  const { law, article, version } = detail;
  const firstParagraph = version?.paragraphs[0]?.text ?? "";
  return {
    title: `${law.shortName || law.number} Madde ${article.number} — ${article.title}`,
    description: firstParagraph.length > 160 ? `${firstParagraph.slice(0, 157)}…` : firstParagraph || undefined,
    robots: article.effectiveVisibility === "private" ? { index: false } : undefined,
  };
}

export default function ArticlePage({ params, searchParams }: PageProps<"/[law]/[article]">) {
  return (
    <Suspense fallback={<ArticleSkeleton />}>
      <ArticleContent params={params} searchParams={searchParams} />
    </Suspense>
  );
}

async function ArticleContent({
  params,
  searchParams,
}: {
  params: Promise<{ law: string; article: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { law: lawSlug, article: articleSlug } = await params;
  // ?tarih=2010-03-15 → o gün yürürlükte olan metin (suç tarihindeki metin).
  const asOf = parseDateParam((await searchParams).tarih);
  const detail = await getArticle(lawSlug, articleSlug, asOf);
  if (!detail) notFound();
  return <ArticleView detail={detail} asOf={asOf} />;
}
