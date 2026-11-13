/** Ders adı gösterimi: bölüm numaralı derste "2. Bölüm — Suç Teorisi", numarasız derste adın kendisi. */
export function courseHeading(course: { title: string; chapterNumber?: number | null }): string {
  return course.chapterNumber ? `${course.chapterNumber}. Bölüm — ${course.title}` : course.title;
}
