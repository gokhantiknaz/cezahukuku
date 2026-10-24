"use client";

import { useState } from "react";
import { Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";

/**
 * Kişisel not formu: metin alanı + Kaydet / Vazgeç (+ varsa Notu sil). Ctrl+Enter kaydeder, Esc vazgeçer.
 * Metindeki açılır not kutusunda ve sağ paneldeki listede kullanılır.
 */
export function NoteForm({
  initialText = "",
  busy,
  rows = 5,
  onSave,
  onCancel,
  onDelete,
}: {
  initialText?: string;
  busy: boolean;
  rows?: number;
  onSave: (text: string) => void;
  onCancel: () => void;
  onDelete?: () => void;
}) {
  const [text, setText] = useState(initialText);
  const save = () => text.trim() && onSave(text);

  return (
    <div
      className="space-y-2"
      onKeyDown={(e) => {
        if (e.key === "Escape") {
          e.stopPropagation();
          onCancel();
        }
        if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) save();
      }}
    >
      <Textarea
        autoFocus
        value={text}
        onChange={(e) => setText(e.target.value)}
        placeholder="Notunuzu yazın… (yalnız siz görürsünüz)"
        rows={rows}
        maxLength={10_000}
        aria-label="Not metni"
        className="font-reading text-base"
      />
      <div className="flex items-center justify-between gap-2">
        {onDelete ? (
          <Button size="sm" variant="ghost" className="text-destructive" disabled={busy} onClick={onDelete}>
            <Trash2 /> Notu sil
          </Button>
        ) : (
          <span className="text-xs text-muted-foreground">Ctrl+Enter ile kaydedin</span>
        )}
        <div className="flex gap-2">
          <Button size="sm" variant="ghost" onClick={onCancel}>
            Vazgeç
          </Button>
          <Button size="sm" disabled={busy || !text.trim()} onClick={save}>
            Kaydet
          </Button>
        </div>
      </div>
    </div>
  );
}
