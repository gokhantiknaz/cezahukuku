import type { Metadata } from "next";
import { QuizAdmin } from "./quiz-admin";

export const metadata: Metadata = { title: "Sorular" };

export default function Page() {
  return <QuizAdmin />;
}
