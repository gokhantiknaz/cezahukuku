import Link from "next/link";

// Kanun açıkken bulunamayan madde: çalışma alanı (sol ağaç) yerinde kalır, yalnız orta alanda gösterilir.
export default function ArticleNotFound() {
  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      <h1 className="text-3xl">Madde bulunamadı</h1>
      <p className="reading mt-3 text-muted-foreground">
        Bu madde yok ya da yalnız giriş yapan kullanıcılara açık. Soldaki ağaçtan başka bir madde seçebilir veya{" "}
        <Link href="/giris" className="text-primary underline">
          giriş yapabilirsiniz
        </Link>
        .
      </p>
    </div>
  );
}
