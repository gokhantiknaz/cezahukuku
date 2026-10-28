import { BookMarked, BookOpen, FileSearch, House, Newspaper, PenLine, type LucideIcon } from "lucide-react";

/** wide: yalnız geniş ekranda (2xl) görünür; başlık satırı daha dar ekranda iki satıra taşmasın. */
export type NavItem = { label: string; icon: LucideIcon; href?: string; highlighted?: boolean; wide?: boolean };

// href'i olmayan bölümler ilgili kilometre taşında açılacak.
export const navItems: NavItem[] = [
  // Logo da ana sayfaya gider.
  { label: "Ana Sayfa", icon: House, href: "/", wide: true },
  { label: "Kütüphane", icon: BookOpen },
  { label: "Pratik Anlatımlar", icon: BookMarked, highlighted: true },
  { label: "Atıf Denetimi", icon: FileSearch, href: "/denetle" },
  { label: "Ceza Gündemi", icon: Newspaper, href: "/gundem", highlighted: true },
];

/** Başlıkta ayrı (dolgulu) düğme olarak duran "Kendini Test Et". */
export const testItem = { label: "Kendini Test Et", icon: PenLine, href: "/test" } satisfies NavItem;
