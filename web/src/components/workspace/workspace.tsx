"use client";

import { useState, type ReactNode } from "react";
import { BookOpen, NotebookPen, PanelRightClose, PanelRightOpen } from "lucide-react";
import { useDefaultLayout, usePanelRef } from "react-resizable-panels";
import { Button } from "@/components/ui/button";
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from "@/components/ui/resizable";
import { Sheet, SheetContent, SheetTitle } from "@/components/ui/sheet";
import { useMediaQuery } from "@/hooks/use-media-query";

type WorkspaceProps = {
  /** Sol panel: kütüphane / kanun ağacı. */
  navigation: ReactNode;
  /** Orta alan: madde içeriği. */
  content: ReactNode;
  /** Sağ panel: kişisel notlar ve vurgular. */
  notes: ReactNode;
  /** Seçili içeriğin anahtarı; değişince mobilde açık çekmece kapanır (madde seçildi → içeriğe dön). */
  selectionKey?: string;
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
 * sağ panel kapatılabilir ve kapalıyken içerik genişler). Dar ekranda yalnız içerik görünür,
 * gezinme ve notlar kenardan açılan çekmecelerde.
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
  );
}

function MobileWorkspace({ navigation, content, notes, selectionKey }: WorkspaceProps) {
  const [open, setOpen] = useState<"navigation" | "notes" | null>(null);
  const [lastSelectionKey, setLastSelectionKey] = useState(selectionKey);
  if (selectionKey !== lastSelectionKey) {
    setLastSelectionKey(selectionKey);
    setOpen(null);
  }

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="flex items-center justify-between border-b bg-card px-2 py-1 print:hidden">
        <Button variant="ghost" onClick={() => setOpen("navigation")}>
          <BookOpen /> Kütüphane
        </Button>
        <Button variant="ghost" onClick={() => setOpen("notes")}>
          <NotebookPen /> Notlarım
        </Button>
      </div>
      <div className="min-h-0 flex-1">{content}</div>

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
  );
}
