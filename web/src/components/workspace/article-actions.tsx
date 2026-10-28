"use client";

import { useState } from "react";
import Link from "next/link";
import { useQueryClient } from "@tanstack/react-query";
import { CalendarSearch, Copy, Ellipsis, FileDown, Pencil, Printer, Share2, Star, type LucideIcon } from "lucide-react";
import { toast } from "sonner";
import {
  getListFavoritesQueryKey,
  useAddFavorite,
  useListFavorites,
  useRemoveFavorite,
} from "@/api/generated/personal/personal";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { useHydrated } from "@/hooks/use-hydrated";
import { useCurrentUser } from "@/lib/auth";
import { canAnnotate } from "@/lib/highlights";
import { cn } from "@/lib/utils";

/**
 * Favorilere ekle / çıkar. Kişisel katmanı olmayan (anonim, okuyucu) kullanıcıda giriş sayfasına götürür ya da gizlenir.
 * `compact`: yalnız simge (telefonda başlığın yanında).
 */
export function FavoriteButton({ articleId, compact = false }: { articleId: string; compact?: boolean }) {
  const hydrated = useHydrated();
  const { data: user } = useCurrentUser();
  const enabled = hydrated && canAnnotate(user);
  const queryClient = useQueryClient();
  const { data } = useListFavorites({ query: { enabled } });
  const add = useAddFavorite();
  const remove = useRemoveFavorite();
  const favorite = data?.status === 200 && data.data.some((f) => f.article.articleId === articleId);

  if (hydrated && !user) {
    return (
      <Button variant={compact ? "ghost" : "outline"} size={compact ? "icon-lg" : "default"} asChild title="Favoriler için giriş yapın">
        <Link href="/giris" aria-label={compact ? "Favorilere ekle" : undefined}>
          <Star className={cn(compact && "size-5")} /> {!compact && "Favorilere ekle"}
        </Link>
      </Button>
    );
  }
  if (!enabled) return null;

  async function toggle() {
    try {
      if (favorite) await remove.mutateAsync({ articleId });
      else await add.mutateAsync({ articleId });
      await queryClient.invalidateQueries({ queryKey: getListFavoritesQueryKey() });
      toast.success(favorite ? "Favorilerden çıkarıldı." : "Favorilere eklendi.");
    } catch {
      toast.error("Favori kaydedilemedi.");
    }
  }

  return (
    <Button
      variant={compact ? "ghost" : "outline"}
      size={compact ? "icon-lg" : "default"}
      onClick={toggle}
      disabled={add.isPending || remove.isPending}
      aria-pressed={favorite}
      aria-label={compact ? (favorite ? "Favorilerde" : "Favorilere ekle") : undefined}
      title={compact ? (favorite ? "Favorilerden çıkar" : "Favorilere ekle") : undefined}
    >
      <Star className={cn(favorite && "fill-highlight-yellow text-amber-600", compact && "size-5")} />
      {!compact && (favorite ? "Favorilerde" : "Favorilere ekle")}
    </Button>
  );
}

/** Paylaş: telefonda sistem paylaşım menüsü, masaüstünde bağlantı panoya kopyalanır. */
export function ShareButton({ title, compact = false }: { title: string; compact?: boolean }) {
  async function share() {
    const url = window.location.href.split("#")[0];
    try {
      if (navigator.share) {
        await navigator.share({ title, url });
        return;
      }
      await navigator.clipboard.writeText(url);
      toast.success("Bağlantı kopyalandı.");
    } catch (error) {
      // Kullanıcı paylaşım menüsünü kapattıysa hata göstermeye gerek yok.
      if (error instanceof DOMException && error.name === "AbortError") return;
      toast.error("Paylaşılamadı.");
    }
  }
  if (compact) {
    return (
      <Button variant="ghost" size="icon-lg" onClick={share} aria-label="Paylaş" title="Paylaş">
        <Share2 className="size-5" />
      </Button>
    );
  }
  return (
    <Button variant="outline" onClick={share}>
      <Share2 /> Paylaş
    </Button>
  );
}

function printAsPdf() {
  toast.info("Yazdırma penceresinde hedef olarak “PDF olarak kaydet”i seçin.");
  window.setTimeout(() => window.print(), 300);
}

/**
 * PDF olarak kaydet: tarayıcının yazdırma penceresi açılır (yazdırma düzeni yalnız maddeyi basar); hedef olarak
 * "PDF olarak kaydet" seçilir.
 */
export function PdfButton() {
  return (
    <Button
      variant="outline"
      title="Yazdırma penceresinde hedef olarak “PDF olarak kaydet”i seçin"
      onClick={printAsPdf}
    >
      <FileDown /> PDF
    </Button>
  );
}

type SheetAction = { label: string; icon: LucideIcon; onSelect?: () => void; href?: string };

/**
 * Telefonda madde başlığının yanındaki düğmeler: favori ve paylaş simgeleri, kalanlar "⋯" ile alttan açılan listede
 * (düzenle, kopyala, yazdır, PDF, suç tarihindeki metin).
 */
export function MobileArticleActions({
  articleId,
  shareTitle,
  editHref,
  onCopy,
  onShowDate,
  className,
}: {
  articleId: string;
  shareTitle: string;
  editHref: string | null;
  onCopy: () => void;
  onShowDate: () => void;
  className?: string;
}) {
  const [open, setOpen] = useState(false);
  const actions: SheetAction[] = [
    ...(editHref ? [{ label: "Düzenle", icon: Pencil, href: editHref }] : []),
    { label: "Suç / olay tarihindeki metin", icon: CalendarSearch, onSelect: onShowDate },
    { label: "Kopyala", icon: Copy, onSelect: onCopy },
    // Yazdırma çekmece kapandıktan sonra başlasın (çekmece kâğıda basılmasın).
    { label: "Yazdır", icon: Printer, onSelect: () => window.setTimeout(() => window.print(), 300) },
    { label: "PDF olarak kaydet", icon: FileDown, onSelect: printAsPdf },
  ];
  const rowClass = "flex w-full items-center gap-3 rounded-md px-3 py-3 text-left text-base hover:bg-accent";

  return (
    <div className={cn("-mr-2 flex shrink-0 items-center print:hidden", className)}>
      <FavoriteButton articleId={articleId} compact />
      <ShareButton title={shareTitle} compact />
      <Sheet open={open} onOpenChange={setOpen}>
        <SheetTrigger asChild>
          <Button variant="ghost" size="icon-lg" aria-label="Diğer işlemler" title="Diğer işlemler">
            <Ellipsis className="size-5" />
          </Button>
        </SheetTrigger>
        <SheetContent side="bottom" className="gap-0 rounded-t-xl px-2 pt-3 pb-[max(0.75rem,env(safe-area-inset-bottom))]">
          <SheetTitle className="px-3 pb-1 text-sm font-normal text-muted-foreground">Madde işlemleri</SheetTitle>
          <ul>
            {actions.map((action) => {
              const Icon = action.icon;
              const content = (
                <>
                  <Icon className="size-5 shrink-0 text-primary" aria-hidden />
                  {action.label}
                </>
              );
              return (
                <li key={action.label}>
                  {action.href ? (
                    <Link href={action.href} className={rowClass} onClick={() => setOpen(false)}>
                      {content}
                    </Link>
                  ) : (
                    <button
                      type="button"
                      className={rowClass}
                      onClick={() => {
                        setOpen(false);
                        // Doğrudan dokunuşun içinde çalışır: pano izni (iOS) kullanıcı hareketine bağlı.
                        action.onSelect?.();
                      }}
                    >
                      {content}
                    </button>
                  )}
                </li>
              );
            })}
          </ul>
        </SheetContent>
      </Sheet>
    </div>
  );
}
