// Backend'deki CezaHukuku.Domain.Users.Roles ile aynı değerler.
export const Roles = {
  SuperAdmin: "super_admin",
  Standard: "standard",
  Reader: "reader",
} as const;

export type Role = (typeof Roles)[keyof typeof Roles];

export const roleLabels: Record<Role, string> = {
  super_admin: "Sistem Yöneticisi",
  standard: "Standart Kullanıcı",
  reader: "Okuyucu",
};

export const allRoles = Object.values(Roles);
