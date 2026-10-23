import Link from "next/link";
import { Button } from "@/components/ui/button";

export default function NotFound() {
  return (
    <div className="mx-auto max-w-xl px-6 py-16 text-center">
      <h1 className="text-3xl">Sayfa bulunamadı</h1>
      <p className="mt-3 text-muted-foreground">
        Aradığınız sayfa yok ya da yalnız giriş yapan kullanıcılara açık.
      </p>
      <div className="mt-6 flex justify-center gap-2">
        <Button asChild>
          <Link href="/">Kütüphaneye dön</Link>
        </Button>
        <Button variant="outline" asChild>
          <Link href="/giris">Giriş yap</Link>
        </Button>
      </div>
    </div>
  );
}
