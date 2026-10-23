"use client";

import type { ReactNode } from "react";
import { ApiError } from "@/api/fetcher";
import type { LawNodeKind, Visibility } from "@/api/generated/model";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";

/** API doğrulama hatasından bir alanın mesajları (alan adları JSON'daki camelCase adlar). */
export function fieldMessages(error: unknown, field: string): string[] {
  return error instanceof ApiError ? (error.problem?.errors?.[field] ?? []) : [];
}

/** Formdaki alanlara ait olmayan hatalar (genel hata, "kendi hesabınız" gibi kurallar, ağ hatası). */
export function otherMessages(error: unknown, fields: string[]): string[] {
  if (!error) return [];
  if (!(error instanceof ApiError)) return ["İşlem başarısız. Bağlantıyı kontrol edip tekrar deneyin."];
  const errors = error.problem?.errors;
  if (!errors) return [error.message];
  return Object.entries(errors)
    .filter(([key]) => !fields.includes(key))
    .flatMap(([, messages]) => messages);
}

export function Field({
  label,
  htmlFor,
  error,
  field,
  hint,
  className,
  children,
}: {
  label: string;
  htmlFor: string;
  error?: unknown;
  field?: string;
  hint?: string;
  className?: string;
  children: ReactNode;
}) {
  const messages = field ? fieldMessages(error, field) : [];
  return (
    <div className={className}>
      <Label htmlFor={htmlFor} className="mb-1.5">
        {label}
      </Label>
      {children}
      {hint && messages.length === 0 && <p className="mt-1 text-xs text-muted-foreground">{hint}</p>}
      {messages.map((message) => (
        <p key={message} className="mt-1 text-xs text-destructive">
          {message}
        </p>
      ))}
    </div>
  );
}

export function FormMessages({ messages }: { messages: string[] }) {
  if (messages.length === 0) return null;
  return (
    <div role="alert" className="rounded-md bg-accent px-3 py-2 text-sm text-accent-foreground">
      {messages.map((message) => (
        <p key={message}>{message}</p>
      ))}
    </div>
  );
}

export const visibilityLabels: Record<Visibility, string> = {
  public: "Herkese açık",
  private: "Yalnız giriş yapanlar (özel)",
};

export const nodeKindLabels: Record<LawNodeKind, string> = {
  kitap: "Kitap",
  kisim: "Kısım",
  bolum: "Bölüm",
  ayrim: "Ayrım",
};

export function OptionSelect<T extends string>({
  id,
  value,
  options,
  onChange,
}: {
  id: string;
  value: T;
  options: Record<T, string>;
  onChange: (value: T) => void;
}) {
  return (
    <Select value={value} onValueChange={(v) => onChange(v as T)}>
      <SelectTrigger id={id} className="w-full">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {(Object.keys(options) as T[]).map((key) => (
          <SelectItem key={key} value={key}>
            {options[key]}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

/** Boş metin API'ye null gider (isteğe bağlı alanlar). */
export const emptyToNull = (value: string) => (value.trim() === "" ? null : value);

/** "Sıra" alanı: boşsa null (oluştururken sona ekle, güncellerken mevcut sırayı koru). */
export const toSortOrder = (value: string) => {
  const parsed = Number.parseInt(value, 10);
  return Number.isNaN(parsed) ? null : parsed;
};
