import Link from "next/link";
import type { ArticleLinkDto, LinkedArticleRef } from "@/api/generated/model";
import { ParagraphList } from "./article-text";

function Target({ link }: { link: ArticleLinkDto }) {
  return link.article ? <ArticleLink article={link.article} /> : <span className="font-medium">{link.externalLabel}</span>;
}

function ArticleLink({ article }: { article: LinkedArticleRef }) {
  return (
    <Link href={`/${article.lawSlug}/${article.articleSlug}`} className="font-medium text-primary underline-offset-2 hover:underline">
      {article.lawLabel} Madde {article.number}
      {article.title && <span className="font-normal text-foreground"> — {article.title}</span>}
    </Link>
  );
}

/**
 * İlgili Maddeler sekmesi: iki yönlü ilgili maddeler, (eski kanun maddesinde) yeni kanundaki karşılığı ve metninde
 * bu maddeye atıf yapan maddeler.
 */
export function RelatedLinks({ links, citedBy }: { links: ArticleLinkDto[]; citedBy: LinkedArticleRef[] }) {
  const related = links.filter((l) => l.kind === "ilgili");
  const newer = links.filter((l) => l.kind === "eskiKanunKarsiligi" && l.incoming);
  if (related.length === 0 && newer.length === 0 && citedBy.length === 0) {
    return <p className="text-muted-foreground">Bu maddeye henüz ilgili madde bağlanmadı.</p>;
  }
  return (
    <div className="space-y-4">
      {related.length > 0 && (
        <ul className="space-y-2">
          {related.map((link) => (
            <li key={link.id}>
              <Target link={link} />
              {link.note && <p className="text-sm text-muted-foreground">{link.note}</p>}
            </li>
          ))}
        </ul>
      )}
      {newer.length > 0 && (
        <section>
          <h4 className="mb-2 text-sm font-medium text-muted-foreground">Yeni kanundaki karşılığı</h4>
          <ul className="space-y-2">
            {newer.map((link) => (
              <li key={link.id}>
                <Target link={link} />
                {link.note && <p className="text-sm text-muted-foreground">{link.note}</p>}
              </li>
            ))}
          </ul>
        </section>
      )}
      {citedBy.length > 0 && (
        <section>
          <h4 className="mb-2 text-sm font-medium text-muted-foreground">Bu maddeye atıf yapan maddeler</h4>
          <ul className="space-y-2">
            {citedBy.map((article) => (
              <li key={article.articleId}>
                <ArticleLink article={article} />
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}

/** Eski kanun karşılığı sekmesi (TCK'da 765 sayılı Kanun): karşılık maddenin adı ve kütüphanedeyse metni. */
export function OldLawCounterparts({ links }: { links: ArticleLinkDto[] }) {
  const counterparts = links.filter((l) => l.kind === "eskiKanunKarsiligi" && !l.incoming);
  if (counterparts.length === 0) {
    return <p className="text-muted-foreground">Bu maddenin eski kanundaki karşılığı henüz girilmedi.</p>;
  }
  return (
    <div className="divide-y">
      {counterparts.map((link) => (
        <article key={link.id} className="py-4 first:pt-0 last:pb-0">
          <h4 className="text-lg">
            <Target link={link} />
          </h4>
          {link.note && <p className="text-sm text-muted-foreground">{link.note}</p>}
          {link.targetVersion && link.targetVersion.paragraphs.length > 0 && (
            <div className="mt-3 rounded-md border bg-muted/30 p-3">
              <ParagraphList paragraphs={link.targetVersion.paragraphs} />
            </div>
          )}
        </article>
      ))}
    </div>
  );
}
