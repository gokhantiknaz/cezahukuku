"use client";

import { useEffect, useRef } from "react";
import { useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { getGetCurrentUserQueryKey } from "@/api/generated/auth/auth";
import { useCurrentUser } from "@/lib/auth";

/** Kullanıcı sayfada bir şey yaptıkça oturum en çok bu sıklıkla yenilenir (API'deki en kısa oturum 10 dk). */
const HEARTBEAT_MS = 5 * 60_000;
const ACTIVITY_EVENTS = ["pointerdown", "keydown", "wheel", "touchstart"] as const;
const EXPIRED_TOAST = "session-expired";

let signingOut = false;

/** Kullanıcı kendisi çıkış yapıyor: "süresi doldu" uyarısı gösterilmesin. */
export function markSigningOut() {
  signingOut = true;
}

/**
 * Oturum hareketsizlikle kapanır (API: Auth:IdleTimeoutMinutes). Sunucuda çizilen sayfalar çerezi yenileyemez (API'nin
 * yenilediği çerez tarayıcıya ulaşmaz); bu yüzden kullanıcı sayfada bir şey yaptıkça ara sıra tarayıcıdan oturum
 * sorulur. Çerez böylece yenilenir; süresi dolmuşsa kullanıcıya söylenir, panelde giriş sayfasına gidilir.
 */
export function SessionKeeper() {
  const { data: user } = useCurrentUser();
  const queryClient = useQueryClient();
  const router = useRouter();
  const signedIn = useRef(false);
  const lastCheck = useRef(0);

  useEffect(() => {
    if (!user) return;
    lastCheck.current = Date.now();
    const onActivity = () => {
      if (Date.now() - lastCheck.current < HEARTBEAT_MS) return;
      lastCheck.current = Date.now();
      void queryClient.invalidateQueries({ queryKey: getGetCurrentUserQueryKey() });
    };
    for (const name of ACTIVITY_EVENTS) window.addEventListener(name, onActivity, { passive: true });
    return () => {
      for (const name of ACTIVITY_EVENTS) window.removeEventListener(name, onActivity);
    };
  }, [user, queryClient]);

  useEffect(() => {
    if (user === undefined) return;
    const wasSignedIn = signedIn.current;
    signedIn.current = user !== null;
    if (user !== null) {
      toast.dismiss(EXPIRED_TOAST);
      return;
    }
    if (!wasSignedIn) return;
    if (signingOut) {
      signingOut = false;
      return;
    }

    const here = window.location.pathname + window.location.search;
    const loginUrl = `/giris?next=${encodeURIComponent(here)}`;
    const toPanelLogin = window.location.pathname.startsWith("/panel");
    toast.warning("Oturumunuzun süresi doldu. Devam etmek için yeniden giriş yapın.", {
      id: EXPIRED_TOAST,
      duration: Infinity,
      action: toPanelLogin ? undefined : { label: "Giriş yap", onClick: () => router.push(loginUrl) },
    });
    if (toPanelLogin) router.push(loginUrl);
    else router.refresh(); // private içerik ekrandan kalksın
  }, [user, router]);

  return null;
}
