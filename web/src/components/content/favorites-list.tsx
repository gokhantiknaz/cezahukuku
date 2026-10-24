"use client";

import Link from "next/link";
import { useQueryClient } from "@tanstack/react-query";
import { LogIn, Star } from "lucide-react";
import { getListFavoritesQueryKey, useListFavorites, useRemoveFavorite } from "@/api/generated/personal/personal";
import { Button } from "@/components/ui/button";
import { useHydrated } from "@/hooks/use-hydrated";
import { useCurrentUser } from "@/lib/auth";
import { canAnnotate } from "@/lib/highlights";

export function FavoritesList() {
  const hydrated = useHydrated();
  const { data: user, isPending } = useCurrentUser();
  const enabled = hydrated && canAnnotate(user);
  const queryClient = useQueryClient();
  const { data } = useListFavorites({ query: { enabled } });
  const remove = useRemoveFavorite();
  const favorites = data?.status === 200 ? data.data : null;

  if (!hydrated || isPending) return null;
  if (!user)
    return (
      <p className="mt-6">
        <Button asChild>
          <Link href="/giris">
            <LogIn /> Favorileriniz için giriş yapın
          </Link>
        </Button>
      </p>
    );
  if (!enabled) return <p className="mt-6 text-muted-foreground">Hesabınız yalnız okuma yetkisine sahip; favori ekleyemezsiniz.</p>;
  if (!favorites) return <p className="mt-6 text-muted-foreground">Yükleniyor…</p>;
  if (favorites.length === 0) return <p className="mt-6 text-muted-foreground">Henüz favori maddeniz yok.</p>;

  return (
    <ul className="mt-4 divide-y rounded-lg border bg-card">
      {favorites.map(({ article }) => (
        <li key={article.articleId} className="flex items-center gap-3 px-4 py-3">
          <Star className="size-5 shrink-0 fill-highlight-yellow text-amber-600" aria-hidden />
          <Link href={`/${article.lawSlug}/${article.articleSlug}`} className="min-w-0 flex-1 hover:underline">
            <span className="font-medium text-primary">
              {article.lawLabel} Madde {article.number}
            </span>
            {article.title && <span> — {article.title}</span>}
          </Link>
          <Button
            size="sm"
            variant="ghost"
            disabled={remove.isPending}
            onClick={async () => {
              await remove.mutateAsync({ articleId: article.articleId });
              await queryClient.invalidateQueries({ queryKey: getListFavoritesQueryKey() });
            }}
          >
            Çıkar
          </Button>
        </li>
      ))}
    </ul>
  );
}
