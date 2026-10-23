"use client";

import type { ReactNode } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ApiError } from "@/api/fetcher";
import { Toaster } from "@/components/ui/sonner";
import { TooltipProvider } from "@/components/ui/tooltip";

let browserQueryClient: QueryClient | undefined;

function makeQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        // 401/403/404 tekrar denemekle düzelmez.
        retry: (failureCount, error) =>
          !(error instanceof ApiError && error.status < 500) && failureCount < 2,
      },
    },
  });
}

function getQueryClient() {
  // Sunucuda her render için ayrı istemci, tarayıcıda tek istemci.
  if (typeof window === "undefined") return makeQueryClient();
  browserQueryClient ??= makeQueryClient();
  return browserQueryClient;
}

export function Providers({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={getQueryClient()}>
      <TooltipProvider>
        {children}
        {/* Alttaki haber şeridinin (≈44 px) üstünde dursun. */}
        <Toaster position="bottom-right" offset={{ bottom: 64, right: 24 }} mobileOffset={{ bottom: 60 }} />
      </TooltipProvider>
    </QueryClientProvider>
  );
}
