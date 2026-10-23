import { Megaphone } from "lucide-react";

// M7'de Ceza Gündemi yönetim panelinden beslenecek; şimdilik tek bilgilendirme satırı.
const items = ["Ceza Gündemi haberleri yönetim panelinden eklendiğinde burada akacak."];

export function NewsTicker() {
  // Kesintisiz kayma için liste iki kez yan yana basılır, animasyon yarı genişlikte başa sarar.
  const loop = [...items, ...items];

  return (
    <footer className="flex items-center gap-4 overflow-hidden bg-primary px-4 py-2 text-primary-foreground print:hidden">
      <span className="flex shrink-0 items-center gap-2">
        <Megaphone className="size-6" aria-hidden />
        <span className="rounded bg-primary-foreground px-2 py-1 text-sm font-bold text-primary">CEZA GÜNDEMİ</span>
      </span>
      <div className="min-w-0 flex-1 overflow-hidden">
        <ul className="animate-ticker flex w-max gap-12 whitespace-nowrap">
          {loop.map((item, index) => (
            <li key={index} className="flex items-center gap-2" aria-hidden={index >= items.length}>
              <span className="size-2 rounded-full bg-primary-foreground/80" />
              {item}
            </li>
          ))}
        </ul>
      </div>
    </footer>
  );
}
