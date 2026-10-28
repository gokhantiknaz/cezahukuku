"use client";

import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";

/** Orta alanda açık olan madde: sağ panel (Notlarım) bunun vurgu ve notlarını gösterir. */
export type CurrentArticle = {
  articleId: string;
  /** Gösterilen sürüm; vurgular bu sürümün metnine göre yerleştirilir. Metin yoksa undefined. */
  versionId: string | undefined;
  /** "TCK Madde 81 — Kasten öldürme" */
  label: string;
  /** Fıkra/bent anahtarları metindeki sırasıyla ("fıkraId:bentId"); panel vurguları metin sırasıyla dizer. */
  blockOrder: string[];
  /** Önceki / sonraki madde (dar ekrandaki alt çubuk için). */
  previous: ArticleLink | null;
  next: ArticleLink | null;
};

/** "/tck/madde-80" ve "Madde 80" */
export type ArticleLink = { href: string; label: string };

type CurrentArticleContextValue = {
  article: CurrentArticle | null;
  setArticle: (article: CurrentArticle | null) => void;
  /** Panelden metindeki bir vurguya gidilince çağrılır (dar ekranda not çekmecesi kapansın). */
  onReveal?: () => void;
};

const CurrentArticleContext = createContext<CurrentArticleContextValue>({ article: null, setArticle: () => {} });

export function CurrentArticleProvider({ children, onReveal }: { children: ReactNode; onReveal?: () => void }) {
  const [article, setArticle] = useState<CurrentArticle | null>(null);
  const value = useMemo(() => ({ article, setArticle, onReveal }), [article, onReveal]);
  return <CurrentArticleContext value={value}>{children}</CurrentArticleContext>;
}

export const useCurrentArticle = () => useContext(CurrentArticleContext);

/** Madde sayfası açıkken maddeyi bağlama yazar, sayfa kapanınca siler. */
export function usePublishCurrentArticle(article: CurrentArticle) {
  const { setArticle } = useContext(CurrentArticleContext);
  const { articleId, versionId, label } = article;
  const blockOrder = article.blockOrder.join(",");
  const [previousHref, previousLabel] = [article.previous?.href, article.previous?.label];
  const [nextHref, nextLabel] = [article.next?.href, article.next?.label];
  useEffect(() => {
    setArticle({
      articleId,
      versionId,
      label,
      blockOrder: blockOrder ? blockOrder.split(",") : [],
      previous: previousHref ? { href: previousHref, label: previousLabel! } : null,
      next: nextHref ? { href: nextHref, label: nextLabel! } : null,
    });
    return () => setArticle(null);
  }, [setArticle, articleId, versionId, label, blockOrder, previousHref, previousLabel, nextHref, nextLabel]);
}
