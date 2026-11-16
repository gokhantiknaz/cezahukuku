"use client";

import { useRef, useState, type KeyboardEvent, type MouseEvent, type ReactNode } from "react";
import Link from "next/link";
import { ArrowRight, LoaderCircle } from "lucide-react";
import { useGetArticle } from "@/api/generated/library/library";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { formatDate } from "@/lib/dates";
import { ParagraphList } from "./article-text";

/** Parantez içi atıfın açacağı madde: `data-article-ref="tck/madde-21"`. */
type ArticleRefTarget = { lawSlug: string; articleSlug: string };

function targetOf(element: Element): ArticleRefTarget | null {
  const [lawSlug, articleSlug] = (element.getAttribute("data-article-ref") ?? "").split("/");
  return lawSlug && articleSlug ? { lawSlug, articleSlug } : null;
}

/**
 * Metindeki parantez içi atıfları (`[data-article-ref]`; madde metninde `ParagraphList`, ders notunda sunucunun
 * işaretlediği okuma HTML'i) açılır pencereye bağlar (spec 0005). Atıf bir bağlantıdır: tıklama ve Enter sayfayı
 * değiştirmez, pencereyi açar; Space de açar. Pencere kapanınca odak tıklanan numaraya döner.
 *
 * @param asOf Madde ekranında seçili suç tarihi (?tarih=): pencere o tarihte yürürlükte olan metni gösterir.
 */
export function ArticleRefHost({ asOf, className, children }: { asOf?: string; className?: string; children: ReactNode }) {
  const [target, setTarget] = useState<ArticleRefTarget | null>(null);
  const trigger = useRef<HTMLElement | null>(null);

  function open(event: MouseEvent | KeyboardEvent) {
    const element = event.target instanceof Element ? event.target.closest("[data-article-ref]") : null;
    if (!element || !(element instanceof HTMLElement)) return;
    event.preventDefault();
    // Değişiklik işareti ya da başka bir üst tıklama işleyicisi aynı tıklamayla çalışmasın.
    event.stopPropagation();
    // Metin seçilirken (sürükleyerek) bırakılan tıklama atıfı açmaz.
    if (event.type === "click" && window.getSelection()?.isCollapsed === false) return;
    const next = targetOf(element);
    if (!next) return;
    trigger.current = element;
    setTarget(next);
  }

  return (
    <div
      className={className}
      onClick={open}
      onKeyDown={(e) => e.key === " " && open(e)}
    >
      {children}
      <Dialog open={target !== null} onOpenChange={(isOpen) => !isOpen && setTarget(null)}>
        <DialogContent
          className="flex max-h-[85dvh] flex-col gap-3 sm:max-w-2xl"
          onCloseAutoFocus={(e) => {
            e.preventDefault();
            trigger.current?.focus();
          }}
        >
          {target && <ArticleRefContent key={`${target.lawSlug}/${target.articleSlug}`} target={target} asOf={asOf} />}
        </DialogContent>
      </Dialog>
    </div>
  );
}

/** Pencerenin içi: madde başlığı, metni (atıflar işaretsiz — pencereden pencere açılmaz), mülga ibaresi, "Maddeye git". */
function ArticleRefContent({ target, asOf }: { target: ArticleRefTarget; asOf?: string }) {
  const query = useGetArticle(target.lawSlug, target.articleSlug, asOf ? { asOf } : undefined, {
    // Hata hemen gösterilsin; okuyucu "Yeniden dene" ile tekrarlar.
    query: { retry: false },
  });
  const href = `/${target.lawSlug}/${target.articleSlug}${asOf ? `?tarih=${asOf}` : ""}`;

  if (query.isPending)
    return (
      <>
        <DialogHeader>
          <DialogTitle>Madde yükleniyor</DialogTitle>
          <DialogDescription className="sr-only">Madde metni yükleniyor.</DialogDescription>
        </DialogHeader>
        <p role="status" className="flex items-center gap-2 py-6 text-muted-foreground">
          <LoaderCircle className="size-5 animate-spin" aria-hidden /> Yükleniyor…
        </p>
      </>
    );

  // apiFetch 2xx dışında hata fırlatır; 404 (madde yok ya da görünmüyor) da burada "yüklenemedi" olur.
  if (query.isError || query.data?.status !== 200)
    return (
      <>
        <DialogHeader>
          <DialogTitle>Madde yüklenemedi</DialogTitle>
          <DialogDescription>Bağlantı ya da sunucu hatası. Bir süre sonra yeniden deneyin.</DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={() => query.refetch()} disabled={query.isFetching}>
            Yeniden dene
          </Button>
        </DialogFooter>
      </>
    );

  const { article, law, version } = query.data.data;
  const paragraphs = version?.paragraphs ?? [];
  return (
    <>
      <DialogHeader className="pr-8">
        <DialogTitle className="text-lg leading-snug">
          {law.shortName || law.number} Madde {article.number}
          {article.title && <span className="font-normal"> — {article.title}</span>}
        </DialogTitle>
        <DialogDescription>
          {asOf ? `${formatDate(asOf)} tarihinde yürürlükte olan metin` : "Yürürlükteki metin"}
        </DialogDescription>
      </DialogHeader>
      <div className="min-h-0 overflow-y-auto pr-1">
        {asOf && !version ? (
          <p className="text-muted-foreground">
            {formatDate(asOf)} tarihinde yürürlükte olan bir metin kayıtlı değil: madde o tarihte henüz yürürlükte
            değildi ya da o tarihteki metni girilmedi.
          </p>
        ) : version?.isRepealed ? (
          <p className="reading font-semibold text-change">Mülga</p>
        ) : paragraphs.length === 0 ? (
          <p className="text-muted-foreground">Bu maddenin metni henüz girilmedi.</p>
        ) : (
          <ParagraphList paragraphs={paragraphs} />
        )}
      </div>
      <DialogFooter>
        <DialogClose asChild>
          <Button variant="outline">Kapat</Button>
        </DialogClose>
        <Button asChild>
          <Link href={href}>
            Maddeye git <ArrowRight aria-hidden />
          </Link>
        </Button>
      </DialogFooter>
    </>
  );
}
