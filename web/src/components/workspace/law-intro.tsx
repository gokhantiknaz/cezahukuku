import Link from "next/link";
import { ArrowRight, Lock } from "lucide-react";
import type { ArticleSummary, LawGroupWithLaws, LawNodeTree, LawTreeResponse } from "@/api/generated/model";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { formatDate } from "@/lib/dates";
import { LibraryPanel } from "./library-panel";

function firstArticle(tree: LawTreeResponse): ArticleSummary | undefined {
  const inNodes = (nodes: LawNodeTree[]): ArticleSummary | undefined => {
    for (const node of nodes) {
      const found = node.articles[0] ?? inNodes(node.children);
      if (found) return found;
    }
  };
  return tree.articles[0] ?? inNodes(tree.nodes);
}

/**
 * Kanun açılıp henüz madde seçilmediğinde orta alanda gösterilen künye. Telefonda solda panel olmadığından
 * kanunun ağacı da hemen altında gösterilir.
 */
export function LawIntro({ tree, groups }: { tree: LawTreeResponse; groups: LawGroupWithLaws[] }) {
  const { law } = tree;
  const first = firstArticle(tree);
  const facts = [
    { label: "Kanun numarası", value: law.number },
    { label: "Kabul tarihi", value: law.acceptedOn ? formatDate(law.acceptedOn) : null },
    {
      label: "Resmî Gazete",
      value: [law.gazetteDate ? formatDate(law.gazetteDate) : null, law.gazetteNumber && `Sayı ${law.gazetteNumber}`]
        .filter(Boolean)
        .join(" · "),
    },
  ].filter((fact) => fact.value);

  return (
    <div className="mx-auto max-w-3xl px-4 py-6 sm:px-6 sm:py-10">
      <h1 className="flex flex-wrap items-center gap-3 text-3xl">
        {law.name}
        {law.visibility === "private" && (
          <Badge variant="outline" className="text-sm font-normal" title="Yalnız giriş yapan kullanıcılar görür">
            <Lock /> Özel
          </Badge>
        )}
      </h1>
      {law.shortName && <p className="mt-1 text-xl text-primary">{law.shortName}</p>}

      <dl className="mt-4 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 sm:mt-6 sm:gap-x-6 sm:gap-y-2">
        {facts.map((fact) => (
          <div key={fact.label} className="contents">
            <dt className="text-muted-foreground">{fact.label}</dt>
            <dd>{fact.value}</dd>
          </div>
        ))}
      </dl>

      <p className="reading mt-6 text-muted-foreground max-lg:hidden">
        Soldaki ağaçtan bir madde seçin ya da ağacın üstündeki kutuya madde numarası veya başlığı yazın.
      </p>
      <p className="reading mt-6 text-muted-foreground lg:hidden">
        Aşağıdaki ağaçtan bir madde seçin ya da kutuya madde numarası veya başlığı yazın.
      </p>
      {first && (
        <Button asChild className="mt-4">
          <Link href={`/${law.slug}/${first.slug}`}>
            Madde {first.number} ile başla <ArrowRight />
          </Link>
        </Button>
      )}

      <div className="-mx-4 mt-6 border-y bg-sidebar sm:-mx-6 lg:hidden">
        <LibraryPanel groups={groups} tree={tree} />
      </div>
    </div>
  );
}
