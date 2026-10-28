import Link from "next/link";
import { Scale } from "lucide-react";
import { HeaderSearch } from "@/components/search/header-search";
import { cn } from "@/lib/utils";
import { AutoHideHeader } from "./auto-hide-header";
import { MobileMenu } from "./mobile-menu";
import { navItems, testItem, type NavItem } from "./nav-items";
import { UserMenu } from "./user-menu";

/**
 * Başlık her genişlikte tek satır. Telefonda logo, arama simgesi ve ☰ menü (bölümler ve kullanıcı sayfaları orada);
 * md'den itibaren kullanıcı düğmeleri, lg'den itibaren arama kutusu, xl'den itibaren bölüm menüsü de satırda.
 */
export function SiteHeader() {
  const TestIcon = testItem.icon;
  return (
    <AutoHideHeader className="border-b bg-card print:hidden">
      <div className="flex items-center gap-x-2 px-3 py-2 md:gap-x-4 md:px-4">
        <Link href="/" className="flex min-w-0 items-center gap-2 text-primary sm:gap-3">
          <Scale className="size-8 shrink-0 sm:size-10" strokeWidth={1.5} aria-hidden />
          <span className="min-w-0 leading-tight">
            <span className="block truncate font-serif text-lg font-bold tracking-wide sm:text-2xl">CEZA HUKUKÇUSU</span>
            <span className="hidden text-sm text-muted-foreground sm:block">Ceza Hukuku Notları</span>
          </span>
        </Link>

        {/* basis-0: satıra sığıp sığmadığına metnin tam genişliğiyle değil, kalan yerle karar verilir. */}
        <div role="search" className="ml-auto shrink-0 lg:ml-0 lg:min-w-0 lg:flex-1 lg:shrink lg:basis-0">
          <HeaderSearch />
        </div>

        <nav className="hidden items-center gap-1 xl:flex" aria-label="Ana menü">
          {navItems.map((item) => (
            <NavButton key={item.label} item={item} />
          ))}
          <Link
            href={testItem.href}
            className="ml-1 inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2.5 font-medium text-primary-foreground hover:bg-primary/90"
          >
            <TestIcon className="size-5" aria-hidden />
            {testItem.label}
          </Link>
        </nav>

        <div className="hidden md:block">
          <UserMenu />
        </div>
        <MobileMenu />
      </div>
    </AutoHideHeader>
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
