import type { Metadata } from "next";
import { CitationCheck } from "@/components/tools/citation-check";

export const metadata: Metadata = {
  title: "Atıf Denetimi",
  description:
    "Dilekçe ve kararlardaki madde atıflarını ve Yargıtay / AYM künyelerini denetleyin: mülga ya da değişmiş madde, olmayan fıkra, tutarsız künye.",
};

export default function Page() {
  return (
    <div className="mx-auto w-full max-w-7xl px-4 py-6">
      <h1 className="text-3xl">Atıf Denetimi</h1>
      <p className="mt-1 max-w-4xl text-muted-foreground">
        Metindeki madde atıflarını (“TCK m. 53/1-c”) ve karar künyelerini (“Yargıtay 1. CD, 12.03.2019, 2018/1234 E.,
        2019/567 K.”) kütüphaneyle karşılaştırır: madde mülga mı, değişti mi, AYM iptal etti mi, fıkra ve bent var mı;
        künyede karar yılı tarihle uyuşuyor mu, karar kütüphanede var mı.
      </p>
      <CitationCheck />
    </div>
  );
}
