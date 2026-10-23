import { Suspense } from "react";
import type { Metadata } from "next";
import { LawImport } from "./law-import";

export const metadata: Metadata = { title: "Metinden içe aktar" };

export default function LawImportPage() {
  return (
    <Suspense fallback={<p className="mt-6 text-muted-foreground">Yükleniyor…</p>}>
      <LawImport />
    </Suspense>
  );
}
