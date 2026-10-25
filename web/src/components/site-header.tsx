import Link from "next/link";
import {
  BookMarked,
  BookOpen,
  FileSearch,
  House,
  Newspaper,
  PenLine,
  Scale,
  type LucideIcon,
} from "lucide-react";
import { HeaderSearch } from "@/components/search/header-search";
import { cn } from "@/lib/utils";
import { UserMenu } from "./user-menu";

/** wide: yalnız geniş ekranda (2xl) görünür; başlık satırı daha dar ekranda iki satıra taşmasın. */
type NavItem = { label: string; icon: LucideIcon; href?: string; highlighted?: boolean; wide?: boolean };

// href'i olmayan bölümler ilgili kilometre taşında açılacak.
const navItems: NavItem[] = [
  // Logo da ana sayfaya gider.
  { label: "Ana Sayfa", icon: House, href: "/", wide: true },
  { label: "Kütüphane", icon: BookOpen },
  { label: "Pratik Anlatımlar", icon: BookMarked, highlighted: true },
  { label: "Atıf Denetimi", icon: FileSearch, href: "/denetle" },
  { label: "Ceza Gündemi", icon: Newspaper, href: "/gundem", highlighted: true },
];

export function SiteHeader() {
  return (
    <header className="border-b bg-card print:hidden">
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-2">
        <Link href="/" className="flex items-center gap-3 text-primary">
          <Scale className="size-10" strokeWidth={1.5} aria-hidden />
          <span className="leading-tight">
            <span className="block font-serif text-xl font-bold tracking-wide sm:text-2xl">CEZA HUKUKÇUSU</span>
            <span className="block text-sm text-muted-foreground">Ceza Hukuku Notları</span>
          </span>
        </Link>

        {/* basis-0: satıra sığıp sığmadığına metnin tam genişliğiyle değil, kalan yerle karar verilir. */}
        <div role="search" className="order-last w-full md:order-none md:w-auto md:min-w-0 md:flex-1 md:basis-0">
          <HeaderSearch />
        </div>

        <nav className="hidden items-center gap-1 xl:flex" aria-label="Ana menü">
          {navItems.map((item) => (
            <NavButton key={item.label} item={item} />
          ))}
          <Link
            href="/test"
            className="ml-1 inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2.5 font-medium text-primary-foreground hover:bg-primary/90"
          >
            <PenLine className="size-5" aria-hidden />
            Kendini Test Et
          </Link>
        </nav>

        <div className="ml-auto md:ml-0">
          <UserMenu />
        </div>
      </div>
    </header>
  );
}

function NavButton({ item }: { item: NavItem }) {
  const Icon = item.icon;
  const className = cn(
    "flex flex-col items-center gap-0.5 rounded-lg px-3 py-1.5 text-sm",
    item.highlighted ? "bg-accent text-accent-foreground" : "text-foreground",
  );
  const content = (
    <>
      <Icon className="size-6" strokeWidth={1.5} aria-hidden />
      {item.label}
    </>
  );

  if (!item.href) {
    return (
      // Henüz açılmamış bölümler başlık satırına sığmayan genişlikte gizlenir (başlık iki satıra taşmasın).
      <span aria-disabled title="Yakında" className={cn(className, "hidden opacity-50 2xl:flex")}>
        {content}
      </span>
    );
  }
  return (
    <Link href={item.href} className={cn(className, "hover:bg-accent", item.wide && "hidden 2xl:flex")}>
      {content}
    </Link>
  );
}
