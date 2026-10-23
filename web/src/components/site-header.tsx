import Link from "next/link";
import {
  BookMarked,
  BookOpen,
  House,
  Newspaper,
  PenLine,
  Scale,
  Search,
  type LucideIcon,
} from "lucide-react";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";
import { UserMenu } from "./user-menu";

type NavItem = { label: string; icon: LucideIcon; href?: string; highlighted?: boolean };

// href'i olmayan bölümler ilgili kilometre taşında açılacak.
const navItems: NavItem[] = [
  { label: "Ana Sayfa", icon: House, href: "/" },
  { label: "Kütüphane", icon: BookOpen },
  { label: "Pratik Anlatımlar", icon: BookMarked, highlighted: true },
  { label: "Ceza Gündemi", icon: Newspaper, highlighted: true },
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

        <form role="search" className="order-last w-full md:order-none md:w-auto md:flex-1" action="#">
          <label className="relative block">
            <Search className="absolute top-1/2 left-3 size-5 -translate-y-1/2 text-muted-foreground" aria-hidden />
            <Input
              type="search"
              name="q"
              disabled
              placeholder="Madde, konu, karar veya kanun ara… (arama M5'te açılacak)"
              className="h-10 bg-background pl-10 text-base"
            />
          </label>
        </form>

        <nav className="hidden items-center gap-1 xl:flex" aria-label="Ana menü">
          {navItems.map((item) => (
            <NavButton key={item.label} item={item} />
          ))}
          <span
            aria-disabled
            title="Yakında"
            className="ml-1 inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2.5 font-medium text-primary-foreground opacity-60"
          >
            <PenLine className="size-5" aria-hidden />
            Kendini Test Et
          </span>
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
      <span aria-disabled title="Yakında" className={cn(className, "opacity-50")}>
        {content}
      </span>
    );
  }
  return (
    <Link href={item.href} className={cn(className, "hover:bg-accent")}>
      {content}
    </Link>
  );
}
