import type { Metadata } from "next";
import { UsersAdmin } from "./users-admin";

export const metadata: Metadata = { title: "Kullanıcılar" };

export default function UsersPage() {
  return (
    <div className="mx-auto w-full max-w-5xl p-4">
      <h1 className="text-2xl text-primary">Kullanıcılar</h1>
      <p className="mt-1 text-sm text-muted-foreground">
        Dışarıdan kayıt kapalıdır; kullanıcıları buradan açın, rolünü değiştirin veya pasifleştirin.
      </p>
      <UsersAdmin />
    </div>
  );
}
