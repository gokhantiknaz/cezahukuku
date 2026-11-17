import { useCallback, useLayoutEffect, useRef } from "react";
import { useGroupRef, type Layout, type LayoutChangedMeta } from "react-resizable-panels";

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

/** Kayıt anahtarı ve biçimi kütüphanenin `useDefaultLayout({ id: "workspace" })` ile aynı: eski kayıtlar geçerli. */
export const WORKSPACE_LAYOUT_KEY = "react-resizable-panels:workspace";

/** Kayıtlı düzeni okur: panel kimliği → yüzde. Bozuk ya da beklenmeyen kayıt yok sayılır. */
export function parseSavedLayout(raw: string | null): Layout | undefined {
  if (!raw) return undefined;
  try {
    const value: unknown = JSON.parse(raw);
    if (typeof value !== "object" || value === null || Array.isArray(value)) return undefined;
    const entries = Object.entries(value);
    if (entries.length === 0 || !entries.every(([, size]) => typeof size === "number" && Number.isFinite(size)))
      return undefined;
    return Object.fromEntries(entries) as Layout;
  } catch {
    return undefined;
  }
}

function samePanels(a: Layout, b: Layout) {
  const keys = Object.keys(a);
  return keys.length === Object.keys(b).length && keys.every((key) => key in b);
}

/**
 * Masaüstü çalışma alanının panel genişlikleri: kullanıcının sürüklediği düzen hatırlanır. Dönen değer olduğu gibi
 * `ResizablePanelGroup`'a verilir.
 *
 * Kayıtlı düzen ilk çizimde kullanılmaz (bug 1): sunucuda localStorage yoktur, hidrasyon çizimi sunucununkiyle aynı
 * olmalıdır. Grup kurulduktan sonra, boyamadan önce (layout effect) kayıtlı düzen grubun kendisine uygulanır.
 */
export function useWorkspaceLayout() {
  const groupRef = useGroupRef();
  // Kayıt grup başına bir kez uygulanır. cacheComponents ziyaret edilen sayfayı gizleyip geri gösterir; gösterilince
  // effect yeniden çalışır ve o anki düzen (ör. düğmeyle kapatılan not paneli) kayıtla ezilmemelidir. Ref gizlenip
  // gösterilmede korunur.
  const savedLayoutApplied = useRef(false);

  useLayoutEffect(() => {
    const group = groupRef.current;
    if (savedLayoutApplied.current || !group) return;
    const current = group.getLayout();
    // Grup henüz ölçülmediyse (boş düzen) karar verilmez.
    if (Object.keys(current).length === 0) return;
    savedLayoutApplied.current = true;
    const saved = parseSavedLayout(safeStorage.getItem(WORKSPACE_LAYOUT_KEY));
    // Panel kümesi değiştiyse (eski kayıt) kayıt uygulanmaz, varsayılanlar kalır.
    if (saved && samePanels(saved, current)) group.setLayout(saved);
  }, [groupRef]);

  // Yalnız kullanıcının sürüklemesi kaydedilir: ilk hidrasyonda dar pencerede bir an kurulan masaüstü
  // düzeninin uç oranları, pencere boyutu değişimleri ya da kayıtlı düzenin uygulanması kalıcı hâle gelmesin.
  const onLayoutChanged = useCallback((layout: Layout, meta: LayoutChangedMeta) => {
    if (meta.isUserInteraction)
      safeStorage.setItem(WORKSPACE_LAYOUT_KEY, JSON.stringify(meta.requestedLayout ?? layout));
  }, []);

  return { groupRef, onLayoutChanged };
}
