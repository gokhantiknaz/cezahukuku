import type { Metadata } from "next";
import { LibraryAdmin } from "./library-admin";

export const metadata: Metadata = { title: "Kütüphane" };

export default function LibraryAdminPage() {
  return <LibraryAdmin />;
}
