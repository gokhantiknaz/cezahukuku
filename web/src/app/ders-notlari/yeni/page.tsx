import type { Metadata } from "next";
import { NewLectureNote } from "@/components/content/lecture-note-form";

export const metadata: Metadata = { title: "Yeni ders notu", robots: { index: false } };

export default function Page() {
  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-6">
      <h1 className="text-3xl">Yeni ders notu</h1>
      <NewLectureNote />
    </div>
  );
}
