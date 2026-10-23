"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { AlertTriangle, ArrowLeft, Eye, FileInput, Lock } from "lucide-react";
import { toast } from "sonner";
import { useImportLaw, usePreviewLawImport } from "@/api/generated/admin-laws/admin-laws";
import { useGetLaw } from "@/api/generated/library/library";
import type {
  ImportArticlePreview,
  ImportLawTextRequest,
  ImportNodePreview,
  LawImportPreview,
} from "@/api/generated/model";
import { emptyToNull, FormMessages, nodeKindLabels, otherMessages } from "@/components/panel/form";
import { useLibraryRefresh } from "@/components/panel/use-library-refresh";
import { ParagraphList } from "@/components/workspace/article-text";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { formatDate } from "@/lib/dates";
import { cn } from "@/lib/utils";

/**
 * Kanunun tam metnini yapıştırıp içe aktarma: Önizle → kontrol et (ağaç, madde metinleri, uyarılar) → İçe aktar.
 * Metin önizlemeden sonra değişirse içe aktarma için yeniden önizlemek gerekir; kaydedilen yapı her zaman
 * görülen önizlemeyle aynıdır.
 */
export function LawImport() {
  const { law: lawSlug } = useParams<{ law: string }>();
  const router = useRouter();
  const refresh = useLibraryRefresh();
  const lawQuery = useGetLaw(lawSlug);
  const law = lawQuery.data?.status === 200 ? lawQuery.data.data.law : undefined;

  const [form, setForm] = useState({ text: "", joinWrappedLines: false, validFrom: "", applyMetadata: true });
  const [previewed, setPreviewed] = useState<{ request: ImportLawTextRequest; result: LawImportPreview } | null>(
    null,
  );
  const [selectedArticle, setSelectedArticle] = useState<ImportArticlePreview | null>(null);
  const previewMutation = usePreviewLawImport();
  const importMutation = useImportLaw();

  const request: ImportLawTextRequest = {
    text: form.text,
    joinWrappedLines: form.joinWrappedLines,
    validFrom: emptyToNull(form.validFrom),
    applyMetadata: form.applyMetadata,
  };
  const upToDate = previewed !== null && JSON.stringify(previewed.request) === JSON.stringify(request);
  const preview = previewed?.result;

  async function runPreview() {
    if (!law) return;
    const response = await previewMutation.mutateAsync({ lawId: law.id, data: request });
    if (response.status === 200) {
      setPreviewed({ request, result: response.data });
      setSelectedArticle(null);
    }
  }

  async function runImport() {
    if (!law) return;
    const response = await importMutation.mutateAsync({ lawId: law.id, data: request });
    if (response.status !== 200) return;
    await refresh();
    toast.success(`${response.data.articleCount} madde ve ${response.data.nodeCount} düğüm içe aktarıldı.`);
    router.push(`/panel/kutuphane/${law.slug}`);
  }

  if (!law) return <p className="mt-6 text-muted-foreground">Yükleniyor…</p>;

  return (
    <div className="mt-4 space-y-4">
      <div className="flex items-center gap-2">
        <Button variant="ghost" size="icon-sm" asChild>
          <Link href={`/panel/kutuphane/${law.slug}`} aria-label="Kanun yapısına dön" title="Kanun yapısına dön">
            <ArrowLeft />
          </Link>
        </Button>
        <h1 className="text-2xl">
          Metinden içe aktar — {law.shortName || law.number} {law.name}
        </h1>
      </div>

      <section className="space-y-4 rounded-lg border bg-card p-4">
        <p className="reading text-muted-foreground">
          mevzuat.gov.tr&apos;de kanunu açın, metnin içine tıklayıp tamamını seçin (Ctrl+A), kopyalayın (Ctrl+C) ve
          aşağıya yapıştırın. Kitap, kısım ve bölüm başlıkları, madde başlıkları, fıkra ve bentler otomatik ayrılır;
          metnin sonundaki değişiklik listesi ve dipnotlar alınmaz.
        </p>
        <Textarea
          value={form.text}
          onChange={(e) => setForm((f) => ({ ...f, text: e.target.value }))}
          placeholder={"TÜRK CEZA KANUNU\nKanun Numarası : 5237\n…\nBİRİNCİ KİTAP\nGenel Hükümler\n…\nCeza Kanununun amacı\nMadde 1- (1) …"}
          className="h-72 font-reading text-sm"
          aria-label="Kanun metni"
        />
        <div className="grid gap-4 md:grid-cols-[14rem_1fr] md:items-start">
          <div>
            <Label htmlFor="import-valid-from" className="mb-1.5">
              Yürürlük başlangıcı
            </Label>
            <Input
              id="import-valid-from"
              type="date"
              value={form.validFrom}
              onChange={(e) => setForm((f) => ({ ...f, validFrom: e.target.value }))}
            />
            <p className="mt-1 text-xs text-muted-foreground">Maddelerin ilk sürümü bu tarihle açılır (TCK: 01.06.2005).</p>
          </div>
          <div className="space-y-2 md:pt-6">
            <label className="flex items-start gap-2">
              <Checkbox
                checked={form.joinWrappedLines}
                onCheckedChange={(checked) => setForm((f) => ({ ...f, joinWrappedLines: checked === true }))}
                className="mt-0.5"
              />
              <span>
                Metni PDF&apos;ten kopyaladım{" "}
                <span className="text-muted-foreground">(cümle ortasında kırılan satırlar birleştirilsin)</span>
              </span>
            </label>
            <label className="flex items-start gap-2">
              <Checkbox
                checked={form.applyMetadata}
                onCheckedChange={(checked) => setForm((f) => ({ ...f, applyMetadata: checked === true }))}
                className="mt-0.5"
              />
              <span>
                Künyedeki kabul ve Resmî Gazete bilgilerini kanuna yaz{" "}
                <span className="text-muted-foreground">(yalnız boş alanlar)</span>
              </span>
            </label>
          </div>
        </div>
        <FormMessages messages={otherMessages(previewMutation.error ?? importMutation.error, [])} />
        <div className="flex flex-wrap gap-2">
          <Button
            variant={upToDate ? "outline" : "default"}
            onClick={() => runPreview().catch(() => {})}
            disabled={previewMutation.isPending || form.text.trim() === ""}
          >
            <Eye /> {previewMutation.isPending ? "Ayrıştırılıyor…" : "Önizle"}
          </Button>
          {preview && (
            <Button
              onClick={() => runImport().catch(() => {})}
              disabled={!upToDate || preview.blockers.length > 0 || importMutation.isPending}
              title={!upToDate ? "Metin ya da ayarlar değişti; önce yeniden önizleyin." : undefined}
            >
              <FileInput /> {importMutation.isPending ? "İçe aktarılıyor…" : `İçe aktar (${preview.articleCount} madde)`}
            </Button>
          )}
          {preview && !upToDate && (
            <p className="self-center text-sm text-muted-foreground">Metin değişti; içe aktarmadan önce yeniden önizleyin.</p>
          )}
        </div>
      </section>

      {preview && (
        <PreviewResult preview={preview} selected={selectedArticle} onSelect={setSelectedArticle} />
      )}
    </div>
  );
}

function PreviewResult({
  preview,
  selected,
  onSelect,
}: {
  preview: LawImportPreview;
  selected: ImportArticlePreview | null;
  onSelect: (article: ImportArticlePreview) => void;
}) {
  const { metadata } = preview;
  const facts = [
    { label: "Düğüm", value: preview.nodeCount },
    { label: "Madde", value: preview.articleCount },
    { label: "Mülga madde", value: preview.repealedCount },
    { label: "Alınmayan dipnot", value: preview.footnoteCount },
  ];
  const metadataText = [
    metadata.number && `Kanun no ${metadata.number}`,
    metadata.acceptedOn && `Kabul ${formatDate(metadata.acceptedOn)}`,
    metadata.gazetteDate && `RG ${formatDate(metadata.gazetteDate)}`,
    metadata.gazetteNumber && `Sayı ${metadata.gazetteNumber}`,
  ]
    .filter(Boolean)
    .join(" · ");

  return (
    <section className="space-y-4">
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        {facts.map((fact) => (
          <div key={fact.label} className="rounded-lg border bg-card px-4 py-3">
            <p className="text-2xl">{fact.value}</p>
            <p className="text-sm text-muted-foreground">{fact.label}</p>
          </div>
        ))}
      </div>
      {metadataText && <p className="text-sm text-muted-foreground">Künye: {metadataText}</p>}

      {preview.blockers.length > 0 && (
        <div role="alert" className="rounded-lg border border-destructive/40 bg-destructive/5 p-3 text-destructive">
          {preview.blockers.map((message) => (
            <p key={message} className="flex items-start gap-2">
              <Lock className="mt-0.5 size-4 shrink-0" /> {message}
            </p>
          ))}
        </div>
      )}
      {preview.warnings.length > 0 && (
        <div className="rounded-lg border bg-accent p-3 text-sm text-accent-foreground">
          {preview.warnings.map((message) => (
            <p key={message} className="flex items-start gap-2">
              <AlertTriangle className="mt-0.5 size-4 shrink-0" /> {message}
            </p>
          ))}
        </div>
      )}

      <div className="grid gap-4 lg:grid-cols-[minmax(18rem,28rem)_1fr]">
        <div className="max-h-[70vh] overflow-y-auto rounded-lg border bg-card p-2">
          <p className="px-2 pb-2 text-sm text-muted-foreground">Maddeye tıklayarak ayrıştırılan metni kontrol edin.</p>
          <ArticleRows articles={preview.articles} depth={0} selected={selected} onSelect={onSelect} />
          {preview.nodes.map((node, index) => (
            <NodeRows key={index} node={node} depth={0} selected={selected} onSelect={onSelect} />
          ))}
        </div>
        <div className="rounded-lg border bg-card p-4">
          {selected ? (
            <>
              <h3 className="text-xl">
                Madde {selected.number}
                {selected.title && <span className="text-primary"> — {selected.title}</span>}
              </h3>
              <p className="text-sm text-muted-foreground">
                {selected.validFrom
                  ? `${formatDate(selected.validFrom)} tarihinden itibaren geçerli sayılacak`
                  : "Yürürlük başlangıcı girilmedi"}
                {selected.amendedBy && ` · son değişiklik notu: ${selected.amendedBy}`}
                {selected.isRepealed && " · mülga olarak işaretlenecek"}
              </p>
              <div className="mt-3">
                <ParagraphList paragraphs={selected.paragraphs} />
              </div>
            </>
          ) : (
            <p className="text-muted-foreground">Soldan bir madde seçin.</p>
          )}
        </div>
      </div>
    </section>
  );
}

function NodeRows({
  node,
  depth,
  selected,
  onSelect,
}: {
  node: ImportNodePreview;
  depth: number;
  selected: ImportArticlePreview | null;
  onSelect: (article: ImportArticlePreview) => void;
}) {
  return (
    <details open={depth < 1} className="group">
      <summary
        className="cursor-pointer rounded-md px-2 py-1 hover:bg-accent"
        style={{ paddingLeft: depth * 14 + 8 }}
      >
        <span className="font-medium">{node.number}</span>{" "}
        <span className="text-sm text-muted-foreground">
          {nodeKindLabels[node.kind]} · {node.title || "(başlık yok)"}
        </span>
      </summary>
      <ArticleRows articles={node.articles} depth={depth + 1} selected={selected} onSelect={onSelect} />
      {node.children.map((child, index) => (
        <NodeRows key={index} node={child} depth={depth + 1} selected={selected} onSelect={onSelect} />
      ))}
    </details>
  );
}

function ArticleRows({
  articles,
  depth,
  selected,
  onSelect,
}: {
  articles: ImportArticlePreview[];
  depth: number;
  selected: ImportArticlePreview | null;
  onSelect: (article: ImportArticlePreview) => void;
}) {
  if (articles.length === 0) return null;
  return (
    <ul>
      {articles.map((article) => (
        <li key={article.slug} style={{ paddingLeft: depth * 14 }}>
          <button
            type="button"
            onClick={() => onSelect(article)}
            className={cn(
              "grid w-full grid-cols-[6rem_1fr] gap-1 rounded-md px-2 py-0.5 text-left text-sm",
              selected?.slug === article.slug ? "bg-primary text-primary-foreground" : "hover:bg-accent",
            )}
          >
            <span>Madde {article.number}</span>
            <span className={cn("truncate", !article.title && "italic opacity-70")}>
              {article.title || (article.isRepealed ? "(mülga)" : "(başlık yok)")}
            </span>
          </button>
        </li>
      ))}
    </ul>
  );
}
