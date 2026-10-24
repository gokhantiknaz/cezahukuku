import type { Metadata } from "next";
import { ScalesLoader } from "@/components/scales-loader";
import { LoginForm } from "./login-form";

export const metadata: Metadata = { title: "Giriş" };

export default function LoginPage() {
  return (
    <div className="flex flex-1 items-start justify-center p-4 pt-12">
      <div className="w-full max-w-sm rounded-lg border bg-card p-6 shadow-sm">
        <div className="flex flex-col items-center text-center">
          {/* Süs: yavaş salınan terazi. */}
          <ScalesLoader className="size-24" duration={6} />
          <p className="mt-1 text-sm text-muted-foreground italic">“Adalet mülkün temelidir.”</p>
        </div>
        <h1 className="mt-5 text-2xl text-primary">Giriş yap</h1>
        <p className="mt-1 text-sm text-muted-foreground">Hesabınızı sistem yöneticisi açar.</p>
        <LoginForm />
      </div>
    </div>
  );
}
