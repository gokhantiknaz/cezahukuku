import { Suspense } from "react";
import type { Metadata } from "next";
import { LawStructureAdmin } from "./law-structure-admin";

export const metadata: Metadata = { title: "Kanun yapısı" };

// Adres parametreleri ve ?madde= istemcide okunur; Suspense ön-render sırasında bunları bekletir.
export default function LawStructurePage() {
  return (
    <Suspense fallback={<p className="mt-6 text-muted-foreground">Yükleniyor…</p>}>
      <LawStructureAdmin />
    </Suspense>
  );
}
