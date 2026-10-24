import type { Metadata } from "next";
import { FavoritesList } from "@/components/content/favorites-list";

export const metadata: Metadata = { title: "Favorilerim", robots: { index: false } };

export default function Page() {
  return (
    <div className="mx-auto w-full max-w-3xl px-4 py-6">
      <h1 className="text-3xl">Favorilerim</h1>
      <p className="mt-1 text-muted-foreground">Sık baktığınız maddeler; madde sayfasındaki “Favorilere ekle” ile eklenir.</p>
      <FavoritesList />
    </div>
  );
}
