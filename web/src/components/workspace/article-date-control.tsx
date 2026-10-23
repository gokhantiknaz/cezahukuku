"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { CalendarSearch } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

/**
 * "Suç tarihindeki metin": seçilen gün yürürlükte olan metni gösterir (TCK 7 — lehe kanun karşılaştırması için).
 * Tarih adreste durur (?tarih=2010-03-15); bağlantı paylaşılabilir, yazdırılabilir.
 * Düz bir GET formudur ve alan kontrolsüzdür: madde sunucudan akarak sonradan hidrasyona girer; o arada yazılan
 * tarih silinmez, gönderilirse tarayıcı ?tarih= adresine kendisi gider. Hidrasyondan sonra istemci içinde geçilir.
 */
export function ArticleDateControl({ asOf }: { asOf?: string }) {
  const router = useRouter();
  const pathname = usePathname();

  function show(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const value = String(new FormData(event.currentTarget).get("tarih") ?? "");
    router.push(value ? `${pathname}?tarih=${value}` : pathname);
  }

  return (
    <form method="get" onSubmit={show} className="flex flex-wrap items-end gap-2 print:hidden">
      <div>
        <Label htmlFor="article-as-of" className="mb-1.5 text-sm text-muted-foreground">
          Suç / olay tarihi
        </Label>
        <Input id="article-as-of" name="tarih" type="date" defaultValue={asOf ?? ""} className="w-44" />
      </div>
      <Button type="submit" variant="outline">
        <CalendarSearch /> O tarihteki metni göster
      </Button>
      {asOf && (
        <Button variant="ghost" asChild>
          <Link href={pathname}>Güncel metne dön</Link>
        </Button>
      )}
    </form>
  );
}
