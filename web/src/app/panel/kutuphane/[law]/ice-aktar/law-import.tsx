"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { AlertTriangle, ArrowLeft, Eye, FileInput, Lock } from "lucide-react";
import { toast } from "sonner";
import { useImportLaw, usePreviewLawImport } from "@/api/generated/admin-laws/admin-laws";
import { useGetLaw } from "@/api/generated/library/library";
import type {
  ArticleChange,
  ImportArticlePreview,
  ImportLawTextRequest,
  ImportNodePreview,
  LawImportPreview,
  LawUpdatePreview,
} from "@/api/generated/model";
import { emptyToNull, FormMessages, nodeKindLabels, otherMessages } from "@/components/panel/form";
import { useLibraryRefresh } from "@/components/panel/use-library-refresh";
import { paragraphKey, ParagraphList } from "@/components/workspace/article-text";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { formatDate } from "@/lib/dates";
import { cn } from "@/lib/utils";

/**
 * Kanunun tam metnini yapıştırıp içe aktarma ya da (kanun doluysa) güncel metinle güncelleme:
 * Önizle → kontrol et → uygula. Metin önizlemeden sonra değişirse yeniden önizlemek gerekir; kaydedilen her zaman
 * görülen önizlemedir.
 */
export function LawImport() {
  const { law: lawSlug } = useParams<{ law: string }>();
  const router = useRouter();
  const refresh = useLibraryRefresh();
  const lawQuery = useGetLaw(lawSlug);
  const tree = lawQuery.data?.status === 200 ? lawQuery.data.data : undefined;
  const law = tree?.law;
  const hasContent = tree !== undefined && (tree.nodes.length > 0 || tree.articles.length > 0);

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
    const { isUpdate, articleCount, nodeCount } = response.data;
    toast.success(
      isUpdate
        ? `${articleCount} madde güncellendi${nodeCount > 0 ? `, ${nodeCount} düğüm eklendi` : ""}.`
        : `${articleCount} madde ve ${nodeCount} düğüm içe aktarıldı.`,
    );
    router.push(`/panel/kutuphane/${law.slug}`);
  }

  if (!law) return <p className="mt-6 text-muted-foreground">Yükleniyor…</p>;

  const changeCount = preview?.update?.changes.length;
  const importLabel = importMutation.isPending
    ? "Kaydediliyor…"
    : changeCount === undefined
      ? `İçe aktar (${preview?.articleCount ?? 0} madde)`
      : changeCount === 0
        ? "Değişiklik yok"
        : `Değişiklikleri uygula (${changeCount} madde)`;

  return (
    <div className="mt-4 space-y-4">
      <div className="flex items-center gap-2">
        <Button variant="ghost" size="icon-sm" asChild>
          <Link href={`/panel/kutuphane/${law.slug}`} aria-label="Kanun yapısına dön" title="Kanun yapısına dön">
            <ArrowLeft />
          </Link>
        </Button>
        <h1 className="text-2xl">
          {hasContent ? "Güncel metinle güncelle" : "Metinden içe aktar"} — {law.shortName || law.number} {law.name}
        </h1>
      </div>

      <section className="space-y-4 rounded-lg border bg-card p-4">
        <p className="reading text-muted-foreground">
          mevzuat.gov.tr&apos;de kanunu açın, metnin içine tıklayıp tamamını seçin (Ctrl+A), kopyalayın (Ctrl+C) ve
          aşağıya yapıştırın. Kitap, kısım ve bölüm başlıkları, madde başlıkları, fıkra ve bentler otomatik ayrılır;
          metnin sonundaki değişiklik listesi ve dipnotlar alınmaz.
        </p>
        {hasContent && (
          <p className="rounded-md bg-accent px-3 py-2 text-sm text-accent-foreground">
            Bu kanunda içerik var: yapıştırdığınız metin mevcut maddelerle karşılaştırılır. Değişmeyen maddelere
            dokunulmaz; değişen madde, değişiklik notunda daha yeni bir tarih varsa yeni sürüm olarak eklenir (eski
            metin önceki tarihler için kalır), yoksa yürürlükteki metin düzeltilir. Metinde olmayan maddeler silinmez.
          </p>
        )}
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
            <p className="mt-1 text-xs text-muted-foreground">
              {hasContent
                ? "Notu olmayan değişiklikler için: girilen tarih yürürlükteki sürümden yeniyse yeni sürüm açılır."
                : "Maddelerin ilk sürümü bu tarihle açılır (TCK: 01.06.2005)."}
            </p>
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
              disabled={!upToDate || preview.blockers.length > 0 || importMutation.isPending || changeCount === 0}
              title={!upToDate ? "Metin ya da ayarlar değişti; önce yeniden önizleyin." : undefined}
            >
              <FileInput /> {importLabel}
            </Button>
          )}
          {preview && !upToDate && (
            <p className="self-center text-sm text-muted-foreground">Metin değişti; uygulamadan önce yeniden önizleyin.</p>
          )}
        </div>
      </section>

      {preview?.update ? (
        <UpdateResult key={JSON.stringify(previewed?.request)} preview={preview} update={preview.update} />
      ) : (
        preview && <PreviewResult preview={preview} selected={selectedArticle} onSelect={setSelectedArticle} />
      )}
    </div>
  );
}

function Messages({ preview }: { preview: LawImportPreview }) {
  return (
    <>
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
    </>
  );
}

function Facts({ facts }: { facts: { label: string; value: number }[] }) {
  return (
    <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
      {facts.map((fact) => (
        <div key={fact.label} className="rounded-lg border bg-card px-4 py-3">
          <p className="text-2xl">{fact.value}</p>
          <p className="text-sm text-muted-foreground">{fact.label}</p>
        </div>
      ))}
    </div>
  );
}

/** Dolu kanunda: hangi maddeler eklenecek/değişecek; seçilen maddenin kayıtlı ve yeni metni yan yana. */
function UpdateResult({ preview, update }: { preview: LawImportPreview; update: LawUpdatePreview }) {
  const [selectedSlug, setSelectedSlug] = useState<string | null>(update.changes[0]?.slug ?? null);
  const selected = update.changes.find((c) => c.slug === selectedSlug);

  return (
    <section className="space-y-4">
      <Facts
        facts={[
          { label: "Eklenecek / değişecek madde", value: update.changes.length },
          { label: "Değişmeyen madde", value: update.unchangedCount },
          { label: "Yeni düğüm", value: update.newNodeCount },
          { label: "Metinde olmayan madde", value: update.notInText.length },
        ]}
      />
      <Messages preview={preview} />
      {update.notInText.length > 0 && (
        <p className="rounded-lg border bg-card p-3 text-sm">
          <span className="font-medium">Metinde bulunmayan maddeler (silinmeyecek):</span>{" "}
          {update.notInText.map((n) => `Madde ${n}`).join(", ")}. Gerçekten kaldırıldıysa yapı ekranından silin.
        </p>
      )}
      {update.changes.length === 0 ? (
        <p className="rounded-lg border bg-card p-4 text-muted-foreground">
          Yapıştırılan metin kayıtlı metinle aynı; değişiklik yok.
        </p>
      ) : (
        <div className="grid gap-4 lg:grid-cols-[minmax(18rem,26rem)_1fr]">
          <ul className="max-h-[70vh] space-y-0.5 overflow-y-auto rounded-lg border bg-card p-2">
            {update.changes.map((change) => (
              <li key={change.slug}>
                <button
                  type="button"
                  onClick={() => setSelectedSlug(change.slug)}
                  className={cn(
                    "w-full rounded-md px-2 py-1.5 text-left text-sm",
                    change.slug === selectedSlug ? "bg-primary text-primary-foreground" : "hover:bg-accent",
                  )}
                >
                  <span className="font-medium">Madde {change.number}</span> {change.title}
                  <span className="mt-1 flex flex-wrap gap-1">
                    <ChangeBadges change={change} />
                  </span>
                </button>
              </li>
            ))}
          </ul>
          <div className="rounded-lg border bg-card p-4">{selected && <ChangeDetail change={selected} />}</div>
        </div>
      )}
    </section>
  );
}

function ChangeBadges({ change }: { change: ArticleChange }) {
  const badges: string[] = [];
  if (change.isNew) {
    badges.push("yeni madde");
  } else if (change.textChanged) {
    badges.push(
      change.asNewVersion
        ? `yeni sürüm${change.validFrom ? ` · ${formatDate(change.validFrom)}` : ""}`
        : "metin düzeltmesi",
    );
  }
  if (change.oldTitle !== null) badges.push("başlık değişti");
  if (change.moved) badges.push("yeri değişti");
  return badges.map((badge) => (
    <Badge key={badge} variant="secondary" className="font-normal">
      {badge}
    </Badge>
  ));
}

function ChangeDetail({ change }: { change: ArticleChange }) {
  // Bir tarafta olup öbür tarafta birebir bulunmayan fıkralar işaretlenir.
  const oldKeys = new Set(change.oldParagraphs.map(paragraphKey));
  const newKeys = new Set(change.newParagraphs.map(paragraphKey));
  const changedNew = new Set(change.newParagraphs.filter((p) => !oldKeys.has(paragraphKey(p))).map((p) => p.id));
  const changedOld = new Set(change.oldParagraphs.filter((p) => !newKeys.has(paragraphKey(p))).map((p) => p.id));

  const explanation = change.isNew
    ? `Yeni madde${change.validFrom ? `, ${formatDate(change.validFrom)} tarihinden itibaren` : ""}.`
    : change.asNewVersion
      ? `Yeni sürüm${change.validFrom ? ` ${formatDate(change.validFrom)} tarihinden itibaren` : ""}; kayıtlı metin bu tarihten önceki sorgularda gösterilmeye devam eder.`
      : change.textChanged
        ? "Değişiklik notunda daha yeni bir tarih yok; yürürlükteki metin yerinde düzeltilecek."
        : "Metin aynı.";

  return (
    <div className="space-y-3">
      <h3 className="text-xl">
        Madde {change.number} <span className="text-primary">— {change.title}</span>
      </h3>
      {change.oldTitle !== null && (
        <p className="text-sm">
          Başlık: <s className="text-muted-foreground">{change.oldTitle}</s> → {change.title}
        </p>
      )}
      <p className="text-sm text-muted-foreground">
        {explanation}
        {change.amendedBy && ` · ${change.amendedBy}`}
      </p>
      {change.textChanged && (
        <div className="grid gap-4 xl:grid-cols-2">
          {!change.isNew && (
            <div>
              <p className="mb-1.5 text-sm font-medium">Kayıtlı metin</p>
              <div className="rounded-md border p-3">
                <ParagraphList paragraphs={change.oldParagraphs} marked={changedOld} />
              </div>
            </div>
          )}
          <div>
            <p className="mb-1.5 text-sm font-medium">Yapıştırılan metin</p>
            <div className="rounded-md border p-3">
              <ParagraphList paragraphs={change.newParagraphs} marked={changedNew} />
            </div>
          </div>
        </div>
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
      <Facts facts={facts} />
      {metadataText && <p className="text-sm text-muted-foreground">Künye: {metadataText}</p>}
      <Messages preview={preview} />

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
