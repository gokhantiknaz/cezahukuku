"use client";

import { useEffect, useRef, type ReactNode } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { ArrowLeft, Download, ExternalLink } from "lucide-react";
import { useGetAttachment } from "@/api/generated/personal/personal";
import type { DocumentCitationSpan } from "@/api/generated/model";
import { Button } from "@/components/ui/button";
import { attachmentFileUrl, formatBytes, formatDateTime, formatSeconds } from "@/lib/attachments";
import { cn } from "@/lib/utils";
import { StatusBadge } from "./documents-page";

/** Metni atıf bağlantılarıyla böler ("TCK m. 81" → madde sayfası). */
function withCitations(text: string, spans: DocumentCitationSpan[]): ReactNode {
  if (spans.length === 0) return text;
  const parts: ReactNode[] = [];
  let position = 0;
  for (const span of spans) {
    if (span.start < position) continue;
    parts.push(text.slice(position, span.start));
    parts.push(
      <Link
        key={span.start}
        href={`/${span.target.lawSlug}/${span.target.articleSlug}`}
        title={`${span.target.lawLabel} Madde ${span.target.number}${span.target.title ? ` — ${span.target.title}` : ""}`}
        className="text-primary underline decoration-primary/40 decoration-dotted underline-offset-4 hover:decoration-solid"
      >
        {text.slice(span.start, span.start + span.length)}
      </Link>,
    );
    position = span.start + span.length;
  }
  parts.push(text.slice(position));
  return parts;
}

/**
 * Belge görüntüleyici: PDF'in sayfa sayfa çıkarılmış metni (madde atıfları bağlantılı) ya da sesli notun metni.
 * ?sayfa=3 ile o sayfaya kaydırılır (arama sonucundan gelince).
 */
export function DocumentViewer({ id }: { id: string }) {
  const { data, error } = useGetAttachment(id, {
    query: {
      refetchInterval: (query) => {
        const response = query.state.data;
        return response?.status === 200 && response.data.attachment.status === "pending" ? 2000 : false;
      },
    },
  });
  const page = Number(useSearchParams().get("sayfa")) || null;
  const pageRef = useRef<HTMLElement>(null);
  const detail = data?.status === 200 ? data.data : null;

  useEffect(() => {
    if (detail && page) pageRef.current?.scrollIntoView({ block: "start" });
  }, [detail, page]);

  if (error) return <p className="mt-6 text-muted-foreground">Belge bulunamadı.</p>;
  if (!detail) return <p className="mt-6 text-muted-foreground">Yükleniyor…</p>;

  const { attachment: a, segments } = detail;
  const pdf = a.kind === "pdf";
  return (
    <div className="mt-2">
      <Button variant="ghost" size="sm" asChild>
        <Link href="/belgelerim">
          <ArrowLeft /> Belgelerim
        </Link>
      </Button>
      <header className="mt-2 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-3xl">{a.title}</h1>
          <p className="text-sm text-muted-foreground">
            {[a.fileName, formatBytes(a.sizeBytes), a.pageCount != null ? `${a.pageCount} sayfa` : null, formatDateTime(a.createdAt)]
              .filter(Boolean)
              .join(" · ")}
          </p>
          {a.article && (
            <Link href={`/${a.article.lawSlug}/${a.article.articleSlug}`} className="text-sm text-primary hover:underline">
              {a.article.lawLabel} Madde {a.article.number} — {a.article.title}
            </Link>
          )}
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <StatusBadge attachment={a} />
          {pdf && (
            <Button variant="outline" asChild>
              <a href={attachmentFileUrl(a.id)} target="_blank" rel="noreferrer">
                <ExternalLink /> PDF&apos;i aç
              </a>
            </Button>
          )}
          <Button variant="outline" asChild>
            <a href={attachmentFileUrl(a.id, true)}>
              <Download /> İndir
            </a>
          </Button>
        </div>
      </header>

      {!pdf && <audio controls preload="metadata" src={attachmentFileUrl(a.id)} className="mt-4 w-full" />}

      {a.status === "pending" ? (
        <p className="mt-6 text-muted-foreground">Metin çıkarılıyor…</p>
      ) : a.status === "failed" ? (
        <p className="mt-6 text-destructive">{a.error}</p>
      ) : segments.length === 0 ? (
        <p className="mt-6 text-muted-foreground">
          {pdf ? "Bu belgede metin bulunamadı (taranmış görüntü olabilir)." : "Bu sesli notun metni yok."}
        </p>
      ) : (
        <div className="mt-6 space-y-4">
          {segments.map((s) => (
            <section
              key={s.id}
              ref={pdf && s.page === page ? pageRef : undefined}
              id={pdf ? `sayfa-${s.page}` : undefined}
              className={cn("scroll-mt-4 rounded-lg border bg-card p-4", pdf && s.page === page && "border-primary")}
            >
              <h2 className="mb-2 text-sm font-medium text-muted-foreground">
                {pdf ? `Sayfa ${s.page}` : s.startSeconds != null ? formatSeconds(s.startSeconds) : null}
              </h2>
              <p className="reading whitespace-pre-wrap">{withCitations(s.text, s.citations)}</p>
            </section>
          ))}
        </div>
      )}
    </div>
  );
}
