"use client";

import Link from "next/link";
import { useQueryClient } from "@tanstack/react-query";
import { FileDown, Share2, Star } from "lucide-react";
import { toast } from "sonner";
import {
  getListFavoritesQueryKey,
  useAddFavorite,
  useListFavorites,
  useRemoveFavorite,
} from "@/api/generated/personal/personal";
import { Button } from "@/components/ui/button";
import { useHydrated } from "@/hooks/use-hydrated";
import { useCurrentUser } from "@/lib/auth";
import { canAnnotate } from "@/lib/highlights";
import { cn } from "@/lib/utils";

/** Favorilere ekle / çıkar. Kişisel katmanı olmayan (anonim, okuyucu) kullanıcıda giriş sayfasına götürür ya da gizlenir. */
export function FavoriteButton({ articleId }: { articleId: string }) {
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
      <Button variant="outline" asChild title="Favoriler için giriş yapın">
        <Link href="/giris">
          <Star /> Favorilere ekle
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
    <Button variant="outline" onClick={toggle} disabled={add.isPending || remove.isPending} aria-pressed={favorite}>
      <Star className={cn(favorite && "fill-highlight-yellow text-amber-600")} />
      {favorite ? "Favorilerde" : "Favorilere ekle"}
    </Button>
  );
}

/** Paylaş: telefonda sistem paylaşım menüsü, masaüstünde bağlantı panoya kopyalanır. */
export function ShareButton({ title }: { title: string }) {
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
  return (
    <Button variant="outline" onClick={share}>
      <Share2 /> Paylaş
    </Button>
  );
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
      onClick={() => {
        toast.info("Yazdırma penceresinde hedef olarak “PDF olarak kaydet”i seçin.");
        window.setTimeout(() => window.print(), 300);
      }}
    >
      <FileDown /> PDF
    </Button>
  );
}
