"use client";

import { useCallback, useState, type ReactNode } from "react";
import Link from "next/link";
import { BookOpen, ChevronLeft, ChevronRight, NotebookPen, PanelRightClose, PanelRightOpen } from "lucide-react";
import { usePathname } from "next/navigation";
import { useDefaultLayout, usePanelRef } from "react-resizable-panels";
import { Button } from "@/components/ui/button";
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from "@/components/ui/resizable";
import { Sheet, SheetContent, SheetTitle } from "@/components/ui/sheet";
import { useMediaQuery } from "@/hooks/use-media-query";
import { cn } from "@/lib/utils";
import { CurrentArticleProvider, useCurrentArticle, type ArticleLink } from "./current-article";

type WorkspaceProps = {
  /** Sol panel: kütüphane / kanun ağacı. */
  navigation: ReactNode;
  /** Orta alan: madde içeriği. */
  content: ReactNode;
  /** Sağ panel: kişisel notlar ve vurgular. */
  notes: ReactNode;
};

// localStorage gizli pencerede ya da engelliyken hata atabilir; panel boyutu hatırlanmasa da düzen çalışır.
const safeStorage = {
  getItem(key: string) {
    try {
      return window.localStorage.getItem(key);
    } catch {
      return null;
    }
  },
  setItem(key: string, value: string) {
    try {
      window.localStorage.setItem(key, value);
    } catch {
      // yoksay
    }
  },
};

/**
 * Ana çalışma alanı: masaüstünde boyutlandırılabilir 3 panel (sol gezinme, orta içerik, sağ notlar —
 * sağ panel kapatılabilir ve kapalıyken içerik genişler). Dar ekranda yalnız içerik görünür (sayfayla birlikte kayar),
 * gezinme ve notlar kenardan açılan çekmecelerde; alttaki sabit çubukta önceki/sonraki madde, Kütüphane ve Notlarım. Açık madde bağlamla (CurrentArticleProvider) sağ panele iletilir.
 */
export function Workspace(props: WorkspaceProps) {
  const isDesktop = useMediaQuery("(min-width: 1024px)");
  return isDesktop ? <DesktopWorkspace {...props} /> : <MobileWorkspace {...props} />;
}

function DesktopWorkspace({ navigation, content, notes }: WorkspaceProps) {
  const notesPanel = usePanelRef();
  const [notesOpen, setNotesOpen] = useState(true);
  // Yalnız kullanıcının sürüklemesi kaydedilir: ilk hidrasyonda dar pencerede bir an kurulan masaüstü
  // düzeninin uç oranları ya da pencere boyutu değişimleri kalıcı hâle gelmesin.
  const layout = useDefaultLayout({ id: "workspace", storage: safeStorage, onlySaveAfterUserInteractions: true });

  function toggleNotes() {
    const panel = notesPanel.current;
    if (!panel) return;
    if (panel.isCollapsed()) panel.expand();
    else panel.collapse();
  }

  return (
    <CurrentArticleProvider>
      <ResizablePanelGroup
        orientation="horizontal"
        defaultLayout={layout.defaultLayout}
        onLayoutChanged={layout.onLayoutChanged}
        className="min-h-0 flex-1"
      >
        <ResizablePanel id="navigation" defaultSize="24" minSize={260} maxSize="40" className="print:hidden">
          <div className="h-full border-r bg-sidebar">{navigation}</div>
        </ResizablePanel>
        <ResizableHandle className="print:hidden" />
        <ResizablePanel id="content" minSize="35">
          <div className="relative h-full">
            <Button
              variant="ghost"
              size="icon"
              onClick={toggleNotes}
              className="absolute top-2 right-2 z-10 print:hidden"
              aria-label={notesOpen ? "Not panelini kapat" : "Not panelini aç"}
              title={notesOpen ? "Not panelini kapat" : "Not panelini aç"}
            >
              {notesOpen ? <PanelRightClose /> : <PanelRightOpen />}
            </Button>
            {content}
          </div>
        </ResizablePanel>
        <ResizableHandle className="print:hidden" />
        <ResizablePanel
          id="notes"
          panelRef={notesPanel}
          defaultSize="22"
          minSize={260}
          maxSize="35"
          collapsible
          collapsedSize={0}
          onResize={(size) => setNotesOpen(size.asPercentage > 0)}
          className="print:hidden"
        >
          <div className="h-full border-l bg-sidebar">{notes}</div>
        </ResizablePanel>
      </ResizablePanelGroup>
    </CurrentArticleProvider>
  );
}

function MobileWorkspace({ navigation, content, notes }: WorkspaceProps) {
  const [open, setOpen] = useState<"navigation" | "notes" | null>(null);
  // Çekmeceden bir madde seçilince (adres değişince) çekmece kapanır, okuyucu içeriğe döner.
  const pathname = usePathname();
  const [lastPathname, setLastPathname] = useState(pathname);
  if (pathname !== lastPathname) {
    setLastPathname(pathname);
    setOpen(null);
  }

  // Notlarım çekmecesinden metindeki bir vurguya gidilince çekmece kapanır.
  const closeSheet = useCallback(() => setOpen(null), []);

  return (
    <CurrentArticleProvider onReveal={closeSheet}>
      <div className="flex flex-1 flex-col">
        <div className="flex-1">{content}</div>
        <MobileBottomBar onOpen={setOpen} />

        <Sheet open={open === "navigation"} onOpenChange={(value) => setOpen(value ? "navigation" : null)}>
          <SheetContent side="left" className="w-[88vw] max-w-sm p-0">
            <SheetTitle className="sr-only">Kütüphane</SheetTitle>
            {navigation}
          </SheetContent>
        </Sheet>
        <Sheet open={open === "notes"} onOpenChange={(value) => setOpen(value ? "notes" : null)}>
          <SheetContent side="right" className="w-[88vw] max-w-sm p-0">
            <SheetTitle className="sr-only">Notlarım</SheetTitle>
            {notes}
          </SheetContent>
        </Sheet>
      </div>
    </CurrentArticleProvider>
  );
}

const barCell = "flex min-w-0 flex-1 flex-col items-center justify-center gap-0.5 px-1 py-1.5 text-xs";

/** Dar ekranda alta yapışan çubuk: başparmakla önceki/sonraki madde, kütüphane ve notlar. */
function MobileBottomBar({ onOpen }: { onOpen: (panel: "navigation" | "notes") => void }) {
  const { article } = useCurrentArticle();
  return (
    <nav
      aria-label="Madde gezinmesi"
      className="sticky bottom-0 z-30 flex border-t bg-card pb-[env(safe-area-inset-bottom)] shadow-[0_-2px_6px_rgb(0_0_0/0.04)] print:hidden"
    >
      <PagerCell link={article?.previous ?? null} direction="previous" />
      <button type="button" className={cn(barCell, "hover:bg-accent")} onClick={() => onOpen("navigation")}>
        <BookOpen className="size-5" aria-hidden />
        Kütüphane
      </button>
      <button type="button" className={cn(barCell, "hover:bg-accent")} onClick={() => onOpen("notes")}>
        <NotebookPen className="size-5" aria-hidden />
        Notlarım
      </button>
      <PagerCell link={article?.next ?? null} direction="next" />
    </nav>
  );
}

function PagerCell({ link, direction }: { link: ArticleLink | null; direction: "previous" | "next" }) {
  const Icon = direction === "previous" ? ChevronLeft : ChevronRight;
  const fallback = direction === "previous" ? "Önceki" : "Sonraki";
  if (!link) {
    return (
      <span className={cn(barCell, "text-muted-foreground/50")} aria-hidden>
        <Icon className="size-5" />
        {fallback}
      </span>
    );
  }
  return (
    <Link
      href={link.href}
      className={cn(barCell, "hover:bg-accent")}
      aria-label={`${fallback} madde: ${link.label}`}
    >
      <Icon className="size-5" aria-hidden />
      <span className="max-w-full truncate">{link.label}</span>
    </Link>
  );
}
