import type { NextConfig } from "next";

// .NET API adresi. Tarayıcı /api/* isteklerini Next.js'e yapar, Next.js bunları API'ye iletir:
// aynı origin olduğu için kimlik doğrulama cookie'si taşınır ve CORS gerekmez.
const apiUrl = process.env.API_URL ?? "http://localhost:5080";

const nextConfig: NextConfig = {
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
