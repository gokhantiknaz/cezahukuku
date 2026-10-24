import type { Metadata } from "next";
import { DocumentsPage } from "@/components/attachments/documents-page";

export const metadata: Metadata = { title: "Belgelerim", robots: { index: false } };

export default function Page() {
  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-6">
      <h1 className="text-3xl">Belgelerim</h1>
      <p className="mt-1 text-muted-foreground">PDF belgeleriniz ve sesli notlarınız; yalnızca siz görürsünüz.</p>
      <DocumentsPage />
    </div>
  );
}
