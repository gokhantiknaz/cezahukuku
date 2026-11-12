"use client";

import { useCallback, useMemo, useState, type ReactNode } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { ChevronDown, ChevronRight, GraduationCap, ListTree, Lock } from "lucide-react";
import type { LectureCourseDetail, LectureTocEntry } from "@/api/generated/model";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetTitle } from "@/components/ui/sheet";
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

/**
 * Dersin içindekileri: numaralı, açılır-kapanır başlıklar. Açık başlığın üst başlıkları açık, açık başlık vurgulu
 * (`aria-current`). Açma düğmesi ve başlık bağlantısı ayrı öğelerdir (Tab ile ikisine de gidilir).
 */
export function CourseToc({ course, footer }: { course: LectureCourseDetail; footer?: ReactNode }) {
  const { roots, byId } = useMemo(() => buildTree(course.toc), [course.toc]);
  const selectedSlug = useSelectedSlug(course.slug);
  const selected = selectedSlug ? course.toc.find((e) => e.slug === selectedSlug) : undefined;

  const [expanded, setExpanded] = useState(() => new Set(ancestorsOf(selected?.id, byId)));
  // Başka bir başlığa geçilince (önceki/sonraki, arama) onun üst başlıkları da açılır; kullanıcının açtıkları kalır.
  const [lastSelected, setLastSelected] = useState(selected?.id);
  if (selected?.id !== lastSelected) {
    setLastSelected(selected?.id);
    const missing = ancestorsOf(selected?.id, byId).filter((id) => !expanded.has(id));
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
      <div className="min-h-0 flex-1 overflow-y-auto p-2">
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
          />
        )}
        {footer && <div className="mt-2 border-t pt-2">{footer}</div>}
      </div>
    </nav>
  );
}

function TocList({
  nodes,
  courseSlug,
  selectedId,
  expanded,
  onToggle,
  selectedRef,
}: {
  nodes: TocNode[];
  courseSlug: string;
  selectedId: string | undefined;
  expanded: Set<string>;
  onToggle: (id: string) => void;
  selectedRef: (element: HTMLAnchorElement | null) => void;
}) {
  return (
    <ul>
      {nodes.map((node) => {
        const open = expanded.has(node.id);
        const isSelected = node.id === selectedId;
        const name = `${node.label} ${node.title}`;
        return (
          <li key={node.id}>
            <div className="flex items-start gap-0.5" style={{ paddingLeft: `${(node.depth - 1) * 12}px` }}>
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
 */
export function CourseShell({ course, tocFooter, children }: { course: LectureCourseDetail; tocFooter?: ReactNode; children: ReactNode }) {
  const [open, setOpen] = useState(false);
  const pathname = usePathname();
  const [lastPathname, setLastPathname] = useState(pathname);
  if (pathname !== lastPathname) {
    setLastPathname(pathname);
    setOpen(false);
  }

  return (
    <div className="flex flex-1 flex-col lg:grid lg:min-h-0 lg:grid-cols-[minmax(16rem,22rem)_minmax(0,1fr)]">
      <aside className="hidden border-r bg-sidebar lg:block lg:min-h-0 print:hidden">
        <CourseToc course={course} footer={tocFooter} />
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
