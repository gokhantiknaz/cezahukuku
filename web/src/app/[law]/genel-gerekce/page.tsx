import type { Metadata } from "next";
import { LawNotesPage } from "@/components/workspace/law-notes-page";

export const metadata: Metadata = { title: "Genel Gerekçe" };

export default function GeneralReasoningPage({ params }: PageProps<"/[law]/genel-gerekce">) {
  return <LawNotesPage params={params} kind="genelGerekce" />;
}
