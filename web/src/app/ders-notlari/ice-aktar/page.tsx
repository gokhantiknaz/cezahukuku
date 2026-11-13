import type { Metadata } from "next";
import { LectureImport } from "@/components/content/lecture-import";

export const metadata: Metadata = { title: "Başlıkları içe aktar", robots: { index: false } };

export default function Page() {
  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-6">
      <h1 className="text-3xl">Başlıkları içe aktar</h1>
      <LectureImport />
    </div>
  );
}
