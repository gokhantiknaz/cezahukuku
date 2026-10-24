"use client";

import { useMemo } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  getListHighlightsQueryKey,
  getListPersonalNotesQueryKey,
  useCreateHighlight,
  useCreatePersonalNote,
  useDeleteHighlight,
  useDeletePersonalNote,
  useListHighlights,
  useListPersonalNotes,
  useUpdateHighlight,
  useUpdatePersonalNote,
} from "@/api/generated/personal/personal";
import type { HighlightColor, HighlightDto, PersonalNoteDto, TextPosition } from "@/api/generated/model";
import { useHydrated } from "@/hooks/use-hydrated";
import { useCurrentUser } from "@/lib/auth";
import { canAnnotate } from "@/lib/highlights";

/**
 * Maddedeki kişisel vurgular ve notlar, değiştiren işlemlerle birlikte. Metin (HighlightableText) ve sağ panel
 * (NotesPanel) aynı sorgu anahtarlarını kullandığından veri bir kez çekilir, biri değiştirince ikisi de yenilenir.
 * Her işlem başarıda true döner; hatada bildirim gösterip false döner.
 */
export function usePersonalLayer(articleId: string | undefined, versionId: string | undefined) {
  const { data: user } = useCurrentUser();
  const enabled = useHydrated() && canAnnotate(user) && !!articleId;
  const id = articleId ?? "";

  const queryClient = useQueryClient();
  const highlightsQuery = useListHighlights(id, versionId ? { versionId } : undefined, { query: { enabled } });
  const notesQuery = useListPersonalNotes(id, { query: { enabled } });
  const highlights = useMemo(
    () => (enabled && highlightsQuery.data?.status === 200 ? highlightsQuery.data.data : []),
    [enabled, highlightsQuery.data],
  );
  const notes = useMemo(
    () => (enabled && notesQuery.data?.status === 200 ? notesQuery.data.data : []),
    [enabled, notesQuery.data],
  );
  const noteOf = useMemo(() => new Map(notes.filter((n) => n.highlightId).map((n) => [n.highlightId!, n])), [notes]);
  const articleNotes = useMemo(() => notes.filter((n) => !n.highlightId), [notes]);

  const createHighlight = useCreateHighlight();
  const updateHighlight = useUpdateHighlight();
  const deleteHighlight = useDeleteHighlight();
  const createNote = useCreatePersonalNote();
  const updateNote = useUpdatePersonalNote();
  const deleteNote = useDeletePersonalNote();
  const busy = [createHighlight, updateHighlight, deleteHighlight, createNote, updateNote, deleteNote].some((m) => m.isPending);

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: getListHighlightsQueryKey(id).slice(0, 1) }),
      queryClient.invalidateQueries({ queryKey: getListPersonalNotesQueryKey(id) }),
    ]);

  async function run(action: () => Promise<unknown>, failure: string) {
    try {
      await action();
      await refresh();
      return true;
    } catch {
      toast.error(failure);
      return false;
    }
  }

  return {
    enabled,
    loading: enabled && (highlightsQuery.isPending || notesQuery.isPending),
    highlights,
    notes,
    noteOf,
    articleNotes,
    busy,

    /** Seçimden vurgu oluşturur; oluşan vurguyu döner (hatada undefined). */
    async createHighlight(versionIdOfText: string, color: HighlightColor, segments: TextPosition[]) {
      let created: HighlightDto | undefined;
      await run(async () => {
        const response = await createHighlight.mutateAsync({ data: { articleVersionId: versionIdOfText, color, segments } });
        if (response.status === 201) created = response.data;
      }, "Vurgu kaydedilemedi.");
      return created;
    },
    recolor: (highlightId: string, color: HighlightColor) =>
      run(() => updateHighlight.mutateAsync({ id: highlightId, data: { color } }), "Vurgu kaydedilemedi."),
    removeHighlight: (highlightId: string) =>
      run(() => deleteHighlight.mutateAsync({ id: highlightId }), "Vurgu silinemedi."),

    /** Vurguya (highlightId) ya da maddenin geneline (null) not yazar; not varsa günceller. */
    async saveNote(highlightId: string | null, existing: PersonalNoteDto | undefined, text: string) {
      const saved = await run(
        () =>
          existing
            ? updateNote.mutateAsync({ id: existing.id, data: { text } })
            : createNote.mutateAsync({ data: { articleId: id, highlightId, text } }),
        "Not kaydedilemedi.",
      );
      if (saved) toast.success("Not kaydedildi.");
      return saved;
    },
    removeNote: (note: PersonalNoteDto) => run(() => deleteNote.mutateAsync({ id: note.id }), "Not silinemedi."),
  };
}

/** Vurgunun metindeki işaretlerine kaydırır ve kısa süre parlatır (paneldeki listeden). */
export function revealHighlight(highlightId: string) {
  const marks = [...document.querySelectorAll<HTMLElement>(`mark[data-highlight-id="${CSS.escape(highlightId)}"]`)];
  if (marks.length === 0) return false;
  marks[0].scrollIntoView({ block: "center", behavior: "smooth" });
  for (const mark of marks) {
    mark.animate(
      [
        { boxShadow: "0 0 0 3px var(--primary)" },
        { boxShadow: "0 0 0 3px transparent" },
      ],
      { duration: 700, iterations: 2, delay: 250 },
    );
  }
  return true;
}
