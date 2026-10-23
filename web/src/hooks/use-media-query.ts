"use client";

import { useSyncExternalStore } from "react";

/**
 * CSS medya sorgusunu izler. Sunucuda `serverDefault` döner; masaüstü düzeni varsayılan olduğundan
 * geniş ekranlarda ilk boyamada yerleşim kaymaz.
 */
export function useMediaQuery(query: string, serverDefault = true) {
  return useSyncExternalStore(
    (onChange) => {
      const mql = window.matchMedia(query);
      mql.addEventListener("change", onChange);
      return () => mql.removeEventListener("change", onChange);
    },
    () => window.matchMedia(query).matches,
    () => serverDefault,
  );
}
