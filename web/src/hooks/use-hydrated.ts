import { useSyncExternalStore } from "react";

const subscribe = () => () => {};

/**
 * Hidrasyon bitene kadar false. Sunucudan akarak sonradan hidrasyona giren bileşenler, o ana kadar istemcide
 * yüklenmiş veriye (ör. oturumdaki kullanıcı) göre çizilirse sunucu HTML'iyle uyuşmaz; bu tür kısımlar
 * hidrasyondan sonra gösterilir.
 */
export function useHydrated() {
  return useSyncExternalStore(
    subscribe,
    () => true,
    () => false,
  );
}
