"use client";

import { Fragment } from "react";
import { Copy, FileDown, FileText, Printer, Share2, Star } from "lucide-react";
import { toast } from "sonner";
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb";
import { Button } from "@/components/ui/button";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import type { ArticleDetail } from "@/lib/library-types";

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

function articleAsText(article: ArticleDetail) {
  const lines = article.paragraphs.map((p) => `(${p.number}) ${p.text}`);
  return [`${article.law.name} (${article.law.number}) — Madde ${article.number}: ${article.title}`, ...lines].join(
    "\n",
  );
}

export function ArticleView({ article }: { article: ArticleDetail }) {
  async function copy() {
    try {
      await navigator.clipboard.writeText(articleAsText(article));
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
            <BreadcrumbItem>{article.law.shortName || article.law.number}</BreadcrumbItem>
            {article.path.map((node) => (
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
            <h1 className="text-4xl leading-tight">Madde {article.number}</h1>
            <h2 className="text-3xl leading-tight text-primary">{article.title}</h2>
          </div>
          <div className="flex flex-wrap gap-2 print:hidden">
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

        <section className="mt-5 rounded-lg border bg-card">
          <h3 className="flex items-center gap-2 border-b bg-accent px-4 py-2.5 text-lg text-accent-foreground">
            <FileText className="size-5" aria-hidden />
            Kanun Metni
          </h3>
          <div className="px-4 py-4">
            {article.paragraphs.length === 0 ? (
              <p className="text-muted-foreground">Bu maddenin metni henüz girilmedi.</p>
            ) : (
              <ol className="reading space-y-2">
                {article.paragraphs.map((p) => (
                  <li key={p.number} className="grid grid-cols-[2.25rem_1fr]">
                    <span className="text-muted-foreground">({p.number})</span>
                    <span>{p.text}</span>
                  </li>
                ))}
              </ol>
            )}
          </div>
        </section>

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
              </TabsTrigger>
            ))}
          </TabsList>
          {tabs.map((tab) => (
            <TabsContent key={tab.value} value={tab.value} className="rounded-lg border bg-card p-4">
              <p className="text-muted-foreground">
                {tab.label} içeriği yönetim panelinden girildiğinde burada görünecek ({tab.milestone}).
              </p>
            </TabsContent>
          ))}
        </Tabs>
      </article>
    </ScrollArea>
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
