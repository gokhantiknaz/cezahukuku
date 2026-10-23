import type { Metadata } from "next";
import { LoginForm } from "./login-form";

export const metadata: Metadata = { title: "Giriş" };

export default function LoginPage() {
  return (
    <div className="flex flex-1 items-start justify-center p-4 pt-16">
      <div className="w-full max-w-sm rounded-lg border bg-card p-6 shadow-sm">
        <h1 className="text-2xl text-primary">Giriş yap</h1>
        <p className="mt-1 text-sm text-muted-foreground">Hesabınızı sistem yöneticisi açar.</p>
        <LoginForm />
      </div>
    </div>
  );
}
