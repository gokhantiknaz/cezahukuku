import type { NextConfig } from "next";

// .NET API adresi. Tarayıcı /api/* isteklerini Next.js'e yapar, Next.js bunları API'ye iletir:
// aynı origin olduğu için kimlik doğrulama cookie'si taşınır ve CORS gerekmez.
const apiUrl = process.env.API_URL ?? "http://localhost:5080";

// Tüm yanıtlarda (sayfalar ve /api): başka sitenin çerçevesinde açılmaz (clickjacking), tür tahmini yok, mikrofon
// yalnız bu sitede (sesli not). CSP şimdilik betikleri kısıtlamaz (Next.js satır içi betikleri nonce ister); çerçeve,
// <base>, eklenti ve form hedefini kısıtlar.
const securityHeaders = [
  { key: "X-Frame-Options", value: "DENY" },
  { key: "Content-Security-Policy", value: "frame-ancestors 'none'; base-uri 'self'; object-src 'none'; form-action 'self'" },
  { key: "X-Content-Type-Options", value: "nosniff" },
  { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
  { key: "Permissions-Policy", value: "microphone=(self), camera=(), geolocation=(), payment=(), usb=()" },
];

const nextConfig: NextConfig = {
  poweredByHeader: false,
  async headers() {
    return [{ source: "/:path*", headers: securityHeaders }];
  },
  // Geliştirme sunucusu localhost dışından gelen istekleri (sayfanın JS dosyaları dahil) varsayılan olarak engeller;
  // telefondan yerel ağ adresiyle (http://192.168.x.x:3000) denenebilsin. Yalnız `next dev`i etkiler.
  allowedDevOrigins: ["192.168.*.*", "10.*.*.*"],
  cacheComponents: true,
  partialPrefetching: true,
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${apiUrl}/api/:path*` }];
  },
};

export default nextConfig;
