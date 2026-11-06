"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  getListLectureNotesQueryKey,
  useCreateLectureNote,
  useGetLectureNote,
  useUpdateLectureNote,
} from "@/api/generated/lecture-notes/lecture-notes";
import { useGetLaw, useGetLibrary } from "@/api/generated/library/library";
import type { LectureNoteDetail, Visibility } from "@/api/generated/model";
import { Field, fieldMessages, FormMessages, OptionSelect, otherMessages, visibilityLabels } from "@/components/panel/form";
import { allArticles } from "@/components/panel/tree-utils";
import { RichTextEditor } from "@/components/rich-text/rich-text-editor";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useHydrated } from "@/hooks/use-hydrated";
import { isAdmin, useCurrentUser } from "@/lib/auth";

type ScopeKind = "genel" | "grup" | "kanun" | "madde";

const scopeKinds: Record<ScopeKind, string> = {
  genel: "Genel (bağlantısız)",
  grup: "Kanun grubuna",
  kanun: "Kanuna",
  madde: "Maddeye",
};

const selectClass = "h-9 w-full rounded-md border bg-card px-2 text-sm";

/** Yönetici değilse form yerine açıklama. */
function AdminOnly({ children }: { children: React.ReactNode }) {
  const hydrated = useHydrated();
  const { data: user, isPending } = useCurrentUser();
  if (!hydrated || isPending) return null;
  if (!isAdmin(user)) return <p className="mt-6 text-muted-foreground">Ders notlarını yalnız yönetici girebilir.</p>;
  return <>{children}</>;
}

export function NewLectureNote() {
  return (
    <AdminOnly>
      <LectureNoteForm />
    </AdminOnly>
  );
}

export function EditLectureNote({ slug }: { slug: string }) {
  const { data, isPending } = useGetLectureNote(slug);
  return (
    <AdminOnly>
      {isPending ? (
        <p className="mt-6 text-muted-foreground">Yükleniyor…</p>
      ) : data?.status === 200 ? (
        <LectureNoteForm note={data.data} />
      ) : (
        <p className="mt-6 text-muted-foreground">Ders notu bulunamadı.</p>
      )}
    </AdminOnly>
  );
}

/**
 * Ders notu formu: başlık, görünürlük, isteğe bağlı bağlantı (genel / kanun grubu / kanun / madde) ve metin.
 * Maddeye bağlanan not o maddenin kanununa ve grubuna da bağlı sayılır (sunucu tamamlar).
 */
function LectureNoteForm({ note }: { note?: LectureNoteDetail }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [form, setForm] = useState({
    title: note?.title ?? "",
    visibility: note?.visibility ?? ("public" as Visibility),
    scope: (note?.articleId ? "madde" : note?.lawId ? "kanun" : note?.lawGroupId ? "grup" : "genel") as ScopeKind,
    groupId: note?.lawGroupId ?? "",
    lawId: note?.lawId ?? "",
    articleId: note?.articleId ?? "",
    html: note?.html ?? "",
  });
  const set = <K extends keyof typeof form>(key: K, value: (typeof form)[K]) => setForm((f) => ({ ...f, [key]: value }));

  const library = useGetLibrary();
  const groups = library.data?.status === 200 ? library.data.data.groups : [];
  const laws = groups.flatMap((g) => g.laws);
  const lawSlug = laws.find((l) => l.id === form.lawId)?.slug ?? "";
  const tree = useGetLaw(lawSlug, { query: { enabled: form.scope === "madde" && lawSlug !== "" } });
  const articles = tree.data?.status === 200 ? allArticles(tree.data.data) : [];

  const create = useCreateLectureNote();
  const update = useUpdateLectureNote();
  const error = create.error ?? update.error;
  const pending = create.isPending || update.isPending;

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const data = {
      title: form.title,
      html: form.html,
      visibility: form.visibility,
      lawGroupId: form.scope === "grup" && form.groupId ? form.groupId : null,
      lawId: (form.scope === "kanun" || form.scope === "madde") && form.lawId ? form.lawId : null,
      articleId: form.scope === "madde" && form.articleId ? form.articleId : null,
    };
    const saved = note ? await update.mutateAsync({ id: note.id, data }) : await create.mutateAsync({ data });
    await queryClient.invalidateQueries({ queryKey: getListLectureNotesQueryKey() });
    toast.success(note ? "Ders notu kaydedildi." : "Ders notu eklendi.");
    if (saved.status === 200 || saved.status === 201) {
      router.push(`/ders-notlari/${saved.data.slug}`);
      router.refresh();
    }
  }

  return (
    <form onSubmit={(e) => submit(e).catch(() => {})} className="mt-5 space-y-4">
      <div className="grid gap-4 sm:grid-cols-[1fr_14rem]">
        <Field label="Başlık" htmlFor="lecture-title" error={error} field="title">
          <Input
            id="lecture-title"
            value={form.title}
            onChange={(e) => set("title", e.target.value)}
            placeholder="Kast ve taksir ayrımı"
            required
          />
        </Field>
        <Field label="Görünürlük" htmlFor="lecture-visibility">
          <OptionSelect
            id="lecture-visibility"
            value={form.visibility}
            options={visibilityLabels}
            onChange={(visibility) => set("visibility", visibility)}
          />
        </Field>
      </div>

      <fieldset className="space-y-3 rounded-md border p-3">
        <legend className="px-1 text-sm font-medium">Bağlantı (isteğe bağlı)</legend>
        <div className="flex flex-wrap gap-x-4 gap-y-2">
          {(Object.keys(scopeKinds) as ScopeKind[]).map((kind) => (
            <label key={kind} className="flex items-center gap-2 text-sm">
              <input
                type="radio"
                name="lecture-scope"
                value={kind}
                checked={form.scope === kind}
                onChange={() => set("scope", kind)}
                className="accent-primary"
              />
              {scopeKinds[kind]}
            </label>
          ))}
        </div>
        {form.scope === "grup" && (
          <Field label="Kanun grubu" htmlFor="lecture-group" error={error} field="lawGroupId">
            <select id="lecture-group" value={form.groupId} onChange={(e) => set("groupId", e.target.value)} className={selectClass} required>
              <option value="">Seçin…</option>
              {groups.map((g) => (
                <option key={g.id} value={g.id}>
                  {g.title}
                </option>
              ))}
            </select>
          </Field>
        )}
        {(form.scope === "kanun" || form.scope === "madde") && (
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Kanun" htmlFor="lecture-law" error={error} field="lawId">
              <select
                id="lecture-law"
                value={form.lawId}
                onChange={(e) => setForm((f) => ({ ...f, lawId: e.target.value, articleId: "" }))}
                className={selectClass}
                required
              >
                <option value="">Seçin…</option>
                {groups.map((g) => (
                  <optgroup key={g.id} label={g.title}>
                    {g.laws.map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.shortName || l.number} — {l.name}
                      </option>
                    ))}
                  </optgroup>
                ))}
              </select>
            </Field>
            {form.scope === "madde" && (
              <Field label="Madde" htmlFor="lecture-article" error={error} field="articleId">
                <select
                  id="lecture-article"
                  value={form.articleId}
                  onChange={(e) => set("articleId", e.target.value)}
                  className={selectClass}
                  disabled={!lawSlug}
                  required
                >
                  <option value="">{lawSlug ? "Seçin…" : "Önce kanunu seçin"}</option>
                  {articles.map((a) => (
                    <option key={a.id} value={a.id}>
                      Madde {a.number}
                      {a.title ? ` — ${a.title}` : ""}
                    </option>
                  ))}
                </select>
              </Field>
            )}
          </div>
        )}
        <p className="text-xs text-muted-foreground">
          Bağlantı yalnız düzen içindir: ders notu kendi sayfasında okunur; maddeye bağlanırsa maddenin Açıklama
          sekmesinde de bağlantısı görünür.
        </p>
      </fieldset>

      <div>
        <RichTextEditor value={form.html} onChange={(html) => set("html", html)} label="Ders notu metni" />
        {fieldMessages(error, "html").map((m) => (
          <p key={m} className="mt-1 text-xs text-destructive">
            {m}
          </p>
        ))}
      </div>
      <FormMessages messages={otherMessages(error, ["title", "html", "lawGroupId", "lawId", "articleId"])} />
      <div className="flex gap-2">
        <Button type="submit" disabled={pending}>
          {note ? "Kaydet" : "Ders notunu ekle"}
        </Button>
        <Button type="button" variant="ghost" asChild>
          <Link href={note ? `/ders-notlari/${note.slug}` : "/ders-notlari"}>Vazgeç</Link>
        </Button>
      </div>
    </form>
  );
}
