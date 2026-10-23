"use client";

import { Fragment } from "react";
import Link from "next/link";
import { ChevronLeft, ChevronRight, Copy, FileDown, FileText, Lock, Pencil, Printer, Share2, Star } from "lucide-react";
import { toast } from "sonner";
import type { ArticleDetailResponse, ArticleRef } from "@/api/generated/model";
import { Badge } from "@/components/ui/badge";
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb";
import { Button } from "@/components/ui/button";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useHydrated } from "@/hooks/use-hydrated";
import { isAdmin, useCurrentUser } from "@/lib/auth";
import { formatDate } from "@/lib/dates";
import { ArticleDateControl } from "./article-date-control";
import { ArticleHistory } from "./article-history";
import { ParagraphList, paragraphsToText } from "./article-text";

// Sekmeler ve dolacakları kilometre taşı.
const tabs = [
  { value: "gerekce", label: "Gerekçe", milestone: "M3" },
  { value: "degisiklikler", label: "Değişiklikler", milestone: "M3" },
  { value: "aciklama", label: "Açıklama", milestone: "M3" },
  { value: "ictihatlar", label: "İçtihatlar", milestone: "M3" },
  { value: "ilgili", label: "İlgili Maddeler", milestone: "M3" },
  { value: "norm", label: "Norm Denetimi", milestone: "M3" },
  { value: "765", label: "765 Sayılı Kanun", milestone: "M3" },
];

function articleAsText({ law, article, version }: ArticleDetailResponse) {
  const title = `${law.name} (${law.number}) — Madde ${article.number}: ${article.title}`;
  return [title, paragraphsToText(version?.paragraphs ?? [])].join("\n");
}

/** "01.06.2005 tarihinden beri yürürlükte · 5377 s.K. m.12" gibi kısa sürüm açıklaması. */
function versionNote(version: ArticleDetailResponse["version"]) {
  if (!version) return null;
  const from = version.validFrom ? formatDate(version.validFrom) : null;
  const to = version.validTo ? formatDate(version.validTo) : null;
  const range = from && to ? `${from} – ${to} arasında yürürlükte` : from ? `${from} tarihinden beri yürürlükte` : null;
  return [range, version.amendedBy].filter(Boolean).join(" · ") || null;
}

/** @param asOf Seçilen gün (?tarih=); verilirse o gün yürürlükte olan metin gösterilir. */
export function ArticleView({ detail, asOf }: { detail: ArticleDetailResponse; asOf?: string }) {
  const { article, law, path, version } = detail;
  const paragraphs = version?.paragraphs ?? [];
  const note = versionNote(version);
  const { data: user } = useCurrentUser();
  const canEdit = useHydrated() && isAdmin(user);

  async function copy() {
    try {
      await navigator.clipboard.writeText(articleAsText(detail));
      toast.success("Madde metni kopyalandı.");
    } catch {
      toast.error("Kopyalanamadı; tarayıcı panoya erişime izin vermedi.");
    }
  }

  return (
    <ScrollArea className="h-full">
      <article className="mx-auto max-w-5xl px-4 py-5 sm:px-6 lg:pr-14">
        <Breadcrumb>
          <BreadcrumbList>
            <BreadcrumbItem>
              <BreadcrumbLink asChild>
                <Link href={`/${law.slug}`}>{law.shortName || law.number}</Link>
              </BreadcrumbLink>
            </BreadcrumbItem>
            {path.map((node) => (
              <Fragment key={node.id}>
                <BreadcrumbSeparator />
                <BreadcrumbItem>{node.title}</BreadcrumbItem>
              </Fragment>
            ))}
            <BreadcrumbSeparator />
            <BreadcrumbItem>
              <BreadcrumbPage className="text-primary">Madde {article.number}</BreadcrumbPage>
            </BreadcrumbItem>
          </BreadcrumbList>
        </Breadcrumb>

        <header className="mt-3 flex flex-wrap items-end justify-between gap-4">
          <div>
            <h1 className="flex flex-wrap items-center gap-3 text-4xl leading-tight">
              Madde {article.number}
              {article.effectiveVisibility === "private" && (
                <Badge variant="outline" className="text-sm font-normal" title="Yalnız giriş yapan kullanıcılar görür">
                  <Lock /> Özel
                </Badge>
              )}
              {version?.isRepealed && (
                <Badge variant="secondary" className="text-sm font-normal">
                  Mülga
                </Badge>
              )}
            </h1>
            <h2 className="text-3xl leading-tight text-primary">{article.title}</h2>
          </div>
          <div className="flex flex-wrap gap-2 print:hidden">
            {canEdit && (
              <Button variant="outline" asChild>
                <Link href={`/panel/kutuphane/${law.slug}?madde=${article.slug}`}>
                  <Pencil /> Düzenle
                </Link>
              </Button>
            )}
            <Button variant="outline" disabled title="Yakında">
              <Star /> Favorilere ekle
            </Button>
            <Button variant="outline" disabled title="Yakında">
              <Share2 /> Paylaş
            </Button>
            <Button variant="outline" onClick={copy}>
              <Copy /> Kopyala
            </Button>
            <Button variant="outline" onClick={() => window.print()}>
              <Printer /> Yazdır
            </Button>
            <Button variant="outline" disabled title="Yakında">
              <FileDown /> PDF
            </Button>
          </div>
        </header>

        <div className="mt-5">
          <ArticleDateControl key={asOf ?? ""} asOf={asOf} />
        </div>

        <section className="mt-3 rounded-lg border bg-card">
          <h3 className="flex flex-wrap items-center gap-2 border-b bg-accent px-4 py-2.5 text-lg text-accent-foreground">
            <FileText className="size-5" aria-hidden />
            Kanun Metni
            {asOf && <span className="text-base">— {formatDate(asOf)} tarihinde yürürlükte olan metin</span>}
          </h3>
          <div className="px-4 py-4">
            {asOf && !version ? (
              <p className="text-muted-foreground">
                {formatDate(asOf)} tarihinde yürürlükte olan bir metin kayıtlı değil: madde o tarihte henüz yürürlükte
                değildi ya da o tarihteki metni girilmedi. Kayıtlı sürümler için Değişiklikler sekmesine bakın.
              </p>
            ) : paragraphs.length === 0 ? (
              <p className="text-muted-foreground">
                {version?.isRepealed ? "Bu madde mülgadır." : "Bu maddenin metni henüz girilmedi."}
              </p>
            ) : (
              <ParagraphList paragraphs={paragraphs} />
            )}
            {note && <p className="mt-4 border-t pt-3 text-sm text-muted-foreground">{note}</p>}
          </div>
        </section>

        <ArticlePager lawSlug={law.slug} previous={detail.previous} next={detail.next} asOf={asOf} />

        <Tabs defaultValue={tabs[0].value} className="mt-6 print:hidden">
          {/* Dar ekranda tek satır, yatay kaydırılır (alt alta kayıp içeriğin üstüne binmesin). */}
          <TabsList variant="line" className="w-full justify-start overflow-x-auto overflow-y-hidden">
            {tabs.map((tab) => (
              <TabsTrigger
                key={tab.value}
                value={tab.value}
                className="text-base data-active:text-primary after:bg-primary"
              >
                {tab.label}
                {tab.value === "degisiklikler" && detail.versions.length > 1 && (
                  <span className="text-sm text-muted-foreground">({detail.versions.length})</span>
                )}
              </TabsTrigger>
            ))}
          </TabsList>
          {tabs.map((tab) => (
            <TabsContent key={tab.value} value={tab.value} className="rounded-lg border bg-card p-4">
              {tab.value === "degisiklikler" ? (
                <ArticleHistory lawSlug={law.slug} articleSlug={article.slug} shownVersionId={version?.id} />
              ) : (
                <p className="text-muted-foreground">
                  {tab.label} içeriği yönetim panelinden girildiğinde burada görünecek ({tab.milestone}).
                </p>
              )}
            </TabsContent>
          ))}
        </Tabs>
      </article>
    </ScrollArea>
  );
}

function ArticlePager({
  lawSlug,
  previous,
  next,
  asOf,
}: {
  lawSlug: string;
  previous: ArticleRef | null;
  next: ArticleRef | null;
  asOf?: string;
}) {
  if (!previous && !next) return null;
  // Seçilen tarih korunur: aynı suç tarihi için birkaç maddeye bakılabilsin.
  const query = asOf ? `?tarih=${asOf}` : "";
  return (
    <nav className="mt-4 flex justify-between gap-2 print:hidden" aria-label="Madde gezinmesi">
      {previous ? (
        <Button variant="ghost" asChild>
          <Link href={`/${lawSlug}/${previous.slug}${query}`} title={previous.title}>
            <ChevronLeft /> Madde {previous.number}
          </Link>
        </Button>
      ) : (
        <span />
      )}
      {next && (
        <Button variant="ghost" asChild>
          <Link href={`/${lawSlug}/${next.slug}${query}`} title={next.title}>
            Madde {next.number} <ChevronRight />
          </Link>
        </Button>
      )}
    </nav>
  );
}

/** Madde yüklenirken gösterilen iskelet. */
export function ArticleSkeleton() {
  return (
    <div className="mx-auto max-w-5xl space-y-4 px-4 py-5 sm:px-6" aria-busy="true" aria-label="Yükleniyor">
      <div className="h-4 w-1/2 animate-pulse rounded bg-muted" />
      <div className="h-10 w-40 animate-pulse rounded bg-muted" />
      <div className="h-8 w-2/3 animate-pulse rounded bg-muted" />
      <div className="h-40 animate-pulse rounded-lg bg-muted" />
    </div>
  );
}

export function WelcomeView() {
  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      <h1 className="text-3xl">Hoş geldiniz</h1>
      <p className="reading mt-3 text-muted-foreground">
        Soldaki kütüphaneden bir kanun açıp madde seçin. Madde metni, gerekçe, değişiklikler, açıklama,
        içtihatlar, ilgili maddeler, norm denetimi ve 765 sayılı kanun karşılıkları burada sekmeler hâlinde
        gösterilir; sağdaki panelde kendi vurgularınız ve notlarınız yer alır.
      </p>
    </div>
  );
}
