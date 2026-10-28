import { Suspense } from "react";
import type { Metadata } from "next";
import { Noto_Serif, Roboto_Condensed, Source_Sans_3 } from "next/font/google";
import { NavigationProgress } from "@/components/navigation-progress";
import { NewsTicker } from "@/components/news-ticker";
import { SiteHeader } from "@/components/site-header";
import { cn } from "@/lib/utils";
import { Providers } from "./providers";
import "./globals.css";

// Türkçe karakterler (ş, ğ, ı, İ) latin-ext alt kümesinde.
const uiFont = Roboto_Condensed({
  variable: "--font-ui",
  subsets: ["latin", "latin-ext"],
});

const bodyFont = Source_Sans_3({
  variable: "--font-body",
  subsets: ["latin", "latin-ext"],
});

const logoFont = Noto_Serif({
  variable: "--font-logo",
  subsets: ["latin", "latin-ext"],
  weight: ["700"],
});

export const metadata: Metadata = {
  title: {
    default: "Ceza Hukukçusu — Ceza Hukuku Notları",
    template: "%s | Ceza Hukukçusu",
  },
  description: "Ceza hukuku mevzuatı, gerekçeler, içtihatlar ve notlar.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="tr" className={cn("h-full antialiased", uiFont.variable, bodyFont.variable, logoFont.variable)}>
      {/* Geniş ekranda sayfa ekran yüksekliğine sabit: çalışma alanındaki paneller kendi içinde kayar. Dar ekranda
          sayfanın tamamı kayar: tarayıcının adres çubuğu ve başlık kaydırınca çekilir, okuma alanı büyür. */}
      <body className="flex min-h-dvh flex-col lg:h-dvh print:h-auto">
        <Providers>
          <Suspense fallback={null}>
            <NavigationProgress />
          </Suspense>
          <SiteHeader />
          <main className="flex flex-1 flex-col lg:min-h-0 lg:overflow-auto print:overflow-visible">{children}</main>
          <NewsTicker />
        </Providers>
      </body>
    </html>
  );
}
