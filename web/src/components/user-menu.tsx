"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { FolderOpen, LogIn, LogOut, Settings } from "lucide-react";
import { useLogout } from "@/api/generated/auth/auth";
import { Button } from "@/components/ui/button";
import { isAdmin, useCurrentUser, useRefreshCurrentUser } from "@/lib/auth";
import { canAnnotate } from "@/lib/highlights";

export function UserMenu() {
  const router = useRouter();
  const { data: user, isPending } = useCurrentUser();
  const refreshUser = useRefreshCurrentUser();
  const logout = useLogout({
    mutation: {
      onSettled: async () => {
        await refreshUser();
        router.push("/");
        router.refresh(); // private içerik ekrandan kalksın
      },
    },
  });

  if (isPending) return <div className="h-9 w-24" aria-hidden />;

  if (!user) {
    return (
      <Button variant="outline" size="lg" asChild className="border-primary text-primary">
        <Link href="/giris">
          <LogIn /> Giriş
        </Link>
      </Button>
    );
  }

  return (
    <div className="flex items-center gap-1 text-sm">
      <span className="mr-1 hidden text-muted-foreground 2xl:inline">{user.displayName}</span>
      {canAnnotate(user) && (
        <Button variant="ghost" size="lg" asChild>
          <Link href="/belgelerim" title="PDF belgelerim ve sesli notlarım" aria-label="Belgelerim">
            <FolderOpen /> <span className="hidden 2xl:inline">Belgelerim</span>
          </Link>
        </Button>
      )}
      {isAdmin(user) && (
        <Button variant="ghost" size="lg" asChild>
          <Link href="/panel/kutuphane" title="Yönetim paneli">
            <Settings /> Panel
          </Link>
        </Button>
      )}
      <Button variant="ghost" size="lg" onClick={() => logout.mutate()} disabled={logout.isPending}>
        <LogOut /> Çıkış
      </Button>
    </div>
  );
}
