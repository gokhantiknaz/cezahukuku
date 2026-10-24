"use client";

import { useState, type ReactNode } from "react";
import { toast } from "sonner";
import { useCreateLaw, useCreateLawGroup, useUpdateLaw, useUpdateLawGroup } from "@/api/generated/admin-laws/admin-laws";
import type { LawGroupWithLaws, LawSummary, Visibility } from "@/api/generated/model";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { emptyToNull, Field, FormMessages, OptionSelect, otherMessages, toSortOrder, visibilityLabels } from "./form";
import { useLibraryRefresh } from "./use-library-refresh";

/** Pencere her açılışta formu baştan kurar (yarım kalan düzenleme sonraki açılışa taşınmasın). */
export function FormDialog({
  title,
  description,
  trigger,
  children,
}: {
  title: string;
  description?: string;
  trigger: ReactNode;
  children: (close: () => void) => ReactNode;
}) {
  const [open, setOpen] = useState(false);
  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {description && <DialogDescription>{description}</DialogDescription>}
        </DialogHeader>
        {open && children(() => setOpen(false))}
      </DialogContent>
    </Dialog>
  );
}

export function LawGroupDialog({ group, trigger }: { group?: LawGroupWithLaws; trigger: ReactNode }) {
  return (
    <FormDialog title={group ? "Grubu düzenle" : "Yeni grup"} trigger={trigger}>
      {(close) => <LawGroupForm group={group} onDone={close} />}
    </FormDialog>
  );
}

function LawGroupForm({ group, onDone }: { group?: LawGroupWithLaws; onDone: () => void }) {
  const refresh = useLibraryRefresh();
  const [title, setTitle] = useState(group?.title ?? "");
  const [slug, setSlug] = useState(group?.slug ?? "");
  const [sortOrder, setSortOrder] = useState(group ? String(group.sortOrder) : "");
  const create = useCreateLawGroup();
  const update = useUpdateLawGroup();
  const error = create.error ?? update.error;

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const data = { title, slug: emptyToNull(slug), sortOrder: toSortOrder(sortOrder) };
    if (group) await update.mutateAsync({ id: group.id, data });
    else await create.mutateAsync({ data });
    await refresh();
    toast.success(group ? "Grup güncellendi." : "Grup eklendi.");
    onDone();
  }

  return (
    <form onSubmit={(e) => submit(e).catch(() => {})} className="space-y-4">
      <Field label="Grup adı" htmlFor="group-title" error={error} field="title">
        <Input id="group-title" value={title} onChange={(e) => setTitle(e.target.value)} required autoFocus />
      </Field>
      <div className="grid gap-4 sm:grid-cols-[1fr_8rem]">
        <Field label="Kısa ad" htmlFor="group-slug" error={error} field="slug" hint="Boş bırakılırsa addan üretilir.">
          <Input id="group-slug" value={slug} onChange={(e) => setSlug(e.target.value)} />
        </Field>
        <Field label="Sıra" htmlFor="group-order" hint="Boşsa sona.">
          <Input id="group-order" type="number" value={sortOrder} onChange={(e) => setSortOrder(e.target.value)} />
        </Field>
      </div>
      <FormMessages messages={otherMessages(error, ["title", "slug"])} />
      <DialogFooter>
        <Button type="submit" disabled={create.isPending || update.isPending}>
          Kaydet
        </Button>
      </DialogFooter>
    </form>
  );
}

export function LawDialog({
  law,
  groups,
  defaultGroupId,
  trigger,
}: {
  law?: LawSummary;
  groups: LawGroupWithLaws[];
  defaultGroupId?: string;
  trigger: ReactNode;
}) {
  return (
    <FormDialog
      title={law ? "Kanunu düzenle" : "Yeni kanun"}
      description="Maddeler ve bölümler kanun kaydedildikten sonra “Yapı ve maddeler” ekranından girilir."
      trigger={trigger}
    >
      {(close) => <LawForm law={law} groups={groups} defaultGroupId={defaultGroupId} onDone={close} />}
    </FormDialog>
  );
}

const lawFields = ["groupId", "number", "name", "shortName", "slug", "gazetteNumber"];

function LawForm({
  law,
  groups,
  defaultGroupId,
  onDone,
}: {
  law?: LawSummary;
  groups: LawGroupWithLaws[];
  defaultGroupId?: string;
  onDone: () => void;
}) {
  const refresh = useLibraryRefresh();
  const [form, setForm] = useState({
    groupId: law?.groupId ?? defaultGroupId ?? groups[0]?.id ?? "",
    number: law?.number ?? "",
    name: law?.name ?? "",
    shortName: law?.shortName ?? "",
    slug: law?.slug ?? "",
    acceptedOn: law?.acceptedOn ?? "",
    gazetteDate: law?.gazetteDate ?? "",
    gazetteNumber: law?.gazetteNumber ?? "",
    visibility: law?.visibility ?? ("public" as Visibility),
    sortOrder: law ? String(law.sortOrder) : "",
  });
  const set = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((f) => ({ ...f, [key]: e.target.value }));
  const create = useCreateLaw();
  const update = useUpdateLaw();
  const error = create.error ?? update.error;

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const data = {
      groupId: form.groupId,
      number: form.number,
      name: form.name,
      shortName: emptyToNull(form.shortName),
      slug: emptyToNull(form.slug),
      acceptedOn: emptyToNull(form.acceptedOn),
      gazetteDate: emptyToNull(form.gazetteDate),
      gazetteNumber: emptyToNull(form.gazetteNumber),
      visibility: form.visibility,
      sortOrder: toSortOrder(form.sortOrder),
    };
    if (law) await update.mutateAsync({ id: law.id, data });
    else await create.mutateAsync({ data });
    await refresh();
    toast.success(law ? "Kanun güncellendi." : "Kanun eklendi.");
    onDone();
  }

  return (
    <form onSubmit={(e) => submit(e).catch(() => {})} className="space-y-4">
      <Field label="Grup" htmlFor="law-group" error={error} field="groupId">
        <Select value={form.groupId} onValueChange={(groupId) => setForm((f) => ({ ...f, groupId }))}>
          <SelectTrigger id="law-group" className="w-full">
            <SelectValue placeholder="Grup seçin" />
          </SelectTrigger>
          <SelectContent>
            {groups.map((g) => (
              <SelectItem key={g.id} value={g.id}>
                {g.title}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </Field>
      <div className="grid gap-4 sm:grid-cols-[8rem_1fr]">
        <Field label="Numara" htmlFor="law-number" error={error} field="number">
          <Input id="law-number" value={form.number} onChange={set("number")} required placeholder="5237" />
        </Field>
        <Field label="Adı" htmlFor="law-name" error={error} field="name">
          <Input id="law-name" value={form.name} onChange={set("name")} required placeholder="Türk Ceza Kanunu" />
        </Field>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Kısaltma" htmlFor="law-short" error={error} field="shortName">
          <Input id="law-short" value={form.shortName} onChange={set("shortName")} placeholder="TCK" />
        </Field>
        <Field
          label="Adres"
          htmlFor="law-slug"
          error={error}
          field="slug"
          hint="Boşsa kısaltmadan: /tck/madde-81"
        >
          <Input id="law-slug" value={form.slug} onChange={set("slug")} placeholder="tck" />
        </Field>
      </div>
      <div className="grid gap-4 sm:grid-cols-3">
        <Field label="Kabul tarihi" htmlFor="law-accepted">
          <Input id="law-accepted" type="date" value={form.acceptedOn} onChange={set("acceptedOn")} />
        </Field>
        <Field label="RG tarihi" htmlFor="law-gazette-date">
          <Input id="law-gazette-date" type="date" value={form.gazetteDate} onChange={set("gazetteDate")} />
        </Field>
        <Field label="RG sayısı" htmlFor="law-gazette-number" error={error} field="gazetteNumber">
          <Input id="law-gazette-number" value={form.gazetteNumber} onChange={set("gazetteNumber")} />
        </Field>
      </div>
      <div className="grid gap-4 sm:grid-cols-[1fr_8rem]">
        <Field label="Görünürlük" htmlFor="law-visibility">
          <OptionSelect
            id="law-visibility"
            value={form.visibility}
            options={visibilityLabels}
            onChange={(visibility) => setForm((f) => ({ ...f, visibility }))}
          />
        </Field>
        <Field label="Sıra" htmlFor="law-order" hint="Boşsa sona.">
          <Input id="law-order" type="number" value={form.sortOrder} onChange={set("sortOrder")} />
        </Field>
      </div>
      <FormMessages messages={otherMessages(error, lawFields)} />
      <DialogFooter>
        <Button type="submit" disabled={create.isPending || update.isPending}>
          Kaydet
        </Button>
      </DialogFooter>
    </form>
  );
}
