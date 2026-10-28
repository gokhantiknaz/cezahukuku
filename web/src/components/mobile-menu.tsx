"use client";

import { useState } from "react";
import Link from "next/link";
import { FolderOpen, LogIn, LogOut, Menu, Settings, Star, type LucideIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { isAdmin, useCurrentUser } from "@/lib/auth";
import { canAnnotate } from "@/lib/highlights";
import { navItems, testItem } from "./nav-items";
import { useSignOut } from "./user-menu";

type MenuLink = { label: string; icon: LucideIcon; href: string };

/**
 * Başlıktaki menü sığmayan ekranlarda (xl altı) ☰ düğmesi: bölümler ve kullanıcının kendi sayfaları kenardan açılan
 * çekmecede. Henüz açılmamış ("Yakında") bölümler gösterilmez.
 */
export function MobileMenu() {
  const [open, setOpen] = useState(false);
  const { data: user } = useCurrentUser();
  const signOut = useSignOut();

  const sections: MenuLink[] = [
    ...navItems.filter((item): item is MenuLink => item.href !== undefined),
    testItem,
  ];
  const personal: MenuLink[] = [
    ...(user && canAnnotate(user)
      ? [
          { label: "Favorilerim", icon: Star, href: "/favoriler" },
          { label: "Belgelerim", icon: FolderOpen, href: "/belgelerim" },
        ]
      : []),
    ...(user && isAdmin(user) ? [{ label: "Yönetim paneli", icon: Settings, href: "/panel/kutuphane" }] : []),
  ];

  const row = (item: MenuLink) => {
    const Icon = item.icon;
    // Başlık her sayfada önceden hazırlanır (adres okunmaz); açık sayfa işaretlenmez, menü dokununca kapanır.
    return (
      <li key={item.href}>
        <Link href={item.href} onClick={() => setOpen(false)} className="flex items-center gap-3 px-4 py-3 text-base hover:bg-accent">
          <Icon className="size-5 shrink-0 text-primary" aria-hidden />
          {item.label}
        </Link>
      </li>
    );
  };

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger asChild>
        <Button variant="ghost" size="icon-lg" className="xl:hidden" aria-label="Menü" title="Menü">
          <Menu className="size-6" />
        </Button>
      </SheetTrigger>
      <SheetContent side="right" className="w-[85vw] max-w-xs gap-0 overflow-y-auto p-0">
        <SheetTitle className="bg-primary px-4 py-3 text-lg font-normal text-primary-foreground">Menü</SheetTitle>
        <nav aria-label="Bölümler">
          <ul className="py-2">{sections.map(row)}</ul>
        </nav>
        <div className="border-t py-2">
          {user ? (
            <>
              <p className="px-4 pt-1 pb-2 text-sm text-muted-foreground">{user.displayName}</p>
              <ul>{personal.map(row)}</ul>
              <button
                type="button"
                onClick={() => signOut.mutate()}
                disabled={signOut.isPending}
                className="flex w-full items-center gap-3 px-4 py-3 text-left text-base hover:bg-accent"
              >
                <LogOut className="size-5 shrink-0 text-primary" aria-hidden />
                Çıkış
              </button>
            </>
          ) : (
            <div className="px-4 py-2">
              <Button asChild size="lg" className="h-11 w-full text-base">
                <Link href="/giris" onClick={() => setOpen(false)}>
                  <LogIn /> Giriş
                </Link>
              </Button>
            </div>
          )}
        </div>
      </SheetContent>
    </Sheet>
  );
}
