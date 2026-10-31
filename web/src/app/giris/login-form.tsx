"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { ApiError } from "@/api/fetcher";
import { useLogin } from "@/api/generated/auth/auth";
import { LoadingOverlay } from "@/components/scales-loader";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useRefreshCurrentUser } from "@/lib/auth";

/** Giriş katmanı bilerek en az bu kadar görünür (terazi animasyonu fark edilsin; hatalı şifrede de aynı süre). */
const MIN_LOGIN_MS = 1800;

const wait = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

export function LoginForm() {
  const router = useRouter();
  const refreshUser = useRefreshCurrentUser();
  const login = useLogin();
  // İstek sürerken ve başarılı girişten sonra yönlendirme bitene kadar (form o zaman kaldırılır) açık.
  const [busy, setBusy] = useState(false);

  // onSubmit (form action değil): hatalı girişte React formu sıfırlamasın, e-posta yazılı kalsın.
  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const formData = new FormData(event.currentTarget);
    setBusy(true);
    const [result] = await Promise.allSettled([
      login.mutateAsync({
        data: {
          email: String(formData.get("email") ?? ""),
          password: String(formData.get("password") ?? ""),
          rememberMe: formData.get("rememberMe") === "on",
        },
      }),
      wait(MIN_LOGIN_MS),
    ]);
    if (result.status === "rejected") {
      setBusy(false);
      return;
    }
    await refreshUser();
    // Oturum süresi dolunca kalınan sayfaya dönülür (yalnız site içi yol: "//başka-site" değil).
    const next = new URLSearchParams(window.location.search).get("next");
    router.push(next?.startsWith("/") && !next.startsWith("//") && !next.startsWith("/giris") ? next : "/");
    // Sunucuda çizilen içerik oturuma bağlı (private kanunlar); önbellekteki anonim hâli yenilensin.
    router.refresh();
  }

  const error = busy
    ? null
    : login.error instanceof ApiError
      ? login.error.message
      : login.error
        ? "Giriş yapılamadı."
        : null;

  return (
    <form onSubmit={handleSubmit} className="mt-6 space-y-4">
      {busy && <LoadingOverlay title="Giriş yapılıyor…" />}
      <div className="space-y-1.5">
        <Label htmlFor="email">E-posta</Label>
        <Input id="email" name="email" type="email" required autoComplete="username" />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="password">Şifre</Label>
        <Input id="password" name="password" type="password" required autoComplete="current-password" />
      </div>
      <div className="flex items-center gap-2">
        <Checkbox id="rememberMe" name="rememberMe" />
        <Label htmlFor="rememberMe" className="font-normal">
          Beni hatırla
        </Label>
      </div>

      {error && (
        <p role="alert" className="rounded-md bg-accent px-3 py-2 text-sm text-accent-foreground">
          {error}
        </p>
      )}

      <Button type="submit" size="lg" disabled={busy} className="w-full">
        {busy ? "Giriş yapılıyor…" : "Giriş yap"}
      </Button>
    </form>
  );
}
