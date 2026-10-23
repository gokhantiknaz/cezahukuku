"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError } from "@/api/fetcher";
import { getCurrentUser, getGetCurrentUserQueryKey } from "@/api/generated/auth/auth";
import type { CurrentUserResponse } from "@/api/generated/model";
import { Roles } from "./roles";

/** Oturumdaki kullanıcı; giriş yapılmamışsa null. */
export function useCurrentUser() {
  return useQuery({
    queryKey: getGetCurrentUserQueryKey(),
    queryFn: async (): Promise<CurrentUserResponse | null> => {
      try {
        return (await getCurrentUser()).data;
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) return null;
        throw error;
      }
    },
  });
}

/** Giriş/çıkış sonrası oturum bilgisini yeniden yükler. */
export function useRefreshCurrentUser() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: getGetCurrentUserQueryKey() });
}

export function isAdmin(user: CurrentUserResponse | null | undefined) {
  return user?.roles.includes(Roles.SuperAdmin) ?? false;
}
