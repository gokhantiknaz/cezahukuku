import { Suspense } from "react";
import type { Metadata } from "next";
import { ExternalLink, Lock } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { formatDate } from "@/lib/dates";
import { getNews } from "@/lib/server-api";

export const metadata: Metadata = {
  title: "Ceza Gündemi",
  description: "Ceza hukukundaki son gelişmeler: kanun değişiklikleri, önemli kararlar ve duyurular.",
};

export default function NewsPage() {
  return (
    <div className="mx-auto w-full max-w-3xl px-4 py-6">
      <h1 className="text-3xl">Ceza Gündemi</h1>
      <p className="mt-1 text-muted-foreground">Kanun değişiklikleri, önemli kararlar ve duyurular.</p>
      {/* Giriş yapan kullanıcı üyelere özel haberleri de görür (oturum çerezi çalışma anında okunur). */}
      <Suspense fallback={<p className="mt-6 text-muted-foreground">Yükleniyor…</p>}>
        <NewsList />
      </Suspense>
    </div>
  );
}

async function NewsList() {
  const news = await getNews();
  if (news.length === 0) return <p className="mt-6 text-muted-foreground">Henüz haber yok.</p>;
  return (
    <ol className="mt-5 space-y-3">
      {news.map((item) => (
        <li key={item.id} id={item.id} className="scroll-mt-4 rounded-lg border bg-card p-4 target:border-primary">
          <p className="flex items-center gap-2 text-sm text-muted-foreground">
            {formatDate(item.publishedOn)}
            {item.visibility === "private" && (
              <Badge variant="outline">
                <Lock /> Üyelere
              </Badge>
            )}
          </p>
          <h2 className="mt-1 text-xl">{item.title}</h2>
          {item.summary && <p className="reading mt-1 text-[16px] whitespace-pre-wrap">{item.summary}</p>}
          {item.url && (
            <a
              href={item.url}
              target="_blank"
              rel="noreferrer noopener"
              className="mt-2 inline-flex items-center gap-1 text-primary hover:underline"
            >
              Kaynağa git <ExternalLink className="size-4" aria-hidden />
            </a>
          )}
        </li>
      ))}
    </ol>
  );
}
