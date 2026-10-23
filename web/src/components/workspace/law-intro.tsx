import Link from "next/link";
import { ArrowRight, Lock } from "lucide-react";
import type { ArticleSummary, LawNodeTree, LawTreeResponse } from "@/api/generated/model";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { formatDate } from "@/lib/dates";

function firstArticle(tree: LawTreeResponse): ArticleSummary | undefined {
  const inNodes = (nodes: LawNodeTree[]): ArticleSummary | undefined => {
    for (const node of nodes) {
      const found = node.articles[0] ?? inNodes(node.children);
      if (found) return found;
    }
  };
  return tree.articles[0] ?? inNodes(tree.nodes);
}

/** Kanun açılıp henüz madde seçilmediğinde orta alanda gösterilen künye. */
export function LawIntro({ tree }: { tree: LawTreeResponse }) {
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
    <div className="mx-auto max-w-3xl px-6 py-10">
      <h1 className="flex flex-wrap items-center gap-3 text-3xl">
        {law.name}
        {law.visibility === "private" && (
          <Badge variant="outline" className="text-sm font-normal" title="Yalnız giriş yapan kullanıcılar görür">
            <Lock /> Özel
          </Badge>
        )}
      </h1>
      {law.shortName && <p className="mt-1 text-xl text-primary">{law.shortName}</p>}

      <dl className="mt-6 grid gap-x-6 gap-y-2 sm:grid-cols-[auto_1fr]">
        {facts.map((fact) => (
          <div key={fact.label} className="contents">
            <dt className="text-muted-foreground">{fact.label}</dt>
            <dd>{fact.value}</dd>
          </div>
        ))}
      </dl>

      <p className="reading mt-6 text-muted-foreground">
        Soldaki ağaçtan bir madde seçin ya da ağacın üstündeki kutuya madde numarası veya başlığı yazın.
      </p>
      {first && (
        <Button asChild className="mt-4">
          <Link href={`/${law.slug}/${first.slug}`}>
            Madde {first.number} ile başla <ArrowRight />
          </Link>
        </Button>
      )}
    </div>
  );
}
