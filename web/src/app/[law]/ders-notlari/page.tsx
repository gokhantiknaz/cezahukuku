import type { Metadata } from "next";
import { LawNotesPage } from "@/components/workspace/law-notes-page";

export const metadata: Metadata = { title: "Ders Notları" };

export default function LectureNotesPage({ params }: PageProps<"/[law]/ders-notlari">) {
  return <LawNotesPage params={params} kind="dersNotu" />;
}
