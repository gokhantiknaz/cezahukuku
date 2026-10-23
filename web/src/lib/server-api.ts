// Yalnız sunucu bileşenlerinde kullanılır: .NET API'yi doğrudan çağırır ve tarayıcının oturum çerezini iletir,
// böylece giriş yapmış kullanıcı private içeriği sunucu tarafında da görür. Çerez okunduğu için çağıran
// bileşen <Suspense> içinde olmalıdır (Cache Components kuralı).

import { cache } from "react";
import { cookies } from "next/headers";
import { getGetArticleUrl, getGetLawUrl, getGetLibraryUrl } from "@/api/generated/library/library";
import type { ArticleDetailResponse, LawTreeResponse, LibraryResponse } from "@/api/generated/model";

const apiUrl = process.env.API_URL ?? "http://localhost:5080";

/** 404'te null döner (içerik yok ya da kullanıcıya kapalı); diğer hatalarda hata fırlatır. */
async function apiGet<T>(path: string): Promise<T | null> {
  const cookieHeader = (await cookies()).toString();
  const response = await fetch(`${apiUrl}${path}`, {
    headers: { Accept: "application/json", ...(cookieHeader ? { Cookie: cookieHeader } : {}) },
  });
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`API isteği başarısız: ${path} (${response.status})`);
  return (await response.json()) as T;
}

// React cache: aynı istekte metadata ve sayfa (ya da layout ve sayfa) aynı veriyi tek kez çeker.

export const getLibrary = cache(async () => (await apiGet<LibraryResponse>(getGetLibraryUrl())) ?? { groups: [] });

// Adres parçaları API yoluna eklenmeden önce doğrulanır ("../" gibi değerler başka uç noktaya gitmesin).
const isSlug = (value: string) => /^[a-z0-9]+(-[a-z0-9]+)*$/.test(value);

export const getLaw = cache(async (lawSlug: string) =>
  isSlug(lawSlug) ? apiGet<LawTreeResponse>(getGetLawUrl(lawSlug)) : null,
);

export const getArticle = cache(async (lawSlug: string, articleSlug: string) =>
  isSlug(lawSlug) && isSlug(articleSlug) ? apiGet<ArticleDetailResponse>(getGetArticleUrl(lawSlug, articleSlug)) : null,
);
