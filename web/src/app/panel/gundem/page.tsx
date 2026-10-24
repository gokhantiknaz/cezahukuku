import type { Metadata } from "next";
import { NewsAdmin } from "./news-admin";

export const metadata: Metadata = { title: "Ceza Gündemi" };

export default function Page() {
  return <NewsAdmin />;
}
