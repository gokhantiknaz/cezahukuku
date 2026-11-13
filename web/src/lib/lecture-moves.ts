// Ders içindekilerinde başlık taşımanın saf kuralları (spec 0003): bırakılan yerden yeni konum, geçersiz hedefler ve
// taşımanın görünürlüğe etkisi. Tarayıcı API'si ve React içermez; Node'un test koşucusuyla birim testi yapılır
// (lecture-moves.test.ts). Yalnız tür içe aktarımı vardır (çalışma anında silinir).
import type { Visibility } from "@/api/generated/model";

/** İçindekiler satırının bu kurallar için gereken alanları; liste içindekiler sırasındadır (önce başlık, sonra altı). */
export type TocItem = {
  id: string;
  parentId: string | null;
  visibility: Visibility;
  effectiveVisibility: Visibility;
};

/** Bir başlığın üstüne (önüne), altına (arkasına) ya da içine (alt başlığı olarak sona) bırakma. */
export type DropZone = "before" | "after" | "inside";

/** Sunucunun taşıma isteği: yeni üst başlık (boşsa birinci düzey) ve başlığın kendisi hariç yeni kardeşler arasındaki yeri. */
export type MoveTarget = { parentId: string | null; index: number };

const parentOf = (item: TocItem) => item.parentId ?? null;
const childrenOf = (toc: TocItem[], parentId: string | null) => toc.filter((e) => parentOf(e) === parentId);

/** Başlığın kendisi ve bütün alt başlıkları. */
export function subtreeIds(toc: TocItem[], id: string): Set<string> {
  const result = new Set([id]);
  // Liste içindekiler sırasında olduğundan üst başlık her zaman altlarından önce gelir; yine de sıraya bel bağlamamak
  // için değişiklik kalmayana kadar dolaşılır.
  let grew = true;
  while (grew) {
    grew = false;
    for (const entry of toc) {
      if (!result.has(entry.id) && entry.parentId && result.has(entry.parentId)) {
        result.add(entry.id);
        grew = true;
      }
    }
  }
  return result;
}

/** Sürüklenen başlık bu hedefe bırakılabilir mi: kendisi ve alt başlıkları hedef olamaz. */
export function canDropOn(toc: TocItem[], draggedId: string, targetId: string): boolean {
  return !subtreeIds(toc, draggedId).has(targetId);
}

/**
 * Bırakmanın sonucu: yeni üst başlık ve yer. Geçersiz hedefte (kendisi, alt başlığı, olmayan başlık) ya da başlığın
 * yeri değişmiyorsa (aynı üst başlık, kardeşler arasında aynı sıra) `null` — kayıt yapılmaz.
 */
export function resolveDrop(toc: TocItem[], draggedId: string, targetId: string, zone: DropZone): MoveTarget | null {
  const dragged = toc.find((e) => e.id === draggedId);
  const target = toc.find((e) => e.id === targetId);
  if (!dragged || !target || !canDropOn(toc, draggedId, targetId)) return null;

  let parentId: string | null;
  let index: number;
  if (zone === "inside") {
    parentId = target.id;
    index = childrenOf(toc, parentId).filter((e) => e.id !== draggedId).length;
  } else {
    parentId = parentOf(target);
    const siblings = childrenOf(toc, parentId).filter((e) => e.id !== draggedId);
    const position = siblings.findIndex((e) => e.id === target.id);
    index = zone === "before" ? position : position + 1;
  }

  const currentIndex = childrenOf(toc, parentOf(dragged)).findIndex((e) => e.id === draggedId);
  if (parentOf(dragged) === parentId && currentIndex === index) return null;
  return { parentId, index };
}

const narrow = (inherited: Visibility, own: Visibility): Visibility =>
  inherited === "private" || own === "private" ? "private" : "public";

/**
 * Taşıma başlığın etkin görünürlüğünü değiştirir mi (ders → üst başlıklar → kendisi zincirinde daralma, sunucudaki
 * kuralın aynısı). Değişmiyorsa `null`; değişiyorsa yeni görünürlük ve etkin görünürlüğü değişen başlık sayısı
 * (kendisi dahil; kendi ayarı özel olan alt dal değişmez).
 */
export function visibilityChange(
  toc: TocItem[],
  courseVisibility: Visibility,
  draggedId: string,
  newParentId: string | null,
): { to: Visibility; affected: number } | null {
  const byId = new Map(toc.map((e) => [e.id, e]));
  const dragged = byId.get(draggedId);
  if (!dragged) return null;
  const parent = newParentId ? byId.get(newParentId) : undefined;
  const inherited = parent ? parent.effectiveVisibility : courseVisibility;

  const after = new Map<string, Visibility>([[draggedId, narrow(inherited, dragged.visibility)]]);
  let affected = 0;
  for (const id of subtreeIds(toc, draggedId)) {
    const entry = byId.get(id)!;
    const value = id === draggedId ? after.get(id)! : narrow(after.get(entry.parentId!) ?? inherited, entry.visibility);
    after.set(id, value);
    if (value !== entry.effectiveVisibility) affected++;
  }
  const to = after.get(draggedId)!;
  return to === dragged.effectiveVisibility ? null : { to, affected };
}
