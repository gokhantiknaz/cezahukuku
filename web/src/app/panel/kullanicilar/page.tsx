import type { Metadata } from "next";
import { UsersAdmin } from "./users-admin";

export const metadata: Metadata = { title: "Kullanıcılar" };

export default function UsersPage() {
  return (
    <>
      <p className="mt-4 text-sm text-muted-foreground">
        Dışarıdan kayıt kapalıdır; kullanıcıları buradan açın, rolünü değiştirin veya pasifleştirin.
      </p>
      <UsersAdmin />
    </>
  );
}
