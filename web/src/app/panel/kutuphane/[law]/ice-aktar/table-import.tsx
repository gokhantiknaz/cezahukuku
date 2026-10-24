"use client";

import { useState } from "react";
import { Link2, Table2 } from "lucide-react";
import { toast } from "sonner";
import { useBulkOldLawLinks } from "@/api/generated/admin-laws/admin-laws";
import { useGetLibrary } from "@/api/generated/library/library";
import type { BulkOldLawLinksResult, LawSummary } from "@/api/generated/model";
import { FormMessages, otherMessages } from "@/components/panel/form";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { articlesTableToText, guessHeaderRow, parseTable } from "./table-paste";

/**
 * Excel'den madde tablosu: "Madde no | Başlık | Metin" sütunları yapıştırılır, içe aktarma metnine çevrilir ve
 * kanun metni gibi önizlenip içe aktarılır (sürüm ve güncelleme kuralları aynı).
 */
export function ArticlesTablePaste({ onConvert }: { onConvert: (text: string) => void }) {
  const [table, setTable] = useState("");
  const [headerOverride, setHeaderOverride] = useState<boolean | null>(null);
  const all = parseTable(table);
  const header = headerOverride ?? guessHeaderRow(all);
  const rows = header ? all.slice(1) : all;

  return (
    <div className="space-y-2 rounded-md border bg-muted/30 p-3">
      <p className="flex items-center gap-2 font-medium">
        <Table2 className="size-4" aria-hidden /> Excel tablosundan maddeler
      </p>
      <p className="text-sm text-muted-foreground">
        Sütunlar: <strong>Madde no</strong> · <strong>Başlık</strong> · <strong>Metin</strong>. Excel&apos;de hücreleri
        seçip kopyalayın (Ctrl+C) ve buraya yapıştırın; başlık satırı varsa atlanır. Metindeki “(1)”, “(2)” fıkraları ve
        “a)” bentleri ayrıştırılır.
      </p>
      <Textarea
        value={table}
        onChange={(e) => setTable(e.target.value)}
        placeholder={"81\tKasten öldürme\t(1) Bir insanı kasten öldüren kişi, müebbet hapis cezası ile cezalandırılır.\n82\tNitelikli haller\t(1) Kasten öldürme suçunun; …"}
        className="h-32 font-mono text-xs"
        aria-label="Excel tablosu"
      />
      <HeaderToggle checked={header} onChange={setHeaderOverride} />
      <div className="flex items-center gap-2">
        <Button
          type="button"
          variant="outline"
          disabled={rows.length === 0}
          onClick={() => {
            onConvert(articlesTableToText(rows));
            toast.success(`${rows.length} madde metne çevrildi; önizleyip içe aktarın.`);
          }}
        >
          Metne çevir ({rows.length} satır)
        </Button>
        {rows.some((r) => r.length < 3) && (
          <span className="text-sm text-destructive">Bazı satırlarda üç sütun yok.</span>
        )}
      </div>
    </div>
  );
}

const statusLabels = { created: "Eklenecek", exists: "Zaten var", error: "Hata" } as const;

/**
 * Eski kanun karşılıkları (TCK–765 karşılaştırma tablosu): "Yeni madde | Eski madde(ler) | Not" sütunları.
 * Eski kanun kütüphanedeyse maddelerine bağlanır, değilse etiketle ("765 s. TCK m. 448") tutulur.
 */
export function OldLawLinksImport({ law }: { law: LawSummary }) {
  const { data: library } = useGetLibrary();
  const laws = (library?.data.groups.flatMap((g) => g.laws) ?? []).filter((l) => l.id !== law.id);
  const [targetLawId, setTargetLawId] = useState("");
  const [targetLabel, setTargetLabel] = useState("765 s. TCK");
  const [table, setTable] = useState("");
  const [result, setResult] = useState<{ dryRun: boolean; data: BulkOldLawLinksResult } | null>(null);
  const [headerOverride, setHeaderOverride] = useState<boolean | null>(null);
  const bulk = useBulkOldLawLinks();

  const all = parseTable(table);
  const header = headerOverride ?? guessHeaderRow(all);
  const rows = (header ? all.slice(1) : all).map(([source = "", target = "", note = ""]) => ({
    source,
    target,
    note: note || null,
  }));

  async function run(dryRun: boolean) {
    const response = await bulk.mutateAsync({
      lawId: law.id,
      data: { targetLawId: targetLawId || null, targetLabel: targetLawId ? null : targetLabel, rows, dryRun },
    });
    if (response.status !== 200) return;
    setResult({ dryRun, data: response.data });
    if (!dryRun) toast.success(`${response.data.created} karşılık eklendi.`);
  }

  return (
    <section className="space-y-3 rounded-lg border bg-card p-4">
      <h2 className="flex items-center gap-2 text-xl">
        <Link2 className="size-5" aria-hidden /> Eski kanun karşılıkları (karşılaştırma tablosu)
      </h2>
      <p className="text-sm text-muted-foreground">
        Sütunlar: <strong>{law.shortName || law.number} madde</strong> · <strong>Eski kanun maddesi</strong> (birden
        çoksa virgülle) · <strong>Not</strong> (isteğe bağlı). Var olan karşılıklar tekrar eklenmez.
      </p>
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="space-y-1">
          <span className="text-sm font-medium">Eski kanun</span>
          <select
            value={targetLawId}
            onChange={(e) => setTargetLawId(e.target.value)}
            className="block h-9 w-full rounded-md border bg-background px-3"
          >
            <option value="">Kütüphanede yok (etiketle)</option>
            {laws.map((l) => (
              <option key={l.id} value={l.id}>
                {l.shortName || l.number} — {l.name}
              </option>
            ))}
          </select>
        </label>
        {!targetLawId && (
          <label className="space-y-1">
            <span className="text-sm font-medium">Etiket</span>
            <Input value={targetLabel} onChange={(e) => setTargetLabel(e.target.value)} placeholder="765 s. TCK" />
          </label>
        )}
      </div>
      <Textarea
        value={table}
        onChange={(e) => {
          setTable(e.target.value);
          setResult(null);
        }}
        placeholder={"81\t448\n82\t450\tnitelikli haller\n83\t448, 449"}
        className="h-32 font-mono text-xs"
        aria-label="Karşılaştırma tablosu"
      />
      <HeaderToggle
        checked={header}
        onChange={(value) => {
          setHeaderOverride(value);
          setResult(null);
        }}
      />
      <FormMessages messages={otherMessages(bulk.error, [])} />
      <div className="flex flex-wrap gap-2">
        <Button variant="outline" disabled={rows.length === 0 || bulk.isPending} onClick={() => run(true).catch(() => {})}>
          Denetle ({rows.length} satır)
        </Button>
        {result?.dryRun && result.data.created > 0 && (
          <Button disabled={bulk.isPending} onClick={() => run(false).catch(() => {})}>
            Kaydet ({result.data.created} karşılık)
          </Button>
        )}
      </div>
      {result && (
        <div className="space-y-2">
          <p className="text-sm">
            {result.dryRun ? "Denetim" : "Kaydedildi"}: {result.data.created} {result.dryRun ? "eklenecek" : "eklendi"} ·{" "}
            {result.data.exists} zaten var · {result.data.errors} hata
          </p>
          <ul className="max-h-60 divide-y overflow-y-auto rounded-md border text-sm">
            {result.data.rows.map((row, i) => (
              <li key={i} className="flex items-center gap-2 px-3 py-1.5">
                <span className="w-10 text-muted-foreground">{row.row}.</span>
                <span className="w-40">
                  {row.source} → {row.target}
                </span>
                <Badge variant={row.status === "error" ? "destructive" : row.status === "exists" ? "secondary" : "default"}>
                  {result.dryRun || row.status !== "created" ? statusLabels[row.status] : "Eklendi"}
                </Badge>
                {row.message && <span className="text-muted-foreground">{row.message}</span>}
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}

function HeaderToggle({ checked, onChange }: { checked: boolean; onChange: (value: boolean) => void }) {
  return (
    <label className="flex items-center gap-2 text-sm">
      <Checkbox checked={checked} onCheckedChange={(value) => onChange(value === true)} />
      İlk satır başlık (alınmasın)
    </label>
  );
}
