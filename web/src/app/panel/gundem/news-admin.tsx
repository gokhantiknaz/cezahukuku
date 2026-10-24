"use client";

import { useState, type FormEvent } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { ExternalLink, Lock, Pencil, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import {
  getListNewsQueryKey,
  useCreateNewsItem,
  useDeleteNewsItem,
  useListNews,
  useUpdateNewsItem,
} from "@/api/generated/news/news";
import type { NewsItemDto, Visibility } from "@/api/generated/model";
import { ConfirmDelete } from "@/components/panel/confirm-delete";
import { emptyToNull, Field, FormMessages, OptionSelect, otherMessages, visibilityLabels } from "@/components/panel/form";
import { FormDialog } from "@/components/panel/law-dialogs";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { DialogFooter } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { formatDate } from "@/lib/dates";

const today = () => new Date().toLocaleDateString("sv-SE"); // yyyy-mm-dd, yerel gün

/** Ceza Gündemi yönetimi: haberler alttaki şeritte ve /gundem sayfasında görünür. */
export function NewsAdmin() {
  const queryClient = useQueryClient();
  const { data } = useListNews({ limit: 100 });
  const remove = useDeleteNewsItem();
  const news = data?.status === 200 ? data.data : null;
  const refresh = () => queryClient.invalidateQueries({ queryKey: getListNewsQueryKey().slice(0, 1) });

  return (
    <div className="mt-4 space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-muted-foreground">
          Yayın tarihi gelmemiş haber yalnız burada görünür; o gün şeride düşer.
        </p>
        <NewsDialog
          onSaved={refresh}
          trigger={
            <Button>
              <Plus /> Haber ekle
            </Button>
          }
        />
      </div>
      {!news ? (
        <p className="text-muted-foreground">Yükleniyor…</p>
      ) : news.length === 0 ? (
        <p className="rounded-lg border bg-card p-4 text-muted-foreground">Henüz haber yok.</p>
      ) : (
        <ul className="divide-y rounded-lg border bg-card">
          {news.map((item) => (
            <li key={item.id} className="flex flex-wrap items-start gap-3 px-4 py-3">
              <div className="min-w-0 flex-1">
                <p className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
                  {formatDate(item.publishedOn)}
                  {item.publishedOn > today() && <Badge variant="secondary">Yayın bekliyor</Badge>}
                  {item.visibility === "private" && (
                    <Badge variant="outline">
                      <Lock /> Özel
                    </Badge>
                  )}
                </p>
                <p className="font-medium">{item.title}</p>
                {item.summary && <p className="line-clamp-2 text-sm text-muted-foreground">{item.summary}</p>}
                {item.url && (
                  <a href={item.url} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-sm text-primary hover:underline">
                    {item.url} <ExternalLink className="size-3" />
                  </a>
                )}
              </div>
              <div className="flex gap-1">
                <NewsDialog
                  item={item}
                  onSaved={refresh}
                  trigger={
                    <Button variant="ghost" size="icon-sm" aria-label="Haberi düzenle" title="Haberi düzenle">
                      <Pencil />
                    </Button>
                  }
                />
                <ConfirmDelete
                  title="Haber silinsin mi?"
                  description={item.title}
                  onConfirm={async () => {
                    await remove.mutateAsync({ id: item.id });
                    await refresh();
                    toast.success("Haber silindi.");
                  }}
                >
                  <Button variant="ghost" size="icon-sm" aria-label="Haberi sil" title="Haberi sil">
                    <Trash2 />
                  </Button>
                </ConfirmDelete>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function NewsDialog({ item, onSaved, trigger }: { item?: NewsItemDto; onSaved: () => Promise<unknown>; trigger: React.ReactNode }) {
  return (
    <FormDialog title={item ? "Haberi düzenle" : "Yeni haber"} trigger={trigger}>
      {(close) => <NewsForm item={item} onDone={async () => { await onSaved(); close(); }} />}
    </FormDialog>
  );
}

function NewsForm({ item, onDone }: { item?: NewsItemDto; onDone: () => Promise<void> }) {
  const [form, setForm] = useState({
    title: item?.title ?? "",
    summary: item?.summary ?? "",
    url: item?.url ?? "",
    publishedOn: item?.publishedOn ?? today(),
    visibility: (item?.visibility ?? "public") as Visibility,
  });
  const create = useCreateNewsItem();
  const update = useUpdateNewsItem();
  const error = create.error ?? update.error;
  const set = (changes: Partial<typeof form>) => setForm((f) => ({ ...f, ...changes }));

  async function submit(event: FormEvent) {
    event.preventDefault();
    const data = { ...form, summary: emptyToNull(form.summary), url: emptyToNull(form.url) };
    if (item) await update.mutateAsync({ id: item.id, data });
    else await create.mutateAsync({ data });
    toast.success(item ? "Haber güncellendi." : "Haber eklendi.");
    await onDone();
  }

  return (
    <form onSubmit={(e) => submit(e).catch(() => {})} className="space-y-4">
      <Field label="Başlık" htmlFor="news-title" error={error} field="title">
        <Input id="news-title" value={form.title} onChange={(e) => set({ title: e.target.value })} required autoFocus maxLength={300} />
      </Field>
      <Field label="Özet" htmlFor="news-summary" error={error} field="summary">
        <Textarea id="news-summary" value={form.summary} onChange={(e) => set({ summary: e.target.value })} rows={4} maxLength={2000} />
      </Field>
      <Field label="Kaynak bağlantısı" htmlFor="news-url" error={error} field="url" hint="Resmî Gazete, karar metni… (isteğe bağlı)">
        <Input id="news-url" type="url" value={form.url} onChange={(e) => set({ url: e.target.value })} placeholder="https://" />
      </Field>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Yayın tarihi" htmlFor="news-date" error={error} field="publishedOn">
          <Input id="news-date" type="date" value={form.publishedOn} onChange={(e) => set({ publishedOn: e.target.value })} required />
        </Field>
        <Field label="Görünürlük" htmlFor="news-visibility">
          <OptionSelect id="news-visibility" value={form.visibility} options={visibilityLabels} onChange={(visibility) => set({ visibility })} />
        </Field>
      </div>
      <FormMessages messages={otherMessages(error, ["title", "summary", "url", "publishedOn"])} />
      <DialogFooter>
        <Button type="submit" disabled={create.isPending || update.isPending}>
          Kaydet
        </Button>
      </DialogFooter>
    </form>
  );
}
