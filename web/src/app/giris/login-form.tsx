"use client";

import { useRouter } from "next/navigation";
import { ApiError } from "@/api/fetcher";
import { useLogin } from "@/api/generated/auth/auth";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useRefreshCurrentUser } from "@/lib/auth";

export function LoginForm() {
  const router = useRouter();
  const refreshUser = useRefreshCurrentUser();
  const login = useLogin({
    mutation: {
      onSuccess: async () => {
        await refreshUser();
        router.push("/");
      },
    },
  });

  function handleSubmit(formData: FormData) {
    login.mutate({
      data: {
        email: String(formData.get("email") ?? ""),
        password: String(formData.get("password") ?? ""),
        rememberMe: formData.get("rememberMe") === "on",
      },
    });
  }

  const error = login.error instanceof ApiError ? login.error.message : login.error ? "Giriş yapılamadı." : null;

  return (
    <form action={handleSubmit} className="mt-6 space-y-4">
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

      <Button type="submit" size="lg" disabled={login.isPending} className="w-full">
        {login.isPending ? "Giriş yapılıyor…" : "Giriş yap"}
      </Button>
    </form>
  );
}
