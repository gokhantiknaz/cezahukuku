import type { NextConfig } from "next";

// .NET API adresi. Tarayıcı /api/* isteklerini Next.js'e yapar, Next.js bunları API'ye iletir:
// aynı origin olduğu için kimlik doğrulama cookie'si taşınır ve CORS gerekmez.
const apiUrl = process.env.API_URL ?? "http://localhost:5080";

const nextConfig: NextConfig = {
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
