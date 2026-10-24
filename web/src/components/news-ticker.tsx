"use client";

import Link from "next/link";
import { Megaphone } from "lucide-react";
import { useListNews } from "@/api/generated/news/news";
import { formatDate } from "@/lib/dates";

const placeholder = "Ceza Gündemi haberleri yönetim panelinden eklendiğinde burada akacak.";

/** Alttaki Ceza Gündemi şeridi: son haberler kayarak geçer (üstüne gelince durur); habere tıklayınca gündem sayfası açılır. */
export function NewsTicker() {
  const { data } = useListNews({ limit: 10 }, { query: { staleTime: 5 * 60_000 } });
  const news = data?.status === 200 && data.data.length > 0 ? data.data : null;
  const items = news
    ? news.map((n) => ({ id: n.id, text: `${formatDate(n.publishedOn)} · ${n.title}` }))
    : [{ id: "bos", text: placeholder }];
  // Kesintisiz kayma için liste iki kez yan yana basılır, animasyon yarı genişlikte başa sarar.
  const loop = [...items, ...items];

  return (
    <footer className="flex items-center gap-4 overflow-hidden bg-primary px-4 py-2 text-primary-foreground print:hidden">
      <Link href="/gundem" className="flex shrink-0 items-center gap-2" title="Ceza Gündemi">
        <Megaphone className="size-6" aria-hidden />
        <span className="rounded bg-primary-foreground px-2 py-1 text-sm font-bold text-primary">CEZA GÜNDEMİ</span>
      </Link>
      <div className="min-w-0 flex-1 overflow-hidden">
        <ul className="animate-ticker flex w-max gap-12 whitespace-nowrap hover:[animation-play-state:paused]">
          {loop.map((item, index) => {
            const copy = index >= items.length;
            return (
              <li key={`${item.id}-${index}`} className="flex items-center gap-2" aria-hidden={copy}>
                <span className="size-2 rounded-full bg-primary-foreground/80" />
                {news ? (
                  <Link href={`/gundem#${item.id}`} tabIndex={copy ? -1 : undefined} className="hover:underline">
                    {item.text}
                  </Link>
                ) : (
                  item.text
                )}
              </li>
            );
          })}
        </ul>
      </div>
    </footer>
  );
}
