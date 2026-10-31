"use client";

import type { ReactNode } from "react";
import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ApiError } from "@/api/fetcher";
import { getGetCurrentUserQueryKey } from "@/api/generated/auth/auth";
import { SessionKeeper } from "@/components/session-keeper";
import { Toaster } from "@/components/ui/sonner";
import { TooltipProvider } from "@/components/ui/tooltip";

let browserQueryClient: QueryClient | undefined;

function makeQueryClient() {
  // Herhangi bir istek 401 dönerse oturum düşmüştür: oturum bilgisi yenilensin (SessionKeeper kullanıcıya söyler).
  const onError = (error: Error) => {
    if (error instanceof ApiError && error.status === 401)
      void client.invalidateQueries({ queryKey: getGetCurrentUserQueryKey() });
  };
  const client: QueryClient = new QueryClient({
    queryCache: new QueryCache({ onError }),
    mutationCache: new MutationCache({ onError }),
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        // 401/403/404 tekrar denemekle düzelmez.
        retry: (failureCount, error) =>
          !(error instanceof ApiError && error.status < 500) && failureCount < 2,
      },
    },
  });
  return client;
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
        <SessionKeeper />
        {children}
        {/* Alttaki haber şeridinin (≈44 px) üstünde dursun. */}
        <Toaster position="bottom-right" offset={{ bottom: 64, right: 24 }} mobileOffset={{ bottom: 60 }} />
      </TooltipProvider>
    </QueryClientProvider>
  );
}
