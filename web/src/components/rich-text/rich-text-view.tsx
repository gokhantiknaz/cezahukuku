import { cn } from "@/lib/utils";

/**
 * Editoryal notun HTML'ini gösterir. HTML sunucuda kaydedilirken izin verilen etiketlere indirgenir
 * (RichText.Sanitize: script, olay özellikleri, stil ve javascript: bağlantıları atılır); burada yeniden işlenmez.
 */
export function RichTextView({ html, className }: { html: string; className?: string }) {
  return <div className={cn("rich-text reading", className)} dangerouslySetInnerHTML={{ __html: html }} />;
}
