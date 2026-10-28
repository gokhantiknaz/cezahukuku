"use client";

import { Fragment, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { ChevronLeft, ChevronRight, Copy, FileText, Lock, Pencil, Printer } from "lucide-react";
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
import { cn } from "@/lib/utils";
import { FavoriteButton, MobileArticleActions, PdfButton, ShareButton } from "./article-actions";
import { ArticleDateControl } from "./article-date-control";
import { ArticleHistory } from "./article-history";
import { usePublishCurrentArticle } from "./current-article";
import { DecisionList } from "./decision-list";
import { OldLawCounterparts, RelatedLinks } from "./link-list";
import { NoteList } from "./note-list";
import { paragraphsToText } from "./article-text";
import { HighlightableText } from "./highlightable-text";

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

/**
 * @param asOf Seçilen gün (?tarih=); verilirse o gün yürürlükte olan metin gösterilir.
 * @param initialTab Açık gelecek sekme (?sekme=gerekce); verilirse sekmelere kaydırılır.
 */
export function ArticleView({ detail, asOf, initialTab }: { detail: ArticleDetailResponse; asOf?: string; initialTab?: string }) {
  const { article, law, path, version } = detail;
  const paragraphs = version?.paragraphs ?? [];
  const note = versionNote(version);
  const { data: user } = useCurrentUser();
  const canEdit = useHydrated() && isAdmin(user);
  // Seçilen tarih korunur: aynı suç tarihi için birkaç maddeye bakılabilsin.
  const query = asOf ? `?tarih=${asOf}` : "";
  const toLink = (ref: ArticleRef | null) => (ref ? { href: `/${law.slug}/${ref.slug}${query}`, label: `Madde ${ref.number}` } : null);
  // Telefonda suç tarihi alanı yer kaplamasın: tarih seçili değilse "⋯" menüsünden açılır.
  const [dateOpen, setDateOpen] = useState(false);
  const [tabsAtEnd, setTabsAtEnd] = useState(false);
  usePublishCurrentArticle({
    articleId: article.id,
    versionId: version?.id,
    label: `${law.shortName || law.number} Madde ${article.number}${article.title ? ` — ${article.title}` : ""}`,
    blockOrder: paragraphs.flatMap((p) => [`${p.id}:`, ...p.items.map((i) => `${p.id}:${i.id}`)]),
    previous: toLink(detail.previous),
    next: toLink(detail.next),
  });
  const notesOf = (kind: string) => detail.notes.filter((n) => n.kind === kind);
  const openTab = tabs.some((t) => t.value === initialTab) ? initialTab! : tabs[0].value;
  const tabsRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (initialTab && openTab === initialTab) {
      tabsRef.current?.scrollIntoView({ block: "start" });
      // Dar ekranda sekme listesi yana kayar: açık sekme görünür olsun.
      tabsRef.current?.querySelector("[role=tab][data-state=active]")?.scrollIntoView({ block: "nearest", inline: "nearest" });
    }
  }, [article.id, openTab, initialTab]);
  const tabCounts: Record<string, number> = {
    gerekce: notesOf("gerekce").length,
    degisiklikler: detail.versions.length > 1 ? detail.versions.length : 0,
    aciklama: notesOf("aciklama").length + notesOf("dersNotu").length,
    ictihatlar: detail.decisions.filter((d) => d.kind === "ictihat").length,
    norm: detail.decisions.filter((d) => d.kind === "normDenetimi").length,
    ilgili: detail.links.filter((l) => l.kind === "ilgili" || l.incoming).length + detail.citedBy.length,
    "765": detail.links.filter((l) => l.kind === "eskiKanunKarsiligi" && !l.incoming).length,
  };

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
      <article className="mx-auto max-w-5xl px-3 py-4 sm:px-6 sm:py-5 lg:pr-14">
        <Breadcrumb>
          {/* Telefonda tek satır: kanun ve maddenin bulunduğu bölüm; aradaki kitap/kısım adları gizlenir. */}
          <BreadcrumbList className="max-md:flex-nowrap">
            <BreadcrumbItem>
              <BreadcrumbLink asChild>
                <Link href={`/${law.slug}`}>{law.shortName || law.number}</Link>
              </BreadcrumbLink>
            </BreadcrumbItem>
            {path.map((node, index) => {
              const last = index === path.length - 1;
              return (
                <Fragment key={node.id}>
                  <BreadcrumbSeparator className={cn(!last && "max-md:hidden")} />
                  <BreadcrumbItem className={cn(last ? "min-w-0" : "max-md:hidden")}>
                    <span className={cn(last && "max-md:truncate")}>{node.title}</span>
                  </BreadcrumbItem>
                </Fragment>
              );
            })}
            <BreadcrumbSeparator />
            <BreadcrumbItem className="shrink-0">
              <BreadcrumbPage className="text-primary">Madde {article.number}</BreadcrumbPage>
            </BreadcrumbItem>
          </BreadcrumbList>
        </Breadcrumb>

        <header className="mt-2 flex items-start justify-between gap-2 sm:mt-3 md:flex-wrap md:items-end md:gap-4">
          <div className="min-w-0">
            <h1 className="flex flex-wrap items-center gap-x-3 gap-y-1 text-3xl leading-tight md:text-4xl">
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
            <h2 className="text-2xl leading-tight text-primary md:text-3xl">{article.title}</h2>
          </div>
          <MobileArticleActions
            className="md:hidden"
            articleId={article.id}
            shareTitle={`${law.shortName || law.number} Madde ${article.number} — ${article.title}`}
            editHref={canEdit ? `/panel/kutuphane/${law.slug}?madde=${article.slug}` : null}
            onCopy={copy}
            onShowDate={() => setDateOpen(true)}
          />
          <div className="hidden flex-wrap gap-2 md:flex print:hidden">
            {canEdit && (
              <Button variant="outline" asChild>
                <Link href={`/panel/kutuphane/${law.slug}?madde=${article.slug}`}>
                  <Pencil /> Düzenle
                </Link>
              </Button>
            )}
            <FavoriteButton articleId={article.id} />
            <ShareButton title={`${law.shortName || law.number} Madde ${article.number} — ${article.title}`} />
            <Button variant="outline" onClick={copy}>
              <Copy /> Kopyala
            </Button>
            <Button variant="outline" onClick={() => window.print()}>
              <Printer /> Yazdır
            </Button>
            <PdfButton />
          </div>
        </header>

        <div className={cn("mt-5", !asOf && !dateOpen && "max-md:hidden")}>
          <ArticleDateControl key={asOf ?? ""} asOf={asOf} />
        </div>

        {/* Telefonda metin kenardan kenara: okuma genişliği kutu kenarlarına harcanmasın. */}
        <section className="mt-3 rounded-lg border bg-card max-md:-mx-3 max-md:rounded-none max-md:border-x-0">
          <h3 className="flex flex-wrap items-center gap-2 border-b bg-accent px-3 py-2 text-base text-accent-foreground md:px-4 md:py-2.5 md:text-lg">
            <FileText className="size-5" aria-hidden />
            Kanun Metni
            {asOf && <span className="text-base">— {formatDate(asOf)} tarihinde yürürlükte olan metin</span>}
          </h3>
          <div className="px-3 py-3 md:px-4 md:py-4">
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
              <HighlightableText
                articleId={article.id}
                versionId={version!.id}
                paragraphs={paragraphs}
                citations={detail.citations}
              />
            )}
            {note && <p className="mt-4 border-t pt-3 text-sm text-muted-foreground">{note}</p>}
          </div>
        </section>

        <ArticlePager lawSlug={law.slug} previous={detail.previous} next={detail.next} asOf={asOf} />

        {/* Başka maddeye ya da sekmeye gelince sekmeler yeniden kurulsun (defaultValue yalnız ilk açılışta geçer). */}
        <Tabs key={`${article.id}-${openTab}`} ref={tabsRef} defaultValue={openTab} className="mt-6 scroll-mt-4 max-lg:scroll-mt-16 print:hidden">
          {/* Dar ekranda tek satır, yatay kaydırılır (alt alta kayıp içeriğin üstüne binmesin); sağdaki solgunluk
              devamı olduğunu gösterir, sona gelince kalkar. */}
          <div className="relative">
            <TabsList
              variant="line"
              className="w-full justify-start overflow-x-auto overflow-y-hidden max-md:h-12!"
              onScroll={(e) => {
                const list = e.currentTarget;
                setTabsAtEnd(list.scrollLeft + list.clientWidth >= list.scrollWidth - 4);
              }}
            >
              {tabs.map((tab) => (
                <TabsTrigger
                  key={tab.value}
                  value={tab.value}
                  className="text-base data-active:text-primary after:bg-primary max-md:px-3"
                >
                  {tab.label}
                  {(tabCounts[tab.value] ?? 0) > 0 && (
                    <span className="text-sm text-muted-foreground">({tabCounts[tab.value]})</span>
                  )}
                </TabsTrigger>
              ))}
            </TabsList>
            <span
              aria-hidden
              className={cn(
                "pointer-events-none absolute inset-y-0 right-0 w-10 bg-linear-to-l from-background transition-opacity md:hidden",
                tabsAtEnd && "opacity-0",
              )}
            />
          </div>
          {tabs.map((tab) => (
            <TabsContent
              key={tab.value}
              value={tab.value}
              className="rounded-lg border bg-card p-4 max-md:-mx-3 max-md:rounded-none max-md:border-x-0 max-md:px-3"
            >
              {tab.value === "degisiklikler" ? (
                <ArticleHistory lawSlug={law.slug} articleSlug={article.slug} shownVersionId={version?.id} />
              ) : tab.value === "ictihatlar" ? (
                <DecisionList
                  decisions={detail.decisions.filter((d) => d.kind === "ictihat")}
                  empty="Bu maddeye henüz içtihat bağlanmadı."
                />
              ) : tab.value === "norm" ? (
                <DecisionList
                  decisions={detail.decisions.filter((d) => d.kind === "normDenetimi")}
                  empty="Bu madde hakkında kayıtlı norm denetimi kararı yok."
                />
              ) : tab.value === "ilgili" ? (
                <RelatedLinks links={detail.links} citedBy={detail.citedBy} />
              ) : tab.value === "765" ? (
                <OldLawCounterparts links={detail.links} />
              ) : tab.value === "gerekce" ? (
                <NoteList notes={notesOf("gerekce")} empty="Bu maddenin gerekçesi henüz girilmedi." />
              ) : tab.value === "aciklama" ? (
                <div className="space-y-6">
                  <NoteList notes={notesOf("aciklama")} empty="Bu maddeye henüz açıklama girilmedi." />
                  {notesOf("dersNotu").length > 0 && (
                    <section>
                      <h3 className="mb-3 border-t pt-4 text-xl text-primary">Ders notu</h3>
                      <NoteList notes={notesOf("dersNotu")} empty="" />
                    </section>
                  )}
                </div>
              ) : (
                <p className="text-muted-foreground">
                  {tab.label} bölümü yakında eklenecek.
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
  // Dar ekranda önceki/sonraki madde alttaki sabit çubukta.
  return (
    <nav className="mt-4 flex justify-between gap-2 max-lg:hidden print:hidden" aria-label="Önceki ve sonraki madde">
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
