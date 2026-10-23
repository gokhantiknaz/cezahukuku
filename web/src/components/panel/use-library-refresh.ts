"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";

/**
 * Kütüphanede bir değişiklikten sonra kütüphane/kanun/madde sorgularını tazeler. Okuma ekranı sunucuda çizildiği
 * için router da yenilenir; panelden okuma ekranına geçince güncel hâl görünsün.
 */
export function useLibraryRefresh() {
  const queryClient = useQueryClient();
  const router = useRouter();
  return async () => {
    await queryClient.invalidateQueries({
      predicate: (query) => {
        const key = String(query.queryKey[0] ?? "");
        return (
          key.startsWith("/api/library") ||
          key.startsWith("/api/admin/article-versions") ||
          key.startsWith("/api/admin/decisions")
        );
      },
    });
    router.refresh();
  };
}
