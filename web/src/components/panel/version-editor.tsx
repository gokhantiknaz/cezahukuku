"use client";

import { useState } from "react";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { Trash2 } from "lucide-react";
import { toast } from "sonner";
import { ApiError } from "@/api/fetcher";
import {
  parseArticleText,
  useCreateArticleVersion,
  useDeleteArticleVersion,
  useUpdateArticleVersion,
} from "@/api/generated/admin-laws/admin-laws";
import type { ArticleVersionDetail, ParagraphDto } from "@/api/generated/model";
import { ParagraphList, paragraphsToText } from "@/components/workspace/article-text";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useDebouncedValue } from "@/hooks/use-debounced-value";
import { ConfirmDelete } from "./confirm-delete";
import { emptyToNull, Field, FormMessages, otherMessages } from "./form";
import { useLibraryRefresh } from "./use-library-refresh";

const versionFields = ["validFrom", "validTo", "amendedBy", "gazetteNumber"];

/** "paragraphs[1].items[0].text" → "2. fıkra, 1. satır" */
function describeParagraphField(field: string) {
  const match = /^paragraphs\[(\d+)\](?:\.items\[(\d+)\])?/.exec(field);
  if (!match) return null;
  const paragraph = `${Number(match[1]) + 1}. fıkra`;
  return match[2] !== undefined ? `${paragraph}, ${Number(match[2]) + 1}. satır` : paragraph;
}

/** Fıkra hataları metin kutusunun altında, alanı olmayan hatalar formun altında gösterilir. */
function splitMessages(error: unknown) {
  const errors = error instanceof ApiError ? (error.problem?.errors ?? {}) : {};
  const textMessages = Object.entries(errors)
    .filter(([key]) => key.startsWith("paragraphs"))
    .flatMap(([key, messages]) => {
      const where = describeParagraphField(key);
      return messages.map((m) => (where ? `${where}: ${m}` : m));
    });
  const paragraphKeys = Object.keys(errors).filter((key) => key.startsWith("paragraphs"));
  return { textMessages, generalMessages: otherMessages(error, [...versionFields, ...paragraphKeys]) };
}

const textPlaceholder = `(1) Kasten öldürme suçunun;
a) Tasarlayarak,
b) Canavarca hisle veya eziyet çektirerek,
İşlenmesi hâlinde, kişi ağırlaştırılmış müebbet hapis cezası ile cezalandırılır.
(2) ...`;

async function parse(text: string, baseVersionId: string | null): Promise<ParagraphDto[]> {
  if (text.trim() === "") return [];
  const response = await parseArticleText({ text, baseVersionId });
  return response.status === 200 ? response.data : [];
}

/**
 * Madde metninin bir sürümü. "Düzenle" sürümü yerinde düzeltir (yazım hatası, eksik tarih); "Yeni sürüm" bir
 * değişikliği kaydeder ve yürürlükteki sürümü yeni sürümün başlangıcından bir gün önce kapatır.
 * Metin mevzuattaki gibi yazılır/yapıştırılır; fıkra ve bentlere ayırma sunucuda yapılır, önizlemede görünür.
 */
export function VersionEditor({
  articleId,
  mode,
  base,
  onSaved,
  onDeleted,
}: {
  articleId: string;
  mode: "edit" | "new";
  /** Düzenlenen sürüm ya da yeni sürümün metninin alınacağı sürüm (fıkra kimlikleri buradan taşınır). */
  base?: ArticleVersionDetail;
  onSaved: (versionId: string) => void;
  onDeleted: () => void;
}) {
  const refresh = useLibraryRefresh();
  const editing = mode === "edit" && base;
  const [form, setForm] = useState({
    validFrom: editing ? (base.validFrom ?? "") : "",
    validTo: editing ? (base.validTo ?? "") : "",
    amendedBy: editing ? (base.amendedBy ?? "") : "",
    gazetteDate: editing ? (base.gazetteDate ?? "") : "",
    gazetteNumber: editing ? (base.gazetteNumber ?? "") : "",
    isRepealed: editing ? base.isRepealed : false,
    text: base ? paragraphsToText(base.paragraphs) : "",
  });
  const set = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) =>
    setForm((f) => ({ ...f, [key]: e.target.value }));

  const baseVersionId = base?.id ?? null;
  const debouncedText = useDebouncedValue(form.text, 400);
  const preview = useQuery({
    queryKey: ["article-text-preview", debouncedText, baseVersionId],
    queryFn: () => parse(debouncedText, baseVersionId),
    placeholderData: keepPreviousData,
  });

  const create = useCreateArticleVersion();
  const update = useUpdateArticleVersion();
  const remove = useDeleteArticleVersion();
  const error = create.error ?? update.error;
  const pending = create.isPending || update.isPending;

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    // Önizleme gecikmeli olduğundan kaydederken metin son hâliyle yeniden ayrıştırılır.
    const paragraphs = await parse(form.text, baseVersionId);
    const data = {
      validFrom: emptyToNull(form.validFrom),
      validTo: emptyToNull(form.validTo),
      amendedBy: emptyToNull(form.amendedBy),
      gazetteDate: emptyToNull(form.gazetteDate),
      gazetteNumber: emptyToNull(form.gazetteNumber),
      isRepealed: form.isRepealed,
      paragraphs,
    };
    const saved = editing
      ? await update.mutateAsync({ id: base.id, data })
      : await create.mutateAsync({ articleId, data });
    await refresh();
    toast.success(editing ? "Sürüm güncellendi." : "Sürüm kaydedildi.");
    if (saved.status === 200 || saved.status === 201) onSaved(saved.data.id);
  }

  const { textMessages, generalMessages } = splitMessages(error);

  return (
    <form onSubmit={(e) => submit(e).catch(() => {})} className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2">
        <Field
          label="Yürürlük başlangıcı"
          htmlFor="version-from"
          error={error}
          field="validFrom"
          hint={mode === "new" ? "Değişikliğin yürürlüğe girdiği gün." : undefined}
        >
          <Input id="version-from" type="date" value={form.validFrom} onChange={set("validFrom")} />
        </Field>
        <Field
          label="Yürürlük sonu"
          htmlFor="version-to"
          error={error}
          field="validTo"
          hint="Hâlen yürürlükteyse boş bırakın."
        >
          <Input id="version-to" type="date" value={form.validTo} onChange={set("validTo")} />
        </Field>
      </div>
      <Field
        label="Değiştiren düzenleme"
        htmlFor="version-amended"
        error={error}
        field="amendedBy"
        hint="Örn. 29/6/2005 tarihli 5377 s.K. m.12"
      >
        <Input id="version-amended" value={form.amendedBy} onChange={set("amendedBy")} />
      </Field>
      <div className="grid gap-4 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
        <Field label="RG tarihi" htmlFor="version-gazette-date">
          <Input id="version-gazette-date" type="date" value={form.gazetteDate} onChange={set("gazetteDate")} />
        </Field>
        <Field label="RG sayısı" htmlFor="version-gazette-number" error={error} field="gazetteNumber">
          <Input id="version-gazette-number" value={form.gazetteNumber} onChange={set("gazetteNumber")} />
        </Field>
        <div className="flex h-9 items-center gap-2">
          <Checkbox
            id="version-repealed"
            checked={form.isRepealed}
            onCheckedChange={(checked) => setForm((f) => ({ ...f, isRepealed: checked === true }))}
          />
          <Label htmlFor="version-repealed" className="font-normal">
            Mülga
          </Label>
        </div>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <div>
          <Label htmlFor="version-text" className="mb-1.5">
            Madde metni
          </Label>
          <Textarea
            id="version-text"
            value={form.text}
            onChange={set("text")}
            placeholder={textPlaceholder}
            className="min-h-72 font-reading text-base leading-relaxed"
            spellCheck
          />
          <p className="mt-1 text-xs text-muted-foreground">
            Her fıkra “(1)”, her bent “a)”, alt bent “1.” ile yeni satırda başlar. Bentlerden sonra gelen satır
            fıkrayı bağlayan metin sayılır. Baştaki “MADDE 81 –” yazılmasa da olur.
          </p>
          {textMessages.map((m) => (
            <p key={m} className="mt-1 text-xs text-destructive">
              {m}
            </p>
          ))}
        </div>
        <div>
          <p className="mb-1.5 text-sm font-medium">Önizleme</p>
          <div className="min-h-72 rounded-md border bg-background p-3">
            {preview.data && preview.data.length > 0 ? (
              <ParagraphList paragraphs={preview.data} />
            ) : (
              <p className="text-sm text-muted-foreground">Metin yazdıkça fıkra ve bentler burada görünür.</p>
            )}
          </div>
        </div>
      </div>

      <FormMessages messages={generalMessages} />
      <div className="flex justify-between gap-2">
        <Button type="submit" disabled={pending}>
          {editing ? "Sürümü kaydet" : "Yeni sürümü kaydet"}
        </Button>
        {editing && (
          <ConfirmDelete
            title="Bu sürüm silinsin mi?"
            description="Yürürlükteki sürüm silinirse ondan hemen önce biten sürüm yeniden yürürlükte sayılır."
            onConfirm={async () => {
              await remove.mutateAsync({ id: base.id });
              await refresh();
              toast.success("Sürüm silindi.");
              onDeleted();
            }}
          >
            <Button type="button" variant="ghost">
              <Trash2 /> Sürümü sil
            </Button>
          </ConfirmDelete>
        )}
      </div>
    </form>
  );
}
