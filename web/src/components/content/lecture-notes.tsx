"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { ArrowDown, ArrowUp, BookOpen, GraduationCap, Lock, Paperclip, Pencil, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { useReorderLectureCourses } from "@/api/generated/lecture-courses/lecture-courses";
import { getListLectureNotesQueryKey, useDeleteLectureNote } from "@/api/generated/lecture-notes/lecture-notes";
import type { LectureCourseSummary, LectureNoteScope, LectureNoteSummary } from "@/api/generated/model";
import { ConfirmDelete } from "@/components/panel/confirm-delete";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useHydrated } from "@/hooks/use-hydrated";
import { isAdmin, useCurrentUser } from "@/lib/auth";
import { formatDate } from "@/lib/dates";
import { matchesTr } from "@/lib/turkish";

/** "TCK m.81", "TCK", "Temel Ceza Kanunları" ya da "Genel". */
export function scopeLabel(scope: LectureNoteScope) {
  if (scope.article && scope.law) return `${scope.law.label} m.${scope.article.number}`;
  if (scope.law) return scope.law.label;
  return scope.group?.title ?? "Genel";
}

/** Yönetici mi (hidrasyondan sonra; sunucu çiziminde düğmeler görünmez). */
export function useIsAdmin() {
  const { data: user } = useCurrentUser();
  return useHydrated() && isAdmin(user);
}

/** Yönetici: yeni ders notu düğmesi (liste sayfasının başlığında). */
export function NewLectureNoteButton() {
  if (!useIsAdmin()) return null;
  return (
    <Button asChild>
      <Link href="/ders-notlari/yeni">
        <Plus /> Yeni ders notu
      </Link>
    </Button>
  );
}

/** Yönetici: dersi listede bir yukarı/aşağı taşır (bütün derslerin yeni sırası gönderilir). */
function CourseOrderButtons({ courses, index }: { courses: LectureCourseSummary[]; index: number }) {
  const router = useRouter();
  const reorder = useReorderLectureCourses();
  if (!useIsAdmin()) return null;
  async function shift(delta: -1 | 1) {
    const ids = courses.map((c) => c.id);
    [ids[index], ids[index + delta]] = [ids[index + delta], ids[index]];
    try {
      await reorder.mutateAsync({ data: { courseIds: ids } });
      router.refresh();
    } catch {
      toast.error("Sıra değiştirilemedi; sayfayı yenileyip yeniden deneyin.");
    }
  }
  const title = courses[index].title;
  return (
    <div className="flex flex-col justify-center gap-1 print:hidden">
      <Button variant="ghost" size="icon" disabled={index === 0 || reorder.isPending} onClick={() => shift(-1)} aria-label={`Yukarı taşı: ${title}`}>
        <ArrowUp />
      </Button>
      <Button
        variant="ghost"
        size="icon"
        disabled={index === courses.length - 1 || reorder.isPending}
        onClick={() => shift(1)}
        aria-label={`Aşağı taşı: ${title}`}
      >
        <ArrowDown />
      </Button>
    </div>
  );
}

/** Dersler sırasıyla: ad, açıklama, başlık sayısı. */
export function CourseList({ courses }: { courses: LectureCourseSummary[] }) {
  if (courses.length === 0) return <p className="mt-2 text-muted-foreground">Henüz ders eklenmedi.</p>;
  return (
    <ul className="mt-2 space-y-3">
      {courses.map((course, index) => (
        <li key={course.id} className="flex gap-2">
          <Link href={`/ders-notlari/${course.slug}`} className="block min-w-0 flex-1 rounded-lg border bg-card p-4 hover:border-primary/50">
            <span className="flex items-center gap-2 text-xl text-primary">
              <BookOpen className="size-5 shrink-0" aria-hidden />
              <span className="min-w-0">{course.title}</span>
              {course.visibility === "private" && (
                <Badge variant="outline" className="text-foreground">
                  <Lock /> Özel
                </Badge>
              )}
            </span>
            {course.description && <span className="reading mt-1 block text-[16px] text-muted-foreground">{course.description}</span>}
            <span className="mt-1 block text-sm text-muted-foreground">
              {course.nodeCount === 0 ? "Henüz başlık yok" : `${course.nodeCount} başlık`}
            </span>
          </Link>
          <CourseOrderButtons courses={courses} index={index} />
        </li>
      ))}
    </ul>
  );
}

type Sort = "recent" | "title";

/**
 * Ders notları listesi: metin araması (başlık ve özet, Türkçe karakter yazmadan da), kapsam süzgeci (genel, kanun
 * grubu, kanun) ve sıralama. `initialScope` adresten gelir: "grup:temel-ceza-kanunlari", "kanun:tck", "genel".
 */
export function LectureNoteList({ notes, initialScope = "" }: { notes: LectureNoteSummary[]; initialScope?: string }) {
  const [query, setQuery] = useState("");
  const [scope, setScope] = useState(initialScope);
  const [sort, setSort] = useState<Sort>("recent");

  const groups = useMemo(
    () => [...new Map(notes.flatMap((n) => (n.scope.group ? [[n.scope.group.slug, n.scope.group.title]] : []))).entries()],
    [notes],
  );
  const laws = useMemo(
    () => [...new Map(notes.flatMap((n) => (n.scope.law ? [[n.scope.law.slug, n.scope.law.label]] : []))).entries()],
    [notes],
  );
  const filtered = useMemo(() => {
    const [kind, slug] = scope.split(":");
    const result = notes.filter(
      (n) =>
        (scope === "" ||
          (kind === "genel" && !n.scope.group && !n.scope.law) ||
          (kind === "grup" && n.scope.group?.slug === slug) ||
          (kind === "kanun" && n.scope.law?.slug === slug)) &&
        matchesTr(`${n.title} ${n.excerpt} ${scopeLabel(n.scope)}`, query),
    );
    return sort === "title" ? [...result].sort((a, b) => a.title.localeCompare(b.title, "tr")) : result;
  }, [notes, scope, query, sort]);

  if (notes.length === 0) return <p className="mt-6 text-muted-foreground">Henüz ders notu girilmedi.</p>;

  return (
    <>
      <div className="mt-4 flex flex-wrap gap-2 rounded-lg border bg-card p-3">
        <Input
          type="search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Ders notlarında ara…"
          aria-label="Ders notlarında ara"
          className="min-w-0 flex-[1_1_16rem]"
        />
        <select
          value={scope}
          onChange={(e) => setScope(e.target.value)}
          aria-label="Kapsam"
          className="h-9 min-w-0 flex-[1_1_10rem] rounded-md border bg-card px-2 text-sm sm:flex-none"
        >
          <option value="">Bütün ders notları</option>
          <option value="genel">Genel</option>
          {groups.length > 0 && (
            <optgroup label="Kanun grubu">
              {groups.map(([slug, title]) => (
                <option key={slug} value={`grup:${slug}`}>
                  {title}
                </option>
              ))}
            </optgroup>
          )}
          {laws.length > 0 && (
            <optgroup label="Kanun">
              {laws.map(([slug, label]) => (
                <option key={slug} value={`kanun:${slug}`}>
                  {label}
                </option>
              ))}
            </optgroup>
          )}
        </select>
        <select
          value={sort}
          onChange={(e) => setSort(e.target.value as Sort)}
          aria-label="Sıralama"
          className="h-9 min-w-0 flex-[1_1_10rem] rounded-md border bg-card px-2 text-sm sm:flex-none"
        >
          <option value="recent">Son güncellenen önce</option>
          <option value="title">Başlığa göre</option>
        </select>
      </div>

      <p className="mt-3 text-sm text-muted-foreground" aria-live="polite">
        {filtered.length === 0 ? "Süzgece uyan ders notu yok." : `${filtered.length} ders notu`}
      </p>
      <ul className="mt-2 space-y-3">
        {filtered.map((note) => (
          <li key={note.id}>
            <Link href={`/ders-notlari/${note.slug}`} className="block rounded-lg border bg-card p-4 hover:border-primary/50">
              <span className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
                <Badge variant="secondary">{scopeLabel(note.scope)}</Badge>
                {note.visibility === "private" && (
                  <Badge variant="outline">
                    <Lock /> Üyelere
                  </Badge>
                )}
                {note.fileCount > 0 && (
                  <span className="inline-flex items-center gap-1">
                    <Paperclip className="size-3.5" aria-hidden /> {note.fileCount} belge
                  </span>
                )}
                <span className="ml-auto">{formatDate(note.updatedAt.slice(0, 10))}</span>
              </span>
              <span className="mt-1.5 flex items-center gap-2 text-xl text-primary">
                <GraduationCap className="size-5 shrink-0" aria-hidden />
                {note.title}
              </span>
              {note.excerpt && <span className="reading mt-1 block text-[16px] text-muted-foreground">{note.excerpt}</span>}
            </Link>
          </li>
        ))}
      </ul>
    </>
  );
}

/** Ders notu sayfasında yönetici düğmeleri: düzenle, sil. */
export function LectureNoteAdminActions({ id, slug }: { id: string; slug: string }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const remove = useDeleteLectureNote();
  if (!useIsAdmin()) return null;
  return (
    <div className="flex gap-2 print:hidden">
      <Button variant="outline" asChild>
        <Link href={`/ders-notlari/${slug}/duzenle`}>
          <Pencil /> Düzenle
        </Link>
      </Button>
      <ConfirmDelete
        title="Ders notu silinsin mi?"
        description="Ders notu okuyuculardan kaldırılır. Kayıt veritabanında saklanır."
        onConfirm={async () => {
          await remove.mutateAsync({ id });
          await queryClient.invalidateQueries({ queryKey: getListLectureNotesQueryKey() });
          toast.success("Ders notu silindi.");
          router.push("/ders-notlari");
          router.refresh();
        }}
      >
        <Button variant="ghost">
          <Trash2 /> Sil
        </Button>
      </ConfirmDelete>
    </div>
  );
}
