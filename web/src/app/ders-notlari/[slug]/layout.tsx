import { Suspense, type ReactNode } from "react";
import { AddNodeButton } from "@/components/content/course-admin";
import { CourseShell } from "@/components/content/course-toc";
import { getLectureCourse } from "@/lib/server-api";

/**
 * /ders-notlari/{adres}: adres bir dersse ders düzeni (solda içindekiler; başlıklar arasında gezinirken korunur),
 * değilse düz ders notu sayfası olduğu gibi. Ders ve düz notlar aynı adres alanında, adresler tekildir.
 */
export default function LectureSlugLayout({ children, params }: LayoutProps<"/ders-notlari/[slug]">) {
  // İçindekiler oturum çerezine göre (özel başlıklar) sunucuda çekildiğinden Suspense içinde akar.
  return (
    <Suspense fallback={<p className="mx-auto w-full max-w-3xl px-4 py-6 text-muted-foreground">Yükleniyor…</p>}>
      <CourseFrame params={params}>{children}</CourseFrame>
    </Suspense>
  );
}

async function CourseFrame({ params, children }: { params: Promise<{ slug: string }>; children: ReactNode }) {
  const { slug } = await params;
  const course = await getLectureCourse(slug);
  return course ? (
    <CourseShell course={course} tocFooter={<AddNodeButton courseId={course.id} variant="ghost" />}>
      {children}
    </CourseShell>
  ) : (
    children
  );
}
