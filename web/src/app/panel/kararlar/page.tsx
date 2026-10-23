import type { Metadata } from "next";
import { DecisionsAdmin } from "./decisions-admin";

export const metadata: Metadata = { title: "Kararlar" };

export default function DecisionsPage() {
  return <DecisionsAdmin />;
}
