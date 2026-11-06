"use client";

import { useEffect, useRef, useState } from "react";
import { ArrowDown, ArrowUp, Download, ExternalLink, FileImage, FileSpreadsheet, FileText, Paperclip, Presentation, Undo2, X } from "lucide-react";
import { toast } from "sonner";
import type { LectureNoteFileDto } from "@/api/generated/model";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { formatBytes } from "@/lib/attachments";
import { cn } from "@/lib/utils";

/** Sunucunun kabul ettiği türler (DocumentFiles ile aynı). */
const allowedExtensions = [".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg"];
const maxBytes = 50 * 1024 * 1024;

const extensionOf = (name: string) => {
  const dot = name.lastIndexOf(".");
  return dot < 0 ? "" : name.slice(dot).toLowerCase();
};

/** Dosya adına göre tür etiketi ve simgesi. */
function fileKind(fileName: string) {
  const ext = extensionOf(fileName);
  if (ext === ".pdf") return { label: "PDF", Icon: FileText };
  if (ext === ".doc" || ext === ".docx") return { label: "Word", Icon: FileText };
  if (ext === ".ppt" || ext === ".pptx") return { label: "PowerPoint", Icon: Presentation };
  if (ext === ".xls" || ext === ".xlsx") return { label: "Excel", Icon: FileSpreadsheet };
  return { label: "Resim", Icon: FileImage };
}

/** PDF ve resim tarayıcıda açılır; diğerleri iner. */
const opensInBrowser = (contentType: string) => contentType === "application/pdf" || contentType.startsWith("image/");

/**
 * Ders notu sayfasındaki belgeler. Aramadan "#belge-{id}" ile gelinince o belgeye kaydırılır ve kısa süre vurgulanır
 * (Next.js'in sayfa geçişi :target'ı güncellemez).
 */
export function LectureNoteFileList({ files }: { files: LectureNoteFileDto[] }) {
  const listRef = useRef<HTMLUListElement>(null);
  useEffect(() => {
    function reveal() {
      const hash = window.location.hash;
      if (!hash.startsWith("#belge-")) return;
      const target = listRef.current?.querySelector<HTMLElement>(`[id="${CSS.escape(hash.slice(1))}"]`);
      if (!target) return;
      target.scrollIntoView({ block: "center", behavior: "smooth" });
      target.animate([{ boxShadow: "0 0 0 3px var(--primary)" }, { boxShadow: "0 0 0 3px transparent" }], {
        duration: 1800,
        easing: "ease-out",
      });
    }
    reveal();
    window.addEventListener("hashchange", reveal);
    return () => window.removeEventListener("hashchange", reveal);
  }, []);

  if (files.length === 0) return null;
  return (
    <section className="mt-6" aria-labelledby="belgeler">
      <h2 id="belgeler" className="flex items-center gap-2 text-xl">
        <Paperclip className="size-5 text-primary" aria-hidden /> Belgeler
      </h2>
      <ul ref={listRef} className="mt-2 divide-y rounded-lg border bg-card">
        {files.map((file) => {
          const { label, Icon } = fileKind(file.fileName);
          const inline = opensInBrowser(file.contentType);
          return (
            <li key={file.id} id={`belge-${file.id}`} className="flex flex-wrap items-center gap-3 px-4 py-3 first:rounded-t-lg last:rounded-b-lg">
              <Icon className="size-6 shrink-0 text-primary" aria-hidden />
              <div className="min-w-0 flex-1">
                <a
                  href={inline ? file.url : `${file.url}?download=true`}
                  target={inline ? "_blank" : undefined}
                  rel={inline ? "noopener" : undefined}
                  className="font-medium break-words text-primary hover:underline"
                >
                  {file.title}
                </a>
                <p className="text-sm break-all text-muted-foreground">
                  {[label, formatBytes(file.sizeBytes), file.pageCount != null ? `${file.pageCount} sayfa` : null, file.fileName]
                    .filter(Boolean)
                    .join(" · ")}
                </p>
              </div>
              <div className="flex gap-1 print:hidden">
                {inline && (
                  <Button size="sm" variant="outline" asChild>
                    <a href={file.url} target="_blank" rel="noopener">
                      <ExternalLink /> Aç
                    </a>
                  </Button>
                )}
                <Button size="sm" variant="outline" asChild>
                  <a href={`${file.url}?download=true`} download>
                    <Download /> İndir
                  </a>
                </Button>
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

/** Formdaki belge: kayıtlı (silinmek üzere işaretlenebilir) ya da kaydedilince yüklenecek yeni dosya. */
export type FileItem =
  | { key: string; kind: "existing"; file: LectureNoteFileDto; title: string; removed: boolean }
  | { key: string; kind: "new"; file: File; title: string };

export const fileItemsOf = (files: LectureNoteFileDto[]): FileItem[] =>
  files.map((file) => ({ key: file.id, kind: "existing", file, title: file.title, removed: false }));

const titleFromName = (name: string) => name.replace(/\.[^.]+$/, "");

/**
 * Formda belgeler: ekle (seç ya da sürükle-bırak), adını değiştir, sırala, kaldır. Değişiklikler ders notu
 * kaydedilince uygulanır; "Vazgeç" hepsini bırakır.
 */
export function LectureNoteFilesEditor({ items, onChange }: { items: FileItem[]; onChange: (items: FileItem[]) => void }) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);

  function add(files: FileList | null) {
    if (!files) return;
    const added: FileItem[] = [];
    for (const file of Array.from(files)) {
      if (!allowedExtensions.includes(extensionOf(file.name))) {
        toast.error(`${file.name}: yalnız PDF, Word, PowerPoint, Excel ve resim (PNG, JPG) eklenebilir.`);
      } else if (file.size === 0 || file.size > maxBytes) {
        toast.error(`${file.name}: dosya boş ya da 50 MB'tan büyük.`);
      } else {
        added.push({ key: crypto.randomUUID(), kind: "new", file, title: titleFromName(file.name) });
      }
    }
    if (added.length > 0) onChange([...items, ...added]);
    if (inputRef.current) inputRef.current.value = "";
  }

  const update = (key: string, change: (item: FileItem) => FileItem | null) =>
    onChange(items.flatMap((item) => (item.key === key ? (change(item) ?? []) : [item])));
  const move = (index: number, by: number) => {
    const next = [...items];
    [next[index], next[index + by]] = [next[index + by], next[index]];
    onChange(next);
  };

  return (
    <fieldset
      className={cn("space-y-3 rounded-md border p-3", dragging && "border-primary bg-primary/5")}
      onDragOver={(e) => {
        if (!e.dataTransfer.types.includes("Files")) return;
        e.preventDefault();
        setDragging(true);
      }}
      onDragLeave={() => setDragging(false)}
      onDrop={(e) => {
        e.preventDefault();
        setDragging(false);
        add(e.dataTransfer.files);
      }}
    >
      <legend className="px-1 text-sm font-medium">Belgeler (isteğe bağlı)</legend>
      {items.length > 0 && (
        <ul className="divide-y rounded-md border">
          {items.map((item, index) => {
            const name = item.kind === "new" ? item.file.name : item.file.fileName;
            const size = item.kind === "new" ? item.file.size : item.file.sizeBytes;
            const removed = item.kind === "existing" && item.removed;
            const { label, Icon } = fileKind(name);
            return (
              <li key={item.key} className={cn("flex flex-wrap items-center gap-2 p-2", removed && "opacity-60")}>
                <Icon className="size-5 shrink-0 text-primary" aria-hidden />
                <div className="min-w-48 flex-1 space-y-1">
                  <Input
                    aria-label={`Belge adı: ${name}`}
                    value={item.title}
                    maxLength={300}
                    disabled={removed}
                    onChange={(e) => update(item.key, (i) => ({ ...i, title: e.target.value }))}
                    className={cn("h-8", removed && "line-through")}
                  />
                  <p className="flex flex-wrap items-center gap-x-2 text-xs break-all text-muted-foreground">
                    {[label, formatBytes(size), name].join(" · ")}
                    {item.kind === "new" && <Badge variant="secondary">kaydedince eklenecek</Badge>}
                    {removed && <Badge variant="destructive">kaydedince kaldırılacak</Badge>}
                  </p>
                </div>
                <div className="flex gap-0.5">
                  <Button
                    type="button"
                    size="icon-sm"
                    variant="ghost"
                    aria-label="Yukarı taşı"
                    title="Yukarı taşı"
                    disabled={index === 0}
                    onClick={() => move(index, -1)}
                  >
                    <ArrowUp />
                  </Button>
                  <Button
                    type="button"
                    size="icon-sm"
                    variant="ghost"
                    aria-label="Aşağı taşı"
                    title="Aşağı taşı"
                    disabled={index === items.length - 1}
                    onClick={() => move(index, 1)}
                  >
                    <ArrowDown />
                  </Button>
                  {removed ? (
                    <Button
                      type="button"
                      size="icon-sm"
                      variant="ghost"
                      aria-label="Geri al"
                      title="Geri al"
                      onClick={() => update(item.key, (i) => ({ ...i, removed: false }))}
                    >
                      <Undo2 />
                    </Button>
                  ) : (
                    <Button
                      type="button"
                      size="icon-sm"
                      variant="ghost"
                      aria-label={`Kaldır: ${item.title || name}`}
                      title="Kaldır"
                      onClick={() => update(item.key, (i) => (i.kind === "new" ? null : { ...i, removed: true }))}
                    >
                      <X />
                    </Button>
                  )}
                </div>
              </li>
            );
          })}
        </ul>
      )}
      <div className="flex flex-wrap items-center gap-3">
        <Button type="button" variant="outline" onClick={() => inputRef.current?.click()}>
          <Paperclip /> Belge ekle
        </Button>
        <span className="text-xs text-muted-foreground">
          PDF, Word, PowerPoint, Excel ya da resim; en fazla 50 MB. Dosyayı buraya sürükleyip de bırakabilirsiniz. Metni
          okunabilen belgeler (PDF, .docx, .pptx, .xlsx) aramada da bulunur.
        </span>
        <input
          ref={inputRef}
          type="file"
          multiple
          hidden
          accept={allowedExtensions.join(",")}
          aria-label="Belge seç"
          onChange={(e) => add(e.target.files)}
        />
      </div>
    </fieldset>
  );
}
