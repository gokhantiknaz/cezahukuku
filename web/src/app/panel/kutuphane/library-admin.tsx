"use client";

import Link from "next/link";
import { FolderPlus, ListTree, Lock, Pencil, Plus, RefreshCw, Search, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { ApiError } from "@/api/fetcher";
import { useDeleteLaw, useDeleteLawGroup, useRebuildCitations } from "@/api/generated/admin-laws/admin-laws";
import { useGetLibrary } from "@/api/generated/library/library";
import { useRebuildSearchIndex } from "@/api/generated/search/search";
import type { LawGroupWithLaws } from "@/api/generated/model";
import { ConfirmDelete } from "@/components/panel/confirm-delete";
import { LawDialog, LawGroupDialog } from "@/components/panel/law-dialogs";
import { useLibraryRefresh } from "@/components/panel/use-library-refresh";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

export function LibraryAdmin() {
  const { data, error } = useGetLibrary();

  if (error) {
    return <p className="mt-6 text-destructive">{error instanceof ApiError ? error.message : "Yüklenemedi."}</p>;
  }
  if (!data) return <p className="mt-6 text-muted-foreground">Yükleniyor…</p>;

  const groups = data.data.groups;
  return (
    <div className="mt-4 space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-muted-foreground">
          Kanunları gruplar hâlinde ekleyin; maddeleri “Yapı ve maddeler” ekranından girin.
        </p>
        <div className="flex gap-2">
          <RebuildCitationsButton />
          <RebuildSearchButton />
          <LawGroupDialog
            trigger={
              <Button variant="outline">
                <FolderPlus /> Grup ekle
              </Button>
            }
          />
          {groups.length > 0 && (
            <LawDialog
              groups={groups}
              trigger={
                <Button>
                  <Plus /> Kanun ekle
                </Button>
              }
            />
          )}
        </div>
      </div>

      {groups.length === 0 && (
        <p className="rounded-lg border bg-card p-4 text-muted-foreground">
          Henüz grup yok. Önce “Temel Ceza Kanunları” gibi bir grup ekleyin.
        </p>
      )}
      {groups.map((group) => (
        <GroupCard key={group.id} group={group} groups={groups} />
      ))}
    </div>
  );
}

/**
 * Madde metinlerindeki atıflar sürüm kaydedilirken çıkarılır; atıf kuralları güncellendiğinde eski kayıtları yeniden
 * taramak için.
 */
function RebuildCitationsButton() {
  const refresh = useLibraryRefresh();
  const rebuild = useRebuildCitations();
  return (
    <Button
      variant="ghost"
      disabled={rebuild.isPending}
      title="Bütün madde metinlerindeki atıfları yeniden tarar"
      onClick={async () => {
        try {
          const result = await rebuild.mutateAsync();
          await refresh();
          toast.success(`Atıflar yeniden tarandı${result.status === 200 ? `: ${result.data} atıf` : ""}.`);
        } catch {
          toast.error("Atıflar taranamadı.");
        }
      }}
    >
      <RefreshCw className={rebuild.isPending ? "animate-spin" : undefined} /> Atıfları tara
    </Button>
  );
}

/** Arama indeksi her kayıtta kendiliğinden güncellenir; indeks kuralları değişince baştan kurmak için. */
function RebuildSearchButton() {
  const rebuild = useRebuildSearchIndex();
  return (
    <Button
      variant="ghost"
      disabled={rebuild.isPending}
      title="Arama indeksini bütün içerikten yeniden kurar"
      onClick={async () => {
        try {
          const result = await rebuild.mutateAsync();
          toast.success(`Arama indeksi yenilendi${result.status === 200 ? `: ${result.data} kayıt` : ""}.`);
        } catch {
          toast.error("Arama indeksi yenilenemedi.");
        }
      }}
    >
      <Search className={rebuild.isPending ? "animate-pulse" : undefined} /> Arama indeksini yenile
    </Button>
  );
}

function GroupCard({ group, groups }: { group: LawGroupWithLaws; groups: LawGroupWithLaws[] }) {
  const refresh = useLibraryRefresh();
  const deleteGroup = useDeleteLawGroup();
  const deleteLaw = useDeleteLaw();

  return (
    <section className="rounded-lg border bg-card">
      <header className="flex flex-wrap items-center justify-between gap-2 border-b bg-accent px-4 py-2">
        <h2 className="text-lg text-accent-foreground">{group.title}</h2>
        <div className="flex gap-1">
          <LawDialog
            groups={groups}
            defaultGroupId={group.id}
            trigger={
              <Button variant="ghost" size="sm">
                <Plus /> Kanun
              </Button>
            }
          />
          <LawGroupDialog
            group={group}
            trigger={
              <Button variant="ghost" size="icon-sm" aria-label="Grubu düzenle" title="Grubu düzenle">
                <Pencil />
              </Button>
            }
          />
          <ConfirmDelete
            title={`“${group.title}” silinsin mi?`}
            description="Yalnız içinde kanun olmayan grup silinebilir."
            onConfirm={async () => {
              await deleteGroup.mutateAsync({ id: group.id });
              await refresh();
              toast.success("Grup silindi.");
            }}
          >
            <Button variant="ghost" size="icon-sm" aria-label="Grubu sil" title="Grubu sil">
              <Trash2 />
            </Button>
          </ConfirmDelete>
        </div>
      </header>

      {group.laws.length === 0 ? (
        <p className="px-4 py-3 text-sm text-muted-foreground">Bu grupta kanun yok.</p>
      ) : (
        <ul className="divide-y">
          {group.laws.map((law) => (
            <li key={law.id} className="flex flex-wrap items-center gap-x-3 gap-y-2 px-4 py-2.5">
              <div className="min-w-0 flex-1">
                <p className="flex items-center gap-2">
                  <span className="font-medium">{law.shortName || law.number}</span>
                  <span className="truncate">{law.name}</span>
                  {law.visibility === "private" && (
                    <Badge variant="outline">
                      <Lock /> Özel
                    </Badge>
                  )}
                </p>
                <p className="text-sm text-muted-foreground">
                  {law.number} sayılı · /{law.slug}
                </p>
              </div>
              <div className="flex gap-1">
                <Button variant="outline" size="sm" asChild>
                  <Link href={`/panel/kutuphane/${law.slug}`}>
                    <ListTree /> Yapı ve maddeler
                  </Link>
                </Button>
                <LawDialog
                  law={law}
                  groups={groups}
                  trigger={
                    <Button variant="ghost" size="icon-sm" aria-label="Kanunu düzenle" title="Kanunu düzenle">
                      <Pencil />
                    </Button>
                  }
                />
                <ConfirmDelete
                  title={`${law.shortName || law.name} silinsin mi?`}
                  description="Kanun, ağacı ve maddeleriyle birlikte okuyuculardan kaldırılır. Kayıtlar veritabanında saklanır."
                  onConfirm={async () => {
                    await deleteLaw.mutateAsync({ id: law.id });
                    await refresh();
                    toast.success("Kanun silindi.");
                  }}
                >
                  <Button variant="ghost" size="icon-sm" aria-label="Kanunu sil" title="Kanunu sil">
                    <Trash2 />
                  </Button>
                </ConfirmDelete>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
