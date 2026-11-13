"use client";

import { useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { AlertTriangle, ArrowLeft, CircleX, Eye, FileUp, FolderTree } from "lucide-react";
import { toast } from "sonner";
import { useExtractImportText } from "@/api/generated/admin-laws/admin-laws";
import { useImportLectureCourse, usePreviewLectureImport } from "@/api/generated/lecture-courses/lecture-courses";
import type { LectureImportIssue, LectureImportPreview, Visibility } from "@/api/generated/model";
import { Field, FormMessages, OptionSelect, otherMessages, visibilityLabels } from "@/components/panel/form";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { useHydrated } from "@/hooks/use-hydrated";
import { isAdmin, useCurrentUser } from "@/lib/auth";
import { cn } from "@/lib/utils";

type Previewed = { text: string; title: string; chapter: string; result: LectureImportPreview };

/**
 * Spec 0004: ders başlık listesini yapıştır ya da dosyadan al → önizle → düzelt → yeni dersi kur. Metin değişince
 * önizleme metinden önerilen ad ve bölüm numarasını alanlara yazar; alanlar ya da metin önizlemeden sonra değişirse
 * önizleme "güncel değil" olur ve ders kurulamaz. Kaydedilen her zaman görülen önizlemedir (sunucu metni yeniden okur).
 */
export function LectureImport() {
  const router = useRouter();
  const hydrated = useHydrated();
  const { data: user, isLoading } = useCurrentUser();
  const [text, setText] = useState("");
  const [title, setTitle] = useState("");
  const [chapter, setChapter] = useState("");
  const [visibility, setVisibility] = useState<Visibility>("private");
  const [previewed, setPreviewed] = useState<Previewed | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const extract = useExtractImportText();
  const previewMutation = usePreviewLectureImport();
  const importMutation = useImportLectureCourse();

  if (!hydrated || isLoading) return <p className="mt-6 text-muted-foreground">Yükleniyor…</p>;
  if (!isAdmin(user)) return <p className="mt-6 text-muted-foreground">Başlıkları yalnız yönetici içe aktarabilir.</p>;

  const upToDate = previewed !== null && previewed.text === text && previewed.title === title && previewed.chapter === chapter;
  const preview = previewed?.result;
  const canImport = upToDate && preview !== undefined && preview.errors.length === 0 && !importMutation.isPending;

  /** Word (.docx), PDF ya da metin dosyasının metni kutuya alınır. */
  async function loadFile(file: File) {
    try {
      const response = await extract.mutateAsync({ data: { file } });
      if (response.status !== 200) return;
      setText(response.data.text);
      toast.success(`“${file.name}” metni alındı; önizleyip kontrol edin.`);
    } catch (error) {
      toast.error(otherMessages(error, [])[0] ?? "Dosya okunamadı.");
    } finally {
      if (fileRef.current) fileRef.current.value = "";
    }
  }

  async function runPreview() {
    // Metin son önizlemeden beri değiştiyse metinden önerilen ad ve bölüm numarası kullanılır; ama yalnız yöneticinin
    // değiştirmediği alana (boş ya da hâlâ bir önceki önizlemenin önerisi): elle girilen değer ezilmez.
    const textChanged = previewed === null || previewed.text !== text;
    const untouched = (value: string, suggestion: string | null | undefined) =>
      value.trim() === "" || (previewed !== null && value === (suggestion ?? ""));
    const response = await previewMutation.mutateAsync({
      data: {
        text,
        title: title.trim() || null,
        chapterNumber: chapter.trim() ? Number(chapter) : null,
        applyTitleSuggestion: textChanged && untouched(title, previewed?.result.suggestedTitle),
        applyChapterSuggestion: textChanged && untouched(chapter, previewed?.result.suggestedChapterNumber?.toString()),
      },
    });
    if (response.status !== 200) return;
    const result = response.data;
    const usedChapter = result.chapterNumber?.toString() ?? "";
    setTitle(result.title);
    setChapter(usedChapter);
    setPreviewed({ text, title: result.title, chapter: usedChapter, result });
  }

  async function runImport() {
    const response = await importMutation.mutateAsync({
      data: { text, title, chapterNumber: chapter.trim() ? Number(chapter) : null, visibility },
    });
    if (response.status !== 201) return;
    toast.success(`Ders kuruldu; ${response.data.headingCount} başlık aktarıldı.`);
    router.push(`/ders-notlari/${response.data.slug}`);
    router.refresh();
  }

  const summary = preview
    ? `${preview.headings.length} başlık; ${preview.errors.length} hata, ${preview.warnings.length} uyarı.`
    : "";

  return (
    <div className="mt-4 space-y-4">
      <Button variant="ghost" size="sm" asChild>
        <Link href="/ders-notlari">
          <ArrowLeft /> Ders Notları
        </Link>
      </Button>

      <section className="rounded-lg border bg-card p-4">
        {/* Kayıt sürerken form kilitli: gönderilenle ekrandaki metin ayrışmasın. */}
        <fieldset disabled={importMutation.isPending} className="min-w-0 space-y-4">
        <p className="reading text-muted-foreground">
          Ders kitabının başlık listesini yapıştırın ya da Word/PDF dosyasından alın. Her başlık numarasıyla bir satırda
          olmalı (&quot;2.1.1. Tipiklik&quot;); düzey numaradan anlaşılır, numaraları sistem yeniden hesaplar. Tek bölümlük
          bir listede &quot;2. BÖLÜM – SUÇ TEORİSİ&quot; satırı dersin adı ve bölüm numarası olur. Yalnız başlık adları
          aktarılır; her aktarım yeni bir ders kurar.
        </p>
        <div className="flex flex-wrap gap-2">
          <input
            ref={fileRef}
            type="file"
            accept=".docx,.pdf,.txt"
            className="hidden"
            onChange={(e) => e.target.files?.[0] && loadFile(e.target.files[0])}
          />
          <Button type="button" variant="outline" disabled={extract.isPending} onClick={() => fileRef.current?.click()}>
            <FileUp /> {extract.isPending ? "Dosya okunuyor…" : "Dosyadan al (.docx, .pdf, .txt)"}
          </Button>
        </div>
        <Textarea
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder={"2. BÖLÜM – SUÇ TEORİSİ\n2.1. Suçun Unsurları\n2.1.1. Tipiklik\n2.1.2. Hukuka Aykırılık\n…"}
          className="h-72 font-reading text-sm"
          aria-label="Başlık listesi"
        />
        <div className="grid gap-4 sm:grid-cols-[1fr_8rem_12rem]">
          <Field label="Dersin adı" htmlFor="import-title" error={importMutation.error} field="title">
            <Input id="import-title" value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Önizleyince önerilir" />
          </Field>
          <Field label="Bölüm numarası" htmlFor="import-chapter" error={importMutation.error} field="chapterNumber">
            <Input
              id="import-chapter"
              type="number"
              inputMode="numeric"
              min={1}
              max={99}
              step={1}
              value={chapter}
              onChange={(e) => setChapter(e.target.value)}
            />
          </Field>
          <Field label="Görünürlük" htmlFor="import-visibility">
            <OptionSelect id="import-visibility" value={visibility} options={visibilityLabels} onChange={setVisibility} />
          </Field>
        </div>
        <p className="text-xs text-muted-foreground">
          Görünürlük varsayılan olarak özel: ders hazır olana kadar yalnız giriş yapanlar görür; sonra dersin düzenleme
          penceresinden herkese açabilirsiniz.
        </p>
        <FormMessages messages={otherMessages(previewMutation.error ?? importMutation.error, ["title", "chapterNumber"])} />
        <div className="flex flex-wrap items-center gap-2">
          <Button
            variant={upToDate ? "outline" : "default"}
            onClick={() => runPreview().catch(() => {})}
            disabled={previewMutation.isPending || text.trim() === ""}
          >
            <Eye /> {previewMutation.isPending ? "Okunuyor…" : "Önizle"}
          </Button>
          <Button onClick={() => runImport().catch(() => {})} disabled={!canImport}>
            <FolderTree /> {importMutation.isPending ? "Kuruluyor…" : `Dersi kur${preview && upToDate ? ` (${preview.headings.length} başlık)` : ""}`}
          </Button>
          {previewed && !upToDate && (
            <span className="text-sm text-muted-foreground">Önizleme güncel değil: yeniden önizleyin.</span>
          )}
        </div>
        <p role="status" aria-live="polite" className="sr-only">
          {summary}
        </p>
        </fieldset>
      </section>

      {preview && (
        <section className={cn("space-y-4 rounded-lg border bg-card p-4", !upToDate && "opacity-60")} aria-labelledby="import-preview">
          <h2 id="import-preview" className="text-xl">
            Önizleme <span className="text-base text-muted-foreground">— {summary}</span>
          </h2>
          <Issues title="Hatalar (düzeltilmeden ders kurulamaz)" issues={preview.errors} tone="error" />
          <Issues title="Uyarılar" issues={preview.warnings} tone="warning" />
          {preview.headings.length > 0 && (
            <ol className="max-h-[32rem] space-y-0.5 overflow-y-auto rounded-md border p-2 text-sm">
              {preview.headings.map((h) => (
                <li key={h.line} className="flex gap-2" style={{ paddingLeft: `${(h.depth - 1) * 1.25}rem` }}>
                  <span className="shrink-0 font-medium text-primary">{h.label}</span>
                  <span className="min-w-0 break-words">{h.title}</span>
                </li>
              ))}
            </ol>
          )}
        </section>
      )}
    </div>
  );
}

function Issues({ title, issues, tone }: { title: string; issues: LectureImportIssue[]; tone: "error" | "warning" }) {
  if (issues.length === 0) return null;
  const Icon = tone === "error" ? CircleX : AlertTriangle;
  return (
    <div className={cn("rounded-md border px-3 py-2 text-sm", tone === "error" ? "border-destructive/50" : "border-amber-500/50")}>
      <p className={cn("flex items-center gap-1.5 font-medium", tone === "error" ? "text-destructive" : "text-amber-700 dark:text-amber-400")}>
        <Icon className="size-4" aria-hidden /> {title} ({issues.length})
      </p>
      <ul className="mt-1 space-y-0.5">
        {issues.map((issue, index) => (
          <li key={`${issue.line}-${index}`}>
            {issue.line > 0 && <span className="text-muted-foreground">Satır {issue.line}: </span>}
            {issue.message}
          </li>
        ))}
      </ul>
    </div>
  );
}
