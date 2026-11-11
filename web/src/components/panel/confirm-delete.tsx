"use client";

import { useState, type ReactNode } from "react";
import { toast } from "sonner";
import { ApiError } from "@/api/fetcher";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { otherMessages } from "./form";

/** Kayıt bu arada (başka bir sekmede ya da kullanıcı tarafından) silinmiş: API 404 döndü. */
export const isGone = (error: unknown) => error instanceof ApiError && error.status === 404;
export const goneMessage = "Kayıt bulunamadı; başka bir yerde silinmiş olabilir. Liste yenilendi.";

/**
 * Silme onayı. API'nin reddettiği silmede (ör. "düğüm boş değil") gerekçe bildirim olarak gösterilir; kayıt zaten
 * silinmişse (404) `onGone` ile liste tazelenir.
 */
export function ConfirmDelete({
  title,
  description,
  confirmLabel = "Sil",
  onConfirm,
  onGone,
  children,
}: {
  title: string;
  description: string;
  confirmLabel?: string;
  onConfirm: () => Promise<unknown>;
  onGone?: () => Promise<unknown> | void;
  children: ReactNode;
}) {
  const [pending, setPending] = useState(false);

  async function confirm() {
    setPending(true);
    try {
      await onConfirm();
    } catch (error) {
      if (isGone(error)) {
        toast.error(goneMessage);
        await onGone?.();
        return;
      }
      toast.error(otherMessages(error, []).join(" ") || "Silinemedi.");
    } finally {
      setPending(false);
    }
  }

  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>{children}</AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription>{description}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Vazgeç</AlertDialogCancel>
          <AlertDialogAction variant="destructive" disabled={pending} onClick={confirm}>
            {confirmLabel}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
