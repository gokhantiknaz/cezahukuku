"use client";

import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { ApiError } from "@/api/fetcher";
import {
  getListUsersQueryKey,
  useCreateUser,
  useListUsers,
  useResetUserPassword,
  useUpdateUser,
} from "@/api/generated/admin-users/admin-users";
import type { UserSummary } from "@/api/generated/model";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { useCurrentUser } from "@/lib/auth";
import { allRoles, roleLabels, Roles, type Role } from "@/lib/roles";

function errorText(error: unknown) {
  if (!error) return null;
  return error instanceof ApiError ? error.messages.join(" ") : "İşlem başarısız.";
}

/** Yönetici kontrolü panel layout'undaki AdminGate'te yapılır. */
export function UsersAdmin() {
  const { data: currentUser } = useCurrentUser();
  if (!currentUser) return null;

  return (
    <>
      <CreateUserForm />
      <UserTable currentUserId={currentUser.id} />
    </>
  );
}

function RoleSelect({
  value,
  defaultValue,
  name,
  disabled,
  onValueChange,
}: {
  value?: Role;
  defaultValue?: Role;
  name?: string;
  disabled?: boolean;
  onValueChange?: (role: Role) => void;
}) {
  return (
    <Select
      value={value}
      defaultValue={defaultValue}
      name={name}
      disabled={disabled}
      onValueChange={(v) => onValueChange?.(v as Role)}
    >
      <SelectTrigger className="w-full min-w-40" aria-label="Rol">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {allRoles.map((role) => (
          <SelectItem key={role} value={role}>
            {roleLabels[role]}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

function CreateUserForm() {
  const queryClient = useQueryClient();
  const [formKey, setFormKey] = useState(0);
  const createUser = useCreateUser({
    mutation: {
      onSuccess: () => {
        queryClient.invalidateQueries({ queryKey: getListUsersQueryKey() });
        setFormKey((key) => key + 1); // formu sıfırla
        toast.success("Kullanıcı oluşturuldu.");
      },
    },
  });

  function handleSubmit(formData: FormData) {
    createUser.mutate({
      data: {
        email: String(formData.get("email")),
        displayName: String(formData.get("displayName")),
        role: String(formData.get("role")),
        password: String(formData.get("password")),
      },
    });
  }

  return (
    <section className="mt-6 rounded-lg border bg-card p-4">
      <h2 className="text-lg">Yeni kullanıcı</h2>
      <form key={formKey} action={handleSubmit} className="mt-3 grid gap-3 md:grid-cols-[1fr_1fr_180px_1fr_auto]">
        <Input name="displayName" required placeholder="Ad Soyad" aria-label="Ad Soyad" />
        <Input name="email" type="email" required placeholder="E-posta" aria-label="E-posta" />
        <RoleSelect name="role" defaultValue={Roles.Standard} />
        <Input
          name="password"
          type="password"
          required
          minLength={10}
          placeholder="Geçici şifre (en az 10)"
          aria-label="Geçici şifre"
          autoComplete="new-password"
        />
        <Button type="submit" disabled={createUser.isPending}>
          Ekle
        </Button>
      </form>
      {createUser.error && (
        <p role="alert" className="mt-2 text-sm text-destructive">
          {errorText(createUser.error)}
        </p>
      )}
    </section>
  );
}

function UserTable({ currentUserId }: { currentUserId: string }) {
  const { data, error } = useListUsers();

  if (error) return <p className="mt-6 text-destructive">{errorText(error)}</p>;
  if (!data) return <p className="mt-6 text-muted-foreground">Kullanıcılar yükleniyor…</p>;

  return (
    <section className="mt-6 rounded-lg border bg-card">
      <Table>
        <TableHeader>
          <TableRow className="bg-accent hover:bg-accent">
            <TableHead className="text-accent-foreground">Ad Soyad</TableHead>
            <TableHead className="text-accent-foreground">E-posta</TableHead>
            <TableHead className="text-accent-foreground">Rol</TableHead>
            <TableHead className="text-accent-foreground">Aktif</TableHead>
            <TableHead />
          </TableRow>
        </TableHeader>
        <TableBody>
          {data.data.map((user) => (
            <UserRow key={user.id} user={user} isSelf={user.id === currentUserId} />
          ))}
        </TableBody>
      </Table>
    </section>
  );
}

function UserRow({ user, isSelf }: { user: UserSummary; isSelf: boolean }) {
  const queryClient = useQueryClient();
  const [displayName, setDisplayName] = useState(user.displayName);
  const [role, setRole] = useState(user.role as Role);
  const [isActive, setIsActive] = useState(user.isActive);
  const [newPassword, setNewPassword] = useState("");

  const updateUser = useUpdateUser({
    mutation: {
      onSuccess: () => {
        queryClient.invalidateQueries({ queryKey: getListUsersQueryKey() });
        toast.success("Kullanıcı güncellendi.");
      },
    },
  });
  const resetPassword = useResetUserPassword({
    mutation: {
      onSuccess: () => {
        setNewPassword("");
        toast.success("Şifre güncellendi.");
      },
    },
  });

  const dirty = displayName !== user.displayName || role !== user.role || isActive !== user.isActive;
  const actionError = errorText(updateUser.error ?? resetPassword.error);

  return (
    <TableRow className="align-top">
      <TableCell>
        <Input value={displayName} onChange={(e) => setDisplayName(e.target.value)} aria-label="Ad Soyad" />
      </TableCell>
      <TableCell className="pt-4">{user.email}</TableCell>
      <TableCell>
        <RoleSelect value={role} disabled={isSelf} onValueChange={setRole} />
      </TableCell>
      <TableCell className="pt-4">
        <Checkbox
          checked={isActive}
          disabled={isSelf}
          onCheckedChange={(checked) => setIsActive(checked === true)}
          aria-label="Aktif"
        />
      </TableCell>
      <TableCell className="space-y-2">
        <Button
          disabled={!dirty || updateUser.isPending}
          onClick={() => updateUser.mutate({ id: user.id, data: { displayName, role, isActive } })}
        >
          Kaydet
        </Button>
        <div className="flex gap-2">
          <Input
            type="password"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            placeholder="Yeni şifre"
            aria-label="Yeni şifre"
            autoComplete="new-password"
            className="w-36"
          />
          <Button
            variant="outline"
            disabled={newPassword.length < 10 || resetPassword.isPending}
            onClick={() => resetPassword.mutate({ id: user.id, data: { newPassword } })}
          >
            Sıfırla
          </Button>
        </div>
        {actionError && (
          <p role="alert" className="text-xs text-destructive">
            {actionError}
          </p>
        )}
      </TableCell>
    </TableRow>
  );
}
