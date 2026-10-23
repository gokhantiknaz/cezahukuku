import Link from "next/link";
import type { ArticleLinkDto } from "@/api/generated/model";
import { ParagraphList } from "./article-text";

function Target({ link }: { link: ArticleLinkDto }) {
  if (!link.article) return <span className="font-medium">{link.externalLabel}</span>;
  const { lawSlug, lawLabel, articleSlug, number, title } = link.article;
  return (
    <Link href={`/${lawSlug}/${articleSlug}`} className="font-medium text-primary underline-offset-2 hover:underline">
      {lawLabel} Madde {number}
      {title && <span className="font-normal text-foreground"> — {title}</span>}
    </Link>
  );
}

/** İlgili Maddeler sekmesi: iki yönlü ilgili maddeler ve (eski kanun maddesinde) yeni kanundaki karşılığı. */
export function RelatedLinks({ links }: { links: ArticleLinkDto[] }) {
  const related = links.filter((l) => l.kind === "ilgili");
  const newer = links.filter((l) => l.kind === "eskiKanunKarsiligi" && l.incoming);
  if (related.length === 0 && newer.length === 0) {
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
