import { describe, test } from "node:test";
import assert from "node:assert/strict";
import { canDropOn, resolveDrop, subtreeIds, type TocItem, visibilityChange } from "./lecture-moves.ts";

// Ağaç (içindekiler sırası):
// A            1. Bölüm
//   A1         1.1
//   A2         1.2
//     A21      1.2.1
// B            2. Bölüm
//   B1         2.1
// C            3. Bölüm
const item = (id: string, parentId: string | null, visibility: "public" | "private" = "public", effective = visibility): TocItem => ({
  id,
  parentId,
  visibility,
  effectiveVisibility: effective,
});
const toc: TocItem[] = [
  item("A", null),
  item("A1", "A"),
  item("A2", "A"),
  item("A21", "A2"),
  item("B", null),
  item("B1", "B"),
  item("C", null),
];

describe("lecture-moves", () => {
  test("resolveDrop places before, after and inside", () => {
    // A21'i B1'in önüne, arkasına ve içine.
    assert.deepEqual(resolveDrop(toc, "A21", "B1", "before"), { parentId: "B", index: 0 });
    assert.deepEqual(resolveDrop(toc, "A21", "B1", "after"), { parentId: "B", index: 1 });
    assert.deepEqual(resolveDrop(toc, "A21", "B1", "inside"), { parentId: "B1", index: 0 });
    // Alt başlıkları olan başlığın içine: sona.
    assert.deepEqual(resolveDrop(toc, "C", "A", "inside"), { parentId: "A", index: 2 });
    // Aynı düzeyde aşağı: A1'i A2'nin arkasına (A1 hariç kardeşler [A2] → 1).
    assert.deepEqual(resolveDrop(toc, "A1", "A2", "after"), { parentId: "A", index: 1 });
    // Aynı düzeyde yukarı: A2'yi A1'in önüne.
    assert.deepEqual(resolveDrop(toc, "A2", "A1", "before"), { parentId: "A", index: 0 });
  });

  test("resolveDrop at first level moves to root", () => {
    assert.deepEqual(resolveDrop(toc, "A21", "B", "before"), { parentId: null, index: 1 });
    assert.deepEqual(resolveDrop(toc, "A21", "C", "after"), { parentId: null, index: 3 });
    assert.deepEqual(resolveDrop(toc, "C", "A", "before"), { parentId: null, index: 0 });
  });

  test("resolveDrop rejects own subtree", () => {
    assert.equal(resolveDrop(toc, "A", "A", "inside"), null);
    assert.equal(resolveDrop(toc, "A", "A2", "inside"), null);
    assert.equal(resolveDrop(toc, "A", "A21", "before"), null);
    assert.equal(canDropOn(toc, "A2", "A21"), false);
    assert.equal(canDropOn(toc, "A21", "A2"), true);
    assert.deepEqual([...subtreeIds(toc, "A")].sort(), ["A", "A1", "A2", "A21"]);
    assert.equal(resolveDrop(toc, "A", "yok", "after"), null);
  });

  test("resolveDrop returns null for same place", () => {
    // Kendi hemen altındaki çizgi (bir sonraki kardeşin önü) ve bir önceki kardeşin arkası: yer değişmez.
    assert.equal(resolveDrop(toc, "A1", "A2", "before"), null);
    assert.equal(resolveDrop(toc, "A2", "A1", "after"), null);
    assert.equal(resolveDrop(toc, "B", "A", "after"), null);
    assert.equal(resolveDrop(toc, "B", "C", "before"), null);
    // Zaten son alt başlıkken üst başlığının içine bırakma.
    assert.equal(resolveDrop(toc, "A2", "A", "inside"), null);
    assert.equal(resolveDrop(toc, "B1", "B", "inside"), null);
  });

  test("visibilityChange counts affected headings", () => {
    const withPrivate: TocItem[] = [
      item("A", null),
      item("A1", "A"),
      item("A11", "A1", "private"),
      item("A12", "A1"),
      item("P", null, "private"),
      item("P1", "P", "public", "private"),
    ];
    // Açık A1 (altında açık A12 ve zaten özel A11) özel P'nin içine: A1 ve A12 özel olur, A11 değişmez.
    assert.deepEqual(visibilityChange(withPrivate, "public", "A1", "P"), { to: "private", affected: 2 });
    // Özel üstün altından birinci düzeye: P1 açılır.
    assert.deepEqual(visibilityChange(withPrivate, "public", "P1", null), { to: "public", affected: 1 });
    // Görünürlük değişmeyen taşıma: onay yok.
    assert.equal(visibilityChange(withPrivate, "public", "A12", null), null);
    assert.equal(visibilityChange(withPrivate, "public", "A11", "P"), null);
    // Özel derste her şey zaten özel: birinci düzeye taşıma da değiştirmez.
    const privateCourse = withPrivate.map((e) => ({ ...e, effectiveVisibility: "private" as const }));
    assert.equal(visibilityChange(privateCourse, "private", "A1", null), null);
  });
});
