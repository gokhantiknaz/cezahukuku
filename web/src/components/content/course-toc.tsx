"use client";

import { useCallback, useMemo, useRef, useState, type DragEvent, type ReactNode } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { ArrowUpDown, Check, ChevronDown, ChevronRight, GraduationCap, GripVertical, ListTree, Loader2, Lock } from "lucide-react";
import type { LectureCourseDetail, LectureTocEntry } from "@/api/generated/model";
import { useMoveNode } from "@/components/content/course-admin";
import { useIsAdmin } from "@/components/content/lecture-notes";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetTitle } from "@/components/ui/sheet";
import { type DropZone, resolveDrop } from "@/lib/lecture-moves";
import { cn } from "@/lib/utils";

type TocNode = LectureTocEntry & { children: TocNode[] };

/** Düz içindekiler listesinden (API sırası korunur) ağaç. */
function buildTree(entries: LectureTocEntry[]) {
  const nodes = new Map<string, TocNode>(entries.map((e) => [e.id, { ...e, children: [] }]));
  const roots: TocNode[] = [];
  for (const entry of entries) {
    const node = nodes.get(entry.id)!;
    const parent = entry.parentId ? nodes.get(entry.parentId) : undefined;
    if (parent) parent.children.push(node);
    else roots.push(node);
  }
  return { roots, byId: nodes };
}

/** Adresteki açık başlık: /ders-notlari/{ders}/{başlık}[/duzenle]. */
function useSelectedSlug(courseSlug: string) {
  const pathname = usePathname();
  const prefix = `/ders-notlari/${courseSlug}/`;
  if (!pathname.startsWith(prefix)) return null;
  return pathname.slice(prefix.length).split("/")[0] || null;
}

function ancestorsOf(id: string | undefined, byId: Map<string, TocNode>) {
  const result: string[] = [];
  let current = id ? byId.get(id)?.parentId : null;
  while (current && byId.has(current) && !result.includes(current)) {
    result.push(current);
    current = byId.get(current)!.parentId;
  }
  return result;
}

/** Sırala modu (yalnız geniş ekrandaki içindekilerde, yalnız yöneticide): açık mı ve açıp kapatma. */
type SortMode = { sorting: boolean; onSortingChange: (sorting: boolean) => void };

/** Sürüklerken satırın üst/alt dörtte biri önüne/arkasına, ortası içine bırakma. */
const zoneOf = (event: DragEvent<HTMLElement>): DropZone => {
  const rect = event.currentTarget.getBoundingClientRect();
  const y = event.clientY - rect.top;
  return y < rect.height * 0.25 ? "before" : y > rect.height * 0.75 ? "after" : "inside";
};

const EXPAND_DELAY_MS = 700;
const SCROLL_EDGE_PX = 48;
const SCROLL_STEP_PX = 14;

/**
 * Dersin içindekileri: numaralı, açılır-kapanır başlıklar. Açık başlığın üst başlıkları açık, açık başlık vurgulu
 * (`aria-current`). Açma düğmesi ve başlık bağlantısı ayrı öğelerdir (Tab ile ikisine de gidilir).
 * `sort` verilirse (geniş ekran) yöneticiye "Sırala" modu: başlıklar sürüklenip başka bir başlığın önüne, arkasına ya
 * da içine bırakılarak taşınır (spec 0003); klavye ve telefon yolu başlık sayfasındaki Taşı/yukarı/aşağı düğmeleridir.
 */
export function CourseToc({ course, footer, sort }: { course: LectureCourseDetail; footer?: ReactNode; sort?: SortMode }) {
  const { roots, byId } = useMemo(() => buildTree(course.toc), [course.toc]);
  const selectedSlug = useSelectedSlug(course.slug);
  const selected = selectedSlug ? course.toc.find((e) => e.slug === selectedSlug) : undefined;
  const isAdmin = useIsAdmin();
  const canSort = Boolean(sort) && isAdmin && course.toc.length > 0;
  const sorting = canSort && Boolean(sort?.sorting);

  const ancestors = ancestorsOf(selected?.id, byId);
  const [expanded, setExpanded] = useState(() => new Set(ancestors));
  // Başka bir başlığa geçilince (önceki/sonraki, arama) ya da açık başlık başka yere taşınınca yeni üst başlıkları da
  // açılır; kullanıcının açtıkları kalır.
  const ancestorKey = `${selected?.id}|${ancestors.join()}`;
  const [lastAncestorKey, setLastAncestorKey] = useState(ancestorKey);
  if (ancestorKey !== lastAncestorKey) {
    setLastAncestorKey(ancestorKey);
    const missing = ancestors.filter((id) => !expanded.has(id));
    if (missing.length > 0) setExpanded(new Set([...expanded, ...missing]));
  }

  const toggle = useCallback((id: string) => {
    setExpanded((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  // Açık başlık uzun içindekilerde görünür kalsın.
  const revealSelected = useCallback((element: HTMLAnchorElement | null) => {
    element?.scrollIntoView({ block: "nearest" });
  }, []);

  // ——— Sürükle-bırak ———
  const move = useMoveNode(course.toc, course.visibility);
  const [dragId, setDragId] = useState<string | null>(null);
  const [drop, setDrop] = useState<{ id: string; zone: DropZone } | null>(null);
  const [savingId, setSavingId] = useState<string | null>(null);
  const expandTimer = useRef<{ id: string; timer: ReturnType<typeof setTimeout> } | null>(null);
  const scroller = useRef<HTMLDivElement>(null);

  const clearExpandTimer = () => {
    if (expandTimer.current) clearTimeout(expandTimer.current.timer);
    expandTimer.current = null;
  };

  const dnd: Dnd | undefined = sorting
    ? {
        dragId,
        drop,
        savingId,
        busy: savingId !== null || move.pending,
        onDragStart(event, node) {
          event.dataTransfer.effectAllowed = "move";
          event.dataTransfer.setData("text/plain", node.id);
          setDragId(node.id);
        },
        onDragOver(event, node) {
          if (!dragId) return;
          const zone = zoneOf(event);
          // Geçersiz hedef (kendisi, alt başlıkları) ya da yeri değiştirmeyen bırakma: gösterge yok, bırakılamaz.
          if (!resolveDrop(course.toc, dragId, node.id, zone)) {
            if (drop) setDrop(null);
            return;
          }
          event.preventDefault();
          event.dataTransfer.dropEffect = "move";
          if (drop?.id !== node.id || drop.zone !== zone) setDrop({ id: node.id, zone });
          // Kapalı dalın üzerinde beklenirse dal açılır.
          if (node.children.length > 0 && !expanded.has(node.id) && expandTimer.current?.id !== node.id) {
            clearExpandTimer();
            expandTimer.current = {
              id: node.id,
              timer: setTimeout(() => {
                setExpanded((current) => new Set([...current, node.id]));
                expandTimer.current = null;
              }, EXPAND_DELAY_MS),
            };
          }
        },
        onDragLeave(node) {
          if (expandTimer.current?.id === node.id) clearExpandTimer();
        },
        async onDrop(event, node) {
          event.preventDefault();
          const draggedId = dragId ?? event.dataTransfer.getData("text/plain");
          const target = draggedId ? resolveDrop(course.toc, draggedId, node.id, zoneOf(event)) : null;
          setDrop(null);
          setDragId(null);
          clearExpandTimer();
          if (!draggedId || !target) return;
          setSavingId(draggedId);
          try {
            // Taşınan başlık yeni yerinde görünsün: içine bırakılan (kapalı olabilen) üst başlık açılır.
            const moved = await move.request({ noteId: draggedId, target });
            const parentId = target.parentId;
            if (moved && parentId) setExpanded((current) => new Set([...current, parentId]));
          } finally {
            setSavingId(null);
          }
        },
        onDragEnd() {
          // Esc, ağacın dışına bırakma ya da başarılı bırakma: gösterge temizlenir; kayıt yalnız onDrop'ta.
          setDragId(null);
          setDrop(null);
          clearExpandTimer();
        },
      }
    : undefined;

  /** Sürüklerken panelin üst/alt kenarına yaklaşınca içindekiler kayar (uzun derslerde görünmeyen yere taşımak için). */
  function autoScroll(event: DragEvent<HTMLDivElement>) {
    const element = scroller.current;
    if (!dragId || !element) return;
    const rect = element.getBoundingClientRect();
    if (event.clientY < rect.top + SCROLL_EDGE_PX) element.scrollBy(0, -SCROLL_STEP_PX);
    else if (event.clientY > rect.bottom - SCROLL_EDGE_PX) element.scrollBy(0, SCROLL_STEP_PX);
  }

  return (
    <nav aria-label="İçindekiler" className="flex h-full flex-col">
      <div className="flex items-center gap-2 bg-primary px-3 py-2.5 text-primary-foreground">
        <GraduationCap className="size-5 shrink-0" aria-hidden />
        <Link
          href={`/ders-notlari/${course.slug}`}
          aria-current={selectedSlug === null ? "page" : undefined}
          className="min-w-0 truncate text-lg hover:underline"
        >
          {course.title}
        </Link>
      </div>
      {canSort && sort && (
        <div className="space-y-1.5 border-b px-2 py-2">
          <Button
            size="sm"
            variant={sorting ? "default" : "outline"}
            aria-pressed={sorting}
            onClick={() => sort.onSortingChange(!sorting)}
          >
            {sorting ? <Check /> : <ArrowUpDown />}
            {sorting ? "Bitti" : "Sırala"}
          </Button>
          {sorting && (
            <p className="text-xs text-muted-foreground">
              Başlığı tutup sürükleyin: bir başlığın üst ya da alt kenarına bırakınca önüne ya da arkasına, ortasına
              bırakınca içine taşınır. Esc ile vazgeçilir.
            </p>
          )}
        </div>
      )}
      <div ref={scroller} onDragOver={autoScroll} className="min-h-0 flex-1 overflow-y-auto p-2">
        {roots.length === 0 ? (
          <p className="p-2 text-sm text-muted-foreground">Bu derse henüz başlık eklenmedi.</p>
        ) : (
          <TocList
            nodes={roots}
            courseSlug={course.slug}
            selectedId={selected?.id}
            expanded={expanded}
            onToggle={toggle}
            selectedRef={revealSelected}
            dnd={dnd}
          />
        )}
        {footer && <div className="mt-2">{footer}</div>}
      </div>
      {move.confirmDialog}
    </nav>
  );
}

type Dnd = {
  dragId: string | null;
  drop: { id: string; zone: DropZone } | null;
  savingId: string | null;
  busy: boolean;
  onDragStart: (event: DragEvent<HTMLElement>, node: TocNode) => void;
  onDragOver: (event: DragEvent<HTMLElement>, node: TocNode) => void;
  onDragLeave: (node: TocNode) => void;
  onDrop: (event: DragEvent<HTMLElement>, node: TocNode) => void;
  onDragEnd: () => void;
};

function TocList({
  nodes,
  courseSlug,
  selectedId,
  expanded,
  onToggle,
  selectedRef,
  dnd,
}: {
  nodes: TocNode[];
  courseSlug: string;
  selectedId: string | undefined;
  expanded: Set<string>;
  onToggle: (id: string) => void;
  selectedRef: (element: HTMLAnchorElement | null) => void;
  dnd?: Dnd;
}) {
  return (
    <ul>
      {nodes.map((node) => {
        const open = expanded.has(node.id);
        const isSelected = node.id === selectedId;
        const name = `${node.label} ${node.title}`;
        const zone = dnd?.drop?.id === node.id ? dnd.drop.zone : null;
        const saving = dnd?.savingId === node.id;
        return (
          <li key={node.id}>
            <div
              className={cn(
                "flex items-start gap-0.5 rounded-md",
                dnd?.dragId === node.id && "opacity-50",
                saving && "opacity-60",
                // Gösterge yalnız renge dayanmaz: çizgi (önüne/arkasına) ya da çerçeve (içine).
                zone === "before" && "shadow-[inset_0_3px_0_0_var(--primary)]",
                zone === "after" && "shadow-[inset_0_-3px_0_0_var(--primary)]",
                zone === "inside" && "ring-2 ring-primary ring-inset",
              )}
              style={{ paddingLeft: `${(node.depth - 1) * 12}px` }}
              draggable={dnd ? !dnd.busy : undefined}
              aria-busy={saving || undefined}
              onDragStart={dnd ? (event) => dnd.onDragStart(event, node) : undefined}
              onDragOver={dnd ? (event) => dnd.onDragOver(event, node) : undefined}
              onDragLeave={dnd ? () => dnd.onDragLeave(node) : undefined}
              onDrop={dnd ? (event) => dnd.onDrop(event, node) : undefined}
              onDragEnd={dnd ? dnd.onDragEnd : undefined}
            >
              {dnd && (
                <span
                  role="img"
                  aria-label={`Sürükle: ${name}`}
                  title="Sürükleyerek taşı"
                  className={cn("mt-1 shrink-0 text-muted-foreground", dnd.busy ? "cursor-wait" : "cursor-grab")}
                >
                  {saving ? <Loader2 className="size-4 motion-safe:animate-spin" aria-hidden /> : <GripVertical className="size-4" aria-hidden />}
                </span>
              )}
              {node.children.length > 0 ? (
                <button
                  type="button"
                  onClick={() => onToggle(node.id)}
                  aria-expanded={open}
                  aria-label={`${open ? "Kapat" : "Aç"}: ${name}`}
                  className="mt-0.5 rounded p-1 text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                >
                  {open ? <ChevronDown className="size-4" aria-hidden /> : <ChevronRight className="size-4" aria-hidden />}
                </button>
              ) : (
                <span className="w-6 shrink-0" aria-hidden />
              )}
              <Link
                href={`/ders-notlari/${courseSlug}/${node.slug}`}
                aria-current={isSelected ? "page" : undefined}
                ref={isSelected ? selectedRef : undefined}
                // Sırala modunda satır sürüklenir; bağlantının kendi sürüklemesi (adres) kapatılır.
                draggable={dnd ? false : undefined}
                className={cn(
                  "flex min-w-0 flex-1 items-start gap-1.5 rounded-md px-1.5 py-1 text-sm leading-snug",
                  isSelected ? "bg-primary text-primary-foreground" : "hover:bg-accent hover:text-accent-foreground",
                )}
              >
                <span className={cn("shrink-0 tabular-nums", !isSelected && "text-primary")}>{node.label}</span>
                <span className="min-w-0 break-words">{node.title}</span>
                {node.effectiveVisibility === "private" && (
                  <Lock className="mt-0.5 size-3.5 shrink-0 opacity-70" aria-label="Özel: yalnız giriş yapanlar görür" />
                )}
              </Link>
            </div>
            {open && node.children.length > 0 && (
              <TocList
                nodes={node.children}
                courseSlug={courseSlug}
                selectedId={selectedId}
                expanded={expanded}
                onToggle={onToggle}
                selectedRef={selectedRef}
                dnd={dnd}
              />
            )}
          </li>
        );
      })}
    </ul>
  );
}

/**
 * Ders sayfalarının düzeni (/ders-notlari/{ders} ve altı): geniş ekranda solda içindekiler, ortada içerik; ikisi
 * ayrı kayar. Dar ekranda içindekiler üstteki düğmeyle soldan açılan çekmecededir; bir başlık seçilince kapanır.
 * "Sırala" modu yalnız geniş ekrandaki içindekilerdedir; layout başlıklar arasında gezinirken korunduğundan mod açık
 * kalır, sayfa yenilenince kapanır.
 */
export function CourseShell({ course, tocFooter, children }: { course: LectureCourseDetail; tocFooter?: ReactNode; children: ReactNode }) {
  const [open, setOpen] = useState(false);
  const [sorting, setSorting] = useState(false);
  const pathname = usePathname();
  const [lastPathname, setLastPathname] = useState(pathname);
  if (pathname !== lastPathname) {
    setLastPathname(pathname);
    setOpen(false);
  }

  return (
    <div className="flex flex-1 flex-col lg:grid lg:min-h-0 lg:grid-cols-[minmax(16rem,22rem)_minmax(0,1fr)]">
      <aside className="hidden border-r bg-sidebar lg:block lg:min-h-0 print:hidden">
        <CourseToc course={course} footer={tocFooter} sort={{ sorting, onSortingChange: setSorting }} />
      </aside>
      <div className="min-w-0 lg:min-h-0 lg:overflow-y-auto">
        <div className="flex items-center gap-2 border-b bg-card px-4 py-2 lg:hidden print:hidden">
          <Button variant="outline" size="sm" onClick={() => setOpen(true)}>
            <ListTree /> İçindekiler
          </Button>
          <Link href={`/ders-notlari/${course.slug}`} className="min-w-0 truncate text-sm text-muted-foreground hover:text-primary">
            {course.title}
          </Link>
        </div>
        {children}
      </div>
      <Sheet open={open} onOpenChange={setOpen}>
        <SheetContent side="left" className="w-[88vw] max-w-sm p-0">
          <SheetTitle className="sr-only">İçindekiler</SheetTitle>
          <CourseToc course={course} footer={tocFooter} />
        </SheetContent>
      </Sheet>
    </div>
  );
}
