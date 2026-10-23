import { Suspense } from "react";
import type { Metadata } from "next";
import { AdminGate, PanelNav } from "@/components/panel/panel-nav";

export const metadata: Metadata = {
  title: { default: "Yönetim", template: "%s | Yönetim | Ceza Hukukçusu" },
  robots: { index: false },
};

export default function PanelLayout({ children }: LayoutProps<"/panel">) {
  return (
    <div className="mx-auto w-full max-w-7xl p-4">
      <Suspense fallback={<div className="h-11 border-b" />}>
        <PanelNav />
      </Suspense>
      <AdminGate>{children}</AdminGate>
    </div>
  );
}
