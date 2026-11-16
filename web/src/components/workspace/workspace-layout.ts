import { useDefaultLayout } from "react-resizable-panels";

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
 * Masaüstü çalışma alanının panel genişlikleri: kullanıcının sürüklediği düzen hatırlanır. Dönen değer olduğu gibi
 * `ResizablePanelGroup`'a verilir.
 */
export function useWorkspaceLayout() {
  // Yalnız kullanıcının sürüklemesi kaydedilir: ilk hidrasyonda dar pencerede bir an kurulan masaüstü
  // düzeninin uç oranları ya da pencere boyutu değişimleri kalıcı hâle gelmesin.
  const layout = useDefaultLayout({ id: "workspace", storage: safeStorage, onlySaveAfterUserInteractions: true });
  return { defaultLayout: layout.defaultLayout, onLayoutChanged: layout.onLayoutChanged };
}
