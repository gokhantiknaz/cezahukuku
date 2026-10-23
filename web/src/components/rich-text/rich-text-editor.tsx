"use client";

import { EditorContent, useEditor, useEditorState, type Editor } from "@tiptap/react";
import StarterKit from "@tiptap/starter-kit";
import {
  Bold,
  Heading2,
  Heading3,
  Italic,
  Link2,
  List,
  ListOrdered,
  Quote,
  Redo2,
  Strikethrough,
  Underline,
  Undo2,
  type LucideIcon,
} from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Gerekçe, açıklama ve ders notu için zengin metin düzenleyicisi. Yalnız sunucunun izin verdiği biçimler sunulur
 * (başlık, kalın/italik/altı çizili/üstü çizili, liste, alıntı, bağlantı); HTML yine de sunucuda temizlenir.
 */
export function RichTextEditor({
  value,
  onChange,
  label,
}: {
  /** Başlangıç içeriği (HTML). Sonradan değişirse düzenleyici yeniden kurulmalı (key ile). */
  value: string;
  onChange: (html: string) => void;
  label: string;
}) {
  const editor = useEditor({
    extensions: [
      StarterKit.configure({
        heading: { levels: [2, 3, 4] },
        codeBlock: false,
        link: { openOnClick: false, autolink: true, protocols: ["http", "https", "mailto"] },
      }),
    ],
    content: value,
    // Sunucuda çizilmez; hidrasyon uyuşmazlığını önler.
    immediatelyRender: false,
    editorProps: {
      attributes: {
        class: "rich-text min-h-48 px-3 py-2 focus:outline-none",
        "aria-label": label,
        role: "textbox",
        "aria-multiline": "true",
      },
    },
    onUpdate: ({ editor }) => onChange(editor.isEmpty ? "" : editor.getHTML()),
  });

  return (
    <div className="rounded-md border bg-background focus-within:ring-[3px] focus-within:ring-ring/50">
      {editor && <Toolbar editor={editor} />}
      <EditorContent editor={editor} />
    </div>
  );
}

function Toolbar({ editor }: { editor: Editor }) {
  // Araç çubuğu düğmeleri imlecin bulunduğu biçimi gösterir; editor her değiştiğinde yeniden hesaplanır.
  const state = useEditorState({
    editor,
    selector: ({ editor }) => ({
      h2: editor.isActive("heading", { level: 2 }),
      h3: editor.isActive("heading", { level: 3 }),
      bold: editor.isActive("bold"),
      italic: editor.isActive("italic"),
      underline: editor.isActive("underline"),
      strike: editor.isActive("strike"),
      bullet: editor.isActive("bulletList"),
      ordered: editor.isActive("orderedList"),
      quote: editor.isActive("blockquote"),
      link: editor.isActive("link"),
      canUndo: editor.can().undo(),
      canRedo: editor.can().redo(),
    }),
  });

  function toggleLink() {
    if (state.link) {
      editor.chain().focus().unsetLink().run();
      return;
    }
    const url = window.prompt("Bağlantı adresi (https://…)");
    if (url && /^(https?:\/\/|mailto:)/i.test(url.trim())) {
      editor.chain().focus().extendMarkRange("link").setLink({ href: url.trim() }).run();
    }
  }

  const chain = () => editor.chain().focus();
  const buttons: { icon: LucideIcon; label: string; active?: boolean; disabled?: boolean; run: () => void }[] = [
    { icon: Heading2, label: "Başlık", active: state.h2, run: () => chain().toggleHeading({ level: 2 }).run() },
    { icon: Heading3, label: "Alt başlık", active: state.h3, run: () => chain().toggleHeading({ level: 3 }).run() },
    { icon: Bold, label: "Kalın", active: state.bold, run: () => chain().toggleBold().run() },
    { icon: Italic, label: "İtalik", active: state.italic, run: () => chain().toggleItalic().run() },
    { icon: Underline, label: "Altı çizili", active: state.underline, run: () => chain().toggleUnderline().run() },
    { icon: Strikethrough, label: "Üstü çizili", active: state.strike, run: () => chain().toggleStrike().run() },
    { icon: List, label: "Madde işaretli liste", active: state.bullet, run: () => chain().toggleBulletList().run() },
    { icon: ListOrdered, label: "Numaralı liste", active: state.ordered, run: () => chain().toggleOrderedList().run() },
    { icon: Quote, label: "Alıntı", active: state.quote, run: () => chain().toggleBlockquote().run() },
    { icon: Link2, label: state.link ? "Bağlantıyı kaldır" : "Bağlantı ekle", active: state.link, run: toggleLink },
    { icon: Undo2, label: "Geri al", disabled: !state.canUndo, run: () => chain().undo().run() },
    { icon: Redo2, label: "Yinele", disabled: !state.canRedo, run: () => chain().redo().run() },
  ];

  return (
    <div className="flex flex-wrap gap-0.5 border-b px-1 py-1" role="toolbar" aria-label="Biçimlendirme">
      {buttons.map(({ icon: Icon, label, active, disabled, run }) => (
        <button
          key={label}
          type="button"
          // Düğme odağı almasın: düzenleyici odaklı ve seçim yerinde kalır, tıklamadan hemen sonra yazılan harfler
          // kaybolmaz.
          onMouseDown={(e) => e.preventDefault()}
          onClick={run}
          disabled={disabled}
          title={label}
          aria-label={label}
          aria-pressed={active}
          className={cn(
            "rounded p-1.5 text-muted-foreground hover:bg-accent hover:text-accent-foreground disabled:opacity-40",
            active && "bg-accent text-accent-foreground",
          )}
        >
          <Icon className="size-4" />
        </button>
      ))}
    </div>
  );
}
