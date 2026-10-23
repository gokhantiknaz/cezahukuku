"use client";

import { useState } from "react";
import { Trash2 } from "lucide-react";
import { toast } from "sonner";
import { useCreateLawNode, useDeleteLawNode, useUpdateLawNode } from "@/api/generated/admin-laws/admin-laws";
import type { LawNodeKind, LawNodeTree, Visibility } from "@/api/generated/model";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { ConfirmDelete } from "./confirm-delete";
import { Field, FormMessages, nodeKindLabels, OptionSelect, otherMessages, toSortOrder, visibilityLabels } from "./form";
import { flattenNodes, nodeLabel } from "./tree-utils";
import { useLibraryRefresh } from "./use-library-refresh";

const ROOT = "__root";

/** Üst düğüm seçimi; "kanunun doğrudan altı" da bir seçenektir. */
export function NodeSelect({
  id,
  nodes,
  value,
  exclude,
  onChange,
}: {
  id: string;
  nodes: LawNodeTree[];
  value: string | null;
  exclude?: Set<string>;
  onChange: (nodeId: string | null) => void;
}) {
  const options = flattenNodes(nodes).filter(({ node }) => !exclude?.has(node.id));
  return (
    <Select value={value ?? ROOT} onValueChange={(v) => onChange(v === ROOT ? null : v)}>
      <SelectTrigger id={id} className="w-full">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={ROOT}>Kanunun doğrudan altı</SelectItem>
        {options.map(({ node, depth }) => (
          <SelectItem key={node.id} value={node.id}>
            {"  ".repeat(depth)}
            {nodeLabel(node)}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

const nodeFields = ["parentId", "number", "title"];

/** Ağaç düğümü (Kitap/Kısım/Bölüm/Ayrım) ekleme ve düzenleme. */
export function NodeEditor({
  lawId,
  nodes,
  node,
  parentId,
  excludeFromParents,
  onSaved,
  onDeleted,
}: {
  lawId: string;
  nodes: LawNodeTree[];
  /** Düzenlenen düğüm; yoksa yeni düğüm. */
  node?: LawNodeTree;
  /** Yeni düğümün üst düğümü. */
  parentId?: string | null;
  excludeFromParents?: Set<string>;
  onSaved: (nodeId: string) => void;
  onDeleted: () => void;
}) {
  const refresh = useLibraryRefresh();
  const [form, setForm] = useState({
    parentId: node ? node.parentId : (parentId ?? null),
    kind: node?.kind ?? ("bolum" as LawNodeKind),
    number: node?.number ?? "",
    title: node?.title ?? "",
    visibility: node?.visibility ?? ("public" as Visibility),
    sortOrder: node ? String(node.sortOrder) : "",
  });
  const create = useCreateLawNode();
  const update = useUpdateLawNode();
  const remove = useDeleteLawNode();
  const error = create.error ?? update.error;

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const data = { ...form, sortOrder: toSortOrder(form.sortOrder) };
    const saved = node
      ? await update.mutateAsync({ id: node.id, data })
      : await create.mutateAsync({ lawId, data });
    await refresh();
    toast.success(node ? "Düğüm güncellendi." : "Düğüm eklendi.");
    if (saved.status === 200 || saved.status === 201) onSaved(saved.data.id);
  }

  return (
    <form onSubmit={(e) => submit(e).catch(() => {})} className="space-y-4">
      <h2 className="text-xl">{node ? nodeLabel(node) : "Yeni düğüm"}</h2>
      <div className="grid gap-4 sm:grid-cols-[10rem_1fr]">
        <Field label="Tür" htmlFor="node-kind">
          <OptionSelect
            id="node-kind"
            value={form.kind}
            options={nodeKindLabels}
            onChange={(kind) => setForm((f) => ({ ...f, kind }))}
          />
        </Field>
        <Field label="Sıra adı" htmlFor="node-number" error={error} field="number">
          <Input
            id="node-number"
            value={form.number}
            onChange={(e) => setForm((f) => ({ ...f, number: e.target.value }))}
            placeholder="Birinci Bölüm"
            required
          />
        </Field>
      </div>
      <Field label="Başlık" htmlFor="node-title" error={error} field="title">
        <Input
          id="node-title"
          value={form.title}
          onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
          placeholder="Hayata Karşı Suçlar"
          required
        />
      </Field>
      <Field label="Üst düğüm" htmlFor="node-parent" error={error} field="parentId">
        <NodeSelect
          id="node-parent"
          nodes={nodes}
          value={form.parentId}
          exclude={excludeFromParents}
          onChange={(parent) => setForm((f) => ({ ...f, parentId: parent }))}
        />
      </Field>
      <div className="grid gap-4 sm:grid-cols-[1fr_8rem]">
        <Field
          label="Görünürlük"
          htmlFor="node-visibility"
          hint="Özel yapılırsa altındaki tüm bölüm ve maddeler de özel olur."
        >
          <OptionSelect
            id="node-visibility"
            value={form.visibility}
            options={visibilityLabels}
            onChange={(visibility) => setForm((f) => ({ ...f, visibility }))}
          />
        </Field>
        <Field label="Sıra" htmlFor="node-order" hint="Boşsa sona.">
          <Input
            id="node-order"
            type="number"
            value={form.sortOrder}
            onChange={(e) => setForm((f) => ({ ...f, sortOrder: e.target.value }))}
          />
        </Field>
      </div>
      <FormMessages messages={otherMessages(error, nodeFields)} />
      <div className="flex justify-between gap-2">
        <Button type="submit" disabled={create.isPending || update.isPending}>
          Kaydet
        </Button>
        {node && (
          <ConfirmDelete
            title={`“${node.number}” silinsin mi?`}
            description="Yalnız boş düğüm silinebilir; önce içindeki maddeleri ve alt düğümleri taşıyın veya silin."
            onConfirm={async () => {
              await remove.mutateAsync({ id: node.id });
              await refresh();
              toast.success("Düğüm silindi.");
              onDeleted();
            }}
          >
            <Button type="button" variant="ghost">
              <Trash2 /> Sil
            </Button>
          </ConfirmDelete>
        )}
      </div>
    </form>
  );
}
