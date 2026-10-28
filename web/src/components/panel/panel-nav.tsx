"use client";

import type { ReactNode } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { isAdmin, useCurrentUser } from "@/lib/auth";
import { cn } from "@/lib/utils";

const sections = [
  { href: "/panel/kutuphane", label: "Kütüphane" },
  { href: "/panel/kararlar", label: "Kararlar" },
  { href: "/panel/gundem", label: "Ceza Gündemi" },
  { href: "/panel/sorular", label: "Sorular" },
  { href: "/panel/kullanicilar", label: "Kullanıcılar" },
];

export function PanelNav() {
  const pathname = usePathname();
  return (
    // Dar ekranda tek satır, yana kaydırılır.
    <nav className="flex gap-1 overflow-x-auto border-b" aria-label="Yönetim paneli">
      {sections.map((section) => {
        const active = pathname.startsWith(section.href);
        return (
          <Link
            key={section.href}
            href={section.href}
            aria-current={active ? "page" : undefined}
            className={cn(
              "-mb-px shrink-0 border-b-2 px-3 py-2 text-base whitespace-nowrap",
              active ? "border-primary text-primary" : "border-transparent text-muted-foreground hover:text-foreground",
            )}
          >
            {section.label}
          </Link>
        );
      })}
    </nav>
  );
}

/**
 * Panel yalnız super_admin içindir. Asıl koruma API'dedir (yönetim uç noktaları 401/403 döner); bu kapı yalnız
 * yetkisiz kullanıcıya anlamlı bir mesaj gösterir.
 */
export function AdminGate({ children }: { children: ReactNode }) {
  const { data: user, isPending } = useCurrentUser();

  if (isPending) return <p className="mt-6 text-muted-foreground">Yükleniyor…</p>;
  if (!user) {
    return (
      <p className="mt-6">
        Bu sayfa için{" "}
        <Link href="/giris" className="text-primary underline">
          giriş yapın
        </Link>
        .
      </p>
    );
  }
  if (!isAdmin(user)) return <p className="mt-6">Bu sayfa yalnızca sistem yöneticisine açıktır.</p>;
  return children;
}
