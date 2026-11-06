import { FileSearch, GraduationCap, House, Newspaper, PenLine, type LucideIcon } from "lucide-react";

/** wide: yalnız geniş ekranda (2xl) görünür; başlık satırı daha dar ekranda iki satıra taşmasın. */
export type NavItem = { label: string; icon: LucideIcon; href: string; highlighted?: boolean; wide?: boolean };

// Kütüphane (kaynak PDF'leri) ve Pratik Anlatımlar içerikleri kararlaşınca eklenecek.
export const navItems: NavItem[] = [
  // Logo da ana sayfaya gider.
  { label: "Ana Sayfa", icon: House, href: "/", wide: true },
  { label: "Ders Notları", icon: GraduationCap, href: "/ders-notlari" },
  { label: "Atıf Denetimi", icon: FileSearch, href: "/denetle" },
  { label: "Ceza Gündemi", icon: Newspaper, href: "/gundem", highlighted: true },
];

/** Başlıkta ayrı (dolgulu) düğme olarak duran "Kendini Test Et". */
export const testItem = { label: "Kendini Test Et", icon: PenLine, href: "/test" } satisfies NavItem;
