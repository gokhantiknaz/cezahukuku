"use client";

import { useMemo, useState, useTransition, type ReactNode } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowDown, ArrowUp, FileInput, FolderInput, Pencil, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import {
  useAddLectureNode,
  useCreateLectureCourse,
  useDeleteLectureCourse,
  useMoveLectureNode,
  useUpdateLectureCourse,
} from "@/api/generated/lecture-courses/lecture-courses";
import { useDeleteLectureNote } from "@/api/generated/lecture-notes/lecture-notes";
import type {
  LectureCourseDetail,
  LectureNodeContext,
  LectureNoteDetail,
  LectureTocEntry,
  Visibility,
} from "@/api/generated/model";
import { useIsAdmin } from "@/components/content/lecture-notes";
import { ConfirmDelete } from "@/components/panel/confirm-delete";
import { Field, FormMessages, OptionSelect, otherMessages, visibilityLabels } from "@/components/panel/form";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { Dialog, DialogClose, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { type MoveTarget, subtreeIds, visibilityChange } from "@/lib/lecture-moves";

const selectClass = "h-9 w-full rounded-md border bg-card px-2 text-sm";

/** Kapatılınca içeriği sıfırlanan pencere (form durumu içerideki bileşende tutulur). */
function FormDialog({
  title,
  description,
  trigger,
  children,
}: {
  title: string;
  description?: string;
  trigger: ReactNode;
  children: (close: () => void) => ReactNode;
}) {
  const [open, setOpen] = useState(false);
  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {description && <DialogDescription>{description}</DialogDescription>}
        </DialogHeader>
        {children(() => setOpen(false))}
      </DialogContent>
    </Dialog>
  );
}

function FormButtons({ pending, label }: { pending: boolean; label: string }) {
  return (
    <div className="flex justify-end gap-2">
      <DialogClose asChild>
        <Button type="button" variant="ghost">
          Vazgeç
        </Button>
      </DialogClose>
      <Button type="submit" disabled={pending}>
        {pending ? "Kaydediliyor…" : label}
      </Button>
    </div>
  );
}

// ——— Ders ———

function CourseForm({ course, onDone }: { course?: LectureCourseDetail; onDone: () => void }) {
  const router = useRouter();
  const create = useCreateLectureCourse();
  const update = useUpdateLectureCourse();
  const [form, setForm] = useState({
    title: course?.title ?? "",
    description: course?.description ?? "",
    visibility: course?.visibility ?? ("public" as Visibility),
    chapterNumber: course?.chapterNumber?.toString() ?? "",
  });
  const error = create.error ?? update.error;
  const pending = create.isPending || update.isPending;

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const data = {
      title: form.title,
      description: form.description || null,
      visibility: form.visibility,
      chapterNumber: form.chapterNumber.trim() ? Number(form.chapterNumber) : null,
    };
    if (course) {
      await update.mutateAsync({ id: course.id, data });
      toast.success("Ders kaydedildi.");
      onDone();
      router.refresh();
    } else {
      const saved = await create.mutateAsync({ data });
      if (saved.status !== 201) return;
      toast.success("Ders eklendi. Şimdi başlıklarını ekleyebilirsiniz.");
      onDone();
      router.push(`/ders-notlari/${saved.data.slug}`);
      router.refresh();
    }
  }

  return (
    <form onSubmit={(e) => submit(e).catch(() => {})} className="space-y-4">
      <Field label="Dersin adı" htmlFor="course-title" error={error} field="title">
        <Input
          id="course-title"
          value={form.title}
          onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
          placeholder="Ceza Hukuku Genel Hükümler"
          required
          autoFocus
        />
      </Field>
      <Field label="Kısa açıklama (isteğe bağlı)" htmlFor="course-description" error={error} field="description">
        <Textarea
          id="course-description"
          value={form.description}
          onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
          rows={3}
        />
      </Field>
      <Field
        label="Bölüm numarası (isteğe bağlı)"
        htmlFor="course-chapter"
        error={error}
        field="chapterNumber"
        hint={'Ders kitabın bir bölümüyse (ör. 2), başlıklar "2.1", "2.1.1" diye numaralanır; boşsa "1. Bölüm", "1.1".'}
      >
        <Input
          id="course-chapter"
          type="number"
          inputMode="numeric"
          min={1}
          max={99}
          step={1}
          className="w-28"
          value={form.chapterNumber}
          onChange={(e) => setForm((f) => ({ ...f, chapterNumber: e.target.value }))}
        />
      </Field>
      <Field
        label="Görünürlük"
        htmlFor="course-visibility"
        hint="Özel dersin bütün başlıkları yalnız giriş yapanlara görünür."
      >
        <OptionSelect
          id="course-visibility"
          value={form.visibility}
          options={visibilityLabels}
          onChange={(visibility) => setForm((f) => ({ ...f, visibility }))}
        />
      </Field>
      <FormMessages messages={otherMessages(error, ["title", "description", "chapterNumber"])} />
      <FormButtons pending={pending} label={course ? "Kaydet" : "Dersi ekle"} />
    </form>
  );
}

/** Dersler listesinde: yeni ders ve başlıkları içe aktarma (yeni ders kurar). */
export function NewCourseButton() {
  if (!useIsAdmin()) return null;
  return (
    <div className="flex flex-wrap gap-2">
      <Button variant="outline" asChild>
        <Link href="/ders-notlari/ice-aktar">
          <FileInput /> Başlıkları içe aktar
        </Link>
      </Button>
      <NewCourseDialog />
    </div>
  );
}

function NewCourseDialog() {
  return (
    <FormDialog
      title="Yeni ders"
      description="Adres dersin adından üretilir ve sonradan değişmez."
      trigger={
        <Button>
          <Plus /> Yeni ders
        </Button>
      }
    >
      {(close) => <CourseForm onDone={close} />}
    </FormDialog>
  );
}

/** Ders sayfasında: başlık ekle, düzenle, sil. */
export function CourseAdminActions({ course }: { course: LectureCourseDetail }) {
  const router = useRouter();
  const remove = useDeleteLectureCourse();
  if (!useIsAdmin()) return null;
  return (
    <div className="flex flex-wrap gap-2 print:hidden">
      <AddNodeButton courseId={course.id} />
      <FormDialog
        title="Dersi düzenle"
        trigger={
          <Button variant="outline">
            <Pencil /> Düzenle
          </Button>
        }
      >
        {(close) => <CourseForm course={course} onDone={close} />}
      </FormDialog>
      <ConfirmDelete
        title="Ders silinsin mi?"
        description="Ders okuyuculardan kaldırılır, kaydı saklanır. Başlığı olan ders silinemez; önce başlıklarını silin."
        onConfirm={async () => {
          await remove.mutateAsync({ id: course.id });
          toast.success("Ders silindi.");
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

// ——— Başlık ———

type ParentRef = { id: string; label: string; title: string };

function AddNodeForm({ courseId, parent, onDone }: { courseId: string; parent?: ParentRef; onDone: () => void }) {
  const router = useRouter();
  const add = useAddLectureNode();
  const [title, setTitle] = useState("");
  const [visibility, setVisibility] = useState<Visibility>("public");

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const saved = await add.mutateAsync({ id: courseId, data: { title, parentId: parent?.id ?? null, visibility } });
    if (saved.status !== 201) return;
    toast.success(`Başlık eklendi: ${saved.data.node?.label ?? ""} ${saved.data.title}`);
    onDone();
    router.refresh();
  }

  return (
    <form onSubmit={(e) => submit(e).catch(() => {})} className="space-y-4">
      <Field label="Başlık" htmlFor="node-title" error={add.error} field="title" hint="Numara ağaçtaki yerinden kendiliğinden verilir.">
        <Input id="node-title" value={title} onChange={(e) => setTitle(e.target.value)} required autoFocus />
      </Field>
      <Field label="Görünürlük" htmlFor="node-visibility" hint="Üst başlık ya da ders özelse bu başlık da özel sayılır.">
        <OptionSelect id="node-visibility" value={visibility} options={visibilityLabels} onChange={setVisibility} />
      </Field>
      <FormMessages messages={otherMessages(add.error, ["title"])} />
      <FormButtons pending={add.isPending} label="Başlığı ekle" />
    </form>
  );
}

/** Birinci düzeye (`parent` yoksa) ya da bir başlığın altına, kardeşlerinin sonuna başlık ekler. */
export function AddNodeButton({ courseId, parent, variant = "outline" }: { courseId: string; parent?: ParentRef; variant?: "outline" | "ghost" }) {
  if (!useIsAdmin()) return null;
  const label = parent ? "Alt başlık ekle" : "Başlık ekle";
  return (
    <FormDialog
      title={label}
      description={parent ? `${parent.label} ${parent.title} başlığının altına, sona eklenir.` : "Birinci düzeye (bölüm), sona eklenir."}
      trigger={
        <Button variant={variant} size={variant === "ghost" ? "sm" : "default"}>
          <Plus /> {label}
        </Button>
      }
    >
      {(close) => <AddNodeForm courseId={courseId} parent={parent} onDone={close} />}
    </FormDialog>
  );
}

const ROOT = "";

/** `onError` verilirse hata bildirim yerine ona iletilir (form hatayı alanında gösterir). */
type MoveRequest = { noteId: string; target: MoveTarget; onError?: (error: unknown) => void };
type PendingConfirm = { request: MoveRequest; change: { to: Visibility; affected: number }; resolve: (moved: boolean) => void };

/**
 * Başlık taşıma ("Taşı" penceresi, yukarı/aşağı ve içindekilerde sürükle-bırak aynısını kullanır): kayıt, yeni numarayla
 * bildirim, hata mesajı ve içindekilerin tazelenmesi. Taşıma etkin görünürlüğü değiştirecekse önce onay sorulur
 * (spec 0003 R-7); bu yalnız uyarıdır, görünürlük kararı sunucudadır. `confirmDialog` çağıranın ağacına konur.
 *
 * `pending`, onay beklenirken, kayıt sürerken ve kayıttan sonra içindekiler sunucudan tazelenene kadar true'dur:
 * bir sonraki taşıma (bırakma yeri, görünürlük onayı, yukarı/aşağı sırası) hiçbir zaman eski ağaçla hesaplanmaz.
 * `movingId` bu süre boyunca taşınan başlıktır. Aynı ekrandaki bütün taşıma yolları tek bir örneği paylaşmalıdır
 * (başlık sayfasında yukarı/aşağı ve "Taşı" penceresi): biri beklerken öteki eski ağaçla çalışmasın.
 */
export function useMoveNode(toc: LectureTocEntry[], courseVisibility: Visibility) {
  const router = useRouter();
  const move = useMoveLectureNode();
  const [pendingConfirm, setPendingConfirm] = useState<PendingConfirm | null>(null);
  const [movingId, setMovingId] = useState<string | null>(null);
  const [refreshing, startRefresh] = useTransition();

  async function save(request: MoveRequest) {
    try {
      const saved = await move.mutateAsync({ id: request.noteId, data: request.target });
      if (saved.status === 200) toast.success(`Başlık taşındı; yeni numarası ${saved.data.node?.label ?? ""}.`);
      // Tazeleme bir geçiş (transition) içinde: `refreshing` yeni içindekiler gelene kadar true kalır; movingId de
      // aynı geçişte, yani yeni ağaçla birlikte temizlenir.
      startRefresh(() => {
        setMovingId(null);
        router.refresh();
      });
      return true;
    } catch (error) {
      if (request.onError) request.onError(error);
      else toast.error(otherMessages(error, []).join(" ") || "Başlık taşınamadı.");
      setMovingId(null);
      return false;
    }
  }

  /** Taşır; görünürlük değişecekse önce onay ister. Taşındıysa true, vazgeçildi ya da hata olduysa false. */
  function request(request: MoveRequest): Promise<boolean> {
    setMovingId(request.noteId);
    const change = visibilityChange(toc, courseVisibility, request.noteId, request.target.parentId);
    if (!change) return save(request);
    return new Promise((resolve) => setPendingConfirm({ request, change, resolve }));
  }

  async function confirm() {
    if (!pendingConfirm) return;
    const { request: confirmed, resolve } = pendingConfirm;
    setPendingConfirm(null);
    resolve(await save(confirmed));
  }

  const others = pendingConfirm ? pendingConfirm.change.affected - 1 : 0;
  const subject = `Bu başlık${others > 0 ? ` ve ${others} alt başlığı` : ""}`;
  const confirmDialog = (
    <AlertDialog
      open={pendingConfirm !== null}
      onOpenChange={(open) => {
        if (!open && pendingConfirm) {
          pendingConfirm.resolve(false);
          setPendingConfirm(null);
          setMovingId(null);
        }
      }}
    >
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Görünürlük değişecek</AlertDialogTitle>
          <AlertDialogDescription>
            {pendingConfirm?.change.to === "private"
              ? `${subject} giriş yapmamış okuyuculardan kalkacak; yalnız giriş yapanlar görecek.`
              : `${subject} herkese açılacak; giriş yapmamış okuyucular da görecek.`}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Vazgeç</AlertDialogCancel>
          {/* Varsayılan kapanma engellenir: kayıt başlarken pencere kendisi kapanır (Vazgeç sayılmasın). */}
          <AlertDialogAction
            onClick={(event) => {
              event.preventDefault();
              void confirm();
            }}
          >
            Taşı
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );

  const pending = movingId !== null || move.isPending || refreshing || pendingConfirm !== null;
  return { request, pending, movingId, confirmDialog };
}

function MoveForm({
  note,
  context,
  course,
  move,
  onDone,
}: {
  note: LectureNoteDetail;
  context: LectureNodeContext;
  course: LectureCourseDetail;
  /** Başlık sayfasının ortak taşıma örneği (onay penceresini de o koyar). */
  move: ReturnType<typeof useMoveNode>;
  onDone: () => void;
}) {
  const toc = course.toc;
  // Form hatayı alanında gösterir (bildirimle değil).
  const [error, setError] = useState<unknown>(null);
  const blocked = useMemo(() => subtreeIds(toc, note.id), [toc, note.id]);
  const [parentId, setParentId] = useState(context.parentId ?? ROOT);
  // Hedef kardeşler (başlığın kendisi hariç), içindekiler sırasıyla.
  const siblings = toc.filter((e) => (e.parentId ?? ROOT) === parentId && e.id !== note.id);
  const [position, setPosition] = useState(String(siblings.length));
  const index = Math.min(Number(position), siblings.length);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    if (await move.request({ noteId: note.id, target: { parentId: parentId || null, index }, onError: setError })) onDone();
  }

  return (
    <form onSubmit={(e) => submit(e).catch(() => {})} className="space-y-4">
      <Field label="Yeni üst başlık" htmlFor="move-parent" error={error} field="parentId">
        <select
          id="move-parent"
          value={parentId}
          onChange={(e) => {
            setParentId(e.target.value);
            setPosition(String(toc.filter((t) => (t.parentId ?? ROOT) === e.target.value && t.id !== note.id).length));
          }}
          className={selectClass}
        >
          <option value={ROOT}>Birinci düzey (bölüm)</option>
          {toc
            .filter((e) => !blocked.has(e.id))
            .map((e) => (
              <option key={e.id} value={e.id}>
                {" ".repeat((e.depth - 1) * 3)}
                {e.label} {e.title}
              </option>
            ))}
        </select>
      </Field>
      <Field label="Yeri" htmlFor="move-position" error={error} field="index">
        <select id="move-position" value={position} onChange={(e) => setPosition(e.target.value)} className={selectClass}>
          <option value="0">En başa</option>
          {siblings.map((s, i) => (
            <option key={s.id} value={String(i + 1)}>
              {s.label} {s.title} başlığından sonra
            </option>
          ))}
        </select>
      </Field>
      <p className="text-xs text-muted-foreground">
        Başlık alt başlıklarıyla birlikte taşınır; numaraları yeniden hesaplanır, adresleri değişmez.
      </p>
      <FormMessages messages={otherMessages(error, ["parentId", "index"])} />
      <FormButtons pending={move.pending} label="Taşı" />
    </form>
  );
}

/** Başlık sayfasında: alt başlık ekle, düzenle, aynı düzeyde yukarı/aşağı, taşı, sil. */
export function NodeAdminActions({
  note,
  context,
  course,
}: {
  note: LectureNoteDetail;
  context: LectureNodeContext;
  course: LectureCourseDetail;
}) {
  const router = useRouter();
  const toc = course.toc;
  const move = useMoveNode(toc, course.visibility);
  const remove = useDeleteLectureNote();
  const isAdmin = useIsAdmin();
  if (!isAdmin) return null;

  const name = `${context.label} ${note.title}`;
  const siblings = toc.filter((e) => (e.parentId ?? null) === (context.parentId ?? null));
  const position = siblings.findIndex((e) => e.id === note.id);

  // Aynı üst başlık içinde görünürlük değişmez. Onay penceresi bir kez, burada konur ("Taşı" penceresi de bu
  // örneği kullanır): pencere kapandıktan sonra tazeleme bitene kadar yukarı/aşağı da kapalı kalır.
  const shift = (delta: -1 | 1) =>
    move.request({ noteId: note.id, target: { parentId: context.parentId ?? null, index: position + delta } });

  return (
    <div className="flex flex-wrap gap-2 print:hidden">
      <AddNodeButton courseId={context.course.id} parent={{ id: note.id, label: context.label, title: note.title }} />
      <Button variant="outline" asChild>
        <Link href={`/ders-notlari/${context.course.slug}/${note.slug}/duzenle`}>
          <Pencil /> Düzenle
        </Link>
      </Button>
      <Button
        variant="ghost"
        size="icon"
        disabled={position <= 0 || move.pending}
        onClick={() => shift(-1)}
        aria-label={`Yukarı taşı: ${name}`}
        title="Aynı düzeyde yukarı"
      >
        <ArrowUp />
      </Button>
      <Button
        variant="ghost"
        size="icon"
        disabled={position < 0 || position >= siblings.length - 1 || move.pending}
        onClick={() => shift(1)}
        aria-label={`Aşağı taşı: ${name}`}
        title="Aynı düzeyde aşağı"
      >
        <ArrowDown />
      </Button>
      <FormDialog
        title={`Taşı: ${name}`}
        trigger={
          <Button variant="ghost" aria-label={`Başka yere taşı: ${name}`}>
            <FolderInput /> Taşı
          </Button>
        }
      >
        {(close) => <MoveForm note={note} context={context} course={course} move={move} onDone={close} />}
      </FormDialog>
      {move.confirmDialog}
      <ConfirmDelete
        title="Başlık silinsin mi?"
        description="Başlık belgeleriyle birlikte okuyuculardan kaldırılır, kaydı saklanır. Alt başlığı olan başlık silinemez; önce alt başlıklarını taşıyın ya da silin."
        onConfirm={async () => {
          await remove.mutateAsync({ id: note.id });
          toast.success("Başlık silindi.");
          const parent = context.path.at(-1);
          router.push(parent ? `/ders-notlari/${context.course.slug}/${parent.slug}` : `/ders-notlari/${context.course.slug}`);
          router.refresh();
        }}
      >
        <Button variant="ghost" aria-label={`Sil: ${name}`}>
          <Trash2 /> Sil
        </Button>
      </ConfirmDelete>
    </div>
  );
}
