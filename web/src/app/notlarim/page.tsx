import type { Metadata } from "next";
import { Notebook } from "@/components/content/notebook";

export const metadata: Metadata = { title: "Vurgu ve Notlarım", robots: { index: false } };

export default function Page() {
  return (
    <div className="mx-auto w-full max-w-3xl px-4 py-6">
      <h1 className="text-3xl">Vurgu ve Notlarım</h1>
      <p className="mt-1 text-muted-foreground">
        Bütün maddelerdeki vurgularınız ve notlarınız; birine tıklayınca maddede o yere gidilir.
      </p>
      <Notebook />
    </div>
  );
}
