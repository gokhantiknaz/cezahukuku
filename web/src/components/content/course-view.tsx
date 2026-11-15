import { Fragment } from "react";
import Link from "next/link";
import { ChevronLeft, ChevronRight, Lock } from "lucide-react";
import type {
  LectureCourseDetail,
  LectureNodeContext,
  LectureNodeRef,
  LectureNoteDetail,
  LectureNoteScope,
} from "@/api/generated/model";
import { CourseAdminActions, NodeAdminActions } from "@/components/content/course-admin";
import { LectureNoteFileList } from "@/components/content/lecture-note-files";
import { RichTextView } from "@/components/rich-text/rich-text-view";
import { Badge } from "@/components/ui/badge";
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb";
import { courseHeading } from "@/lib/lecture-courses";

/** Üyelere özel ders ya da başlık (üstünden miras alınan dahil). */
export function PrivateBadge() {
  return (
    <Badge variant="outline">
      <Lock /> Özel
    </Badge>
  );
}

/** Ders notunun bağlantısı: kanun grubu → kanun → madde. Hiçbiri yoksa `general` gösterilir. */
export function LectureNoteScopeBadges({ scope, general }: { scope: LectureNoteScope; general?: boolean }) {
  const { group, law, article } = scope;
  return (
    <>
      {general && !group && !law && <Badge variant="secondary">Genel</Badge>}
      {group && (
        <Link href={`/ders-notlari?grup=${group.slug}`}>
          <Badge variant="secondary">{group.title}</Badge>
        </Link>
      )}
      {law && (
        <Link href={`/${law.slug}`} title={law.name}>
          <Badge variant="outline">{law.label}</Badge>
        </Link>
      )}
      {article && law && (
        <Link href={`/${law.slug}/${article.articleSlug}`} className="text-primary hover:underline">
          Madde {article.number}
          {article.title ? ` — ${article.title}` : ""}
        </Link>
      )}
    </>
  );
}

const nodeHref = (courseSlug: string, node: { slug: string }) => `/ders-notlari/${courseSlug}/${node.slug}`;

/** Dersin giriş sayfası: ad, açıklama, birinci düzey başlıklar. */
export function CourseHome({ course }: { course: LectureCourseDetail }) {
  const roots = course.toc.filter((e) => e.depth === 1);
  const childCount = (id: string) => course.toc.filter((e) => e.parentId === id).length;
  return (
    <div className="mx-auto w-full max-w-3xl px-4 py-6">
      <Breadcrumb className="print:hidden">
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink asChild>
              <Link href="/ders-notlari">Ders Notları</Link>
            </BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>{course.title}</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>
      <header className="mt-2 flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h1 className="text-3xl leading-tight md:text-4xl">{courseHeading(course)}</h1>
          {course.visibility === "private" && (
            <p className="mt-2">
              <PrivateBadge />
            </p>
          )}
        </div>
        <CourseAdminActions course={course} />
      </header>
      {course.description && <p className="reading mt-3 text-muted-foreground">{course.description}</p>}

      {roots.length === 0 ? (
        <p className="mt-6 text-muted-foreground">Bu derse henüz başlık eklenmedi.</p>
      ) : (
        <section className="mt-6" aria-labelledby="ders-bolumleri">
          <h2 id="ders-bolumleri" className="text-xl">
            Bölümler
          </h2>
          <ul className="mt-2 space-y-2">
            {roots.map((root) => (
              <li key={root.id}>
                <Link
                  href={nodeHref(course.slug, root)}
                  className="flex flex-wrap items-baseline gap-x-2 rounded-lg border bg-card p-3 hover:border-primary/50"
                >
                  <span className="text-sm text-muted-foreground tabular-nums">{root.label}</span>
                  <span className="text-lg text-primary">{root.title}</span>
                  {root.effectiveVisibility === "private" && <Lock className="size-3.5 opacity-70" aria-label="Özel" />}
                  {childCount(root.id) > 0 && (
                    <span className="ml-auto text-sm text-muted-foreground">{childCount(root.id)} alt başlık</span>
                  )}
                </Link>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}

function NodeLinkList({ courseSlug, nodes }: { courseSlug: string; nodes: LectureNodeRef[] }) {
  return (
    <ul className="mt-2 divide-y rounded-lg border bg-card">
      {nodes.map((child) => (
        <li key={child.id}>
          <Link href={nodeHref(courseSlug, child)} className="flex items-baseline gap-2 px-3 py-2 hover:bg-accent">
            <span className="shrink-0 text-sm text-muted-foreground tabular-nums">{child.label}</span>
            <span className="min-w-0 text-primary">{child.title}</span>
            {child.effectiveVisibility === "private" && <Lock className="size-3.5 shrink-0 opacity-70" aria-label="Özel" />}
          </Link>
        </li>
      ))}
    </ul>
  );
}

function Pager({ courseSlug, context }: { courseSlug: string; context: LectureNodeContext }) {
  const { previous, next } = context;
  if (!previous && !next) return null;
  const cell = "flex min-w-0 flex-1 items-center gap-2 rounded-lg border bg-card p-3 hover:border-primary/50";
  return (
    <nav aria-label="Başlıklar arası gezinme" className="mt-8 flex flex-col gap-2 sm:flex-row print:hidden">
      {previous ? (
        <Link href={nodeHref(courseSlug, previous)} className={cell} aria-label={`Önceki başlık: ${previous.label} ${previous.title}`}>
          <ChevronLeft className="size-5 shrink-0 text-muted-foreground" aria-hidden />
          <span className="min-w-0">
            <span className="block text-xs text-muted-foreground">Önceki</span>
            <span className="block truncate text-primary">
              {previous.label} {previous.title}
            </span>
          </span>
        </Link>
      ) : (
        <span className="hidden flex-1 sm:block" />
      )}
      {next ? (
        <Link
          href={nodeHref(courseSlug, next)}
          className={`${cell} justify-end text-right`}
          aria-label={`Sonraki başlık: ${next.label} ${next.title}`}
        >
          <span className="min-w-0">
            <span className="block text-xs text-muted-foreground">Sonraki</span>
            <span className="block truncate text-primary">
              {next.label} {next.title}
            </span>
          </span>
          <ChevronRight className="size-5 shrink-0 text-muted-foreground" aria-hidden />
        </Link>
      ) : (
        <span className="hidden flex-1 sm:block" />
      )}
    </nav>
  );
}

/** Başlık sayfası: ders yolu, numara ve ad, metin, belgeler, alt başlıklar, önceki/sonraki. */
export function NodeView({
  note,
  context,
  course,
}: {
  note: LectureNoteDetail;
  context: LectureNodeContext;
  course: LectureCourseDetail;
}) {
  const courseSlug = context.course.slug;
  const isEmpty = !note.html && note.files.length === 0;
  return (
    <article className="mx-auto w-full max-w-3xl px-4 py-6">
      <Breadcrumb className="print:hidden">
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink asChild>
              <Link href="/ders-notlari">Ders Notları</Link>
            </BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink asChild>
              <Link href={`/ders-notlari/${courseSlug}`}>{context.course.title}</Link>
            </BreadcrumbLink>
          </BreadcrumbItem>
          {context.path.map((ancestor) => (
            <Fragment key={ancestor.id}>
              <BreadcrumbSeparator />
              <BreadcrumbItem>
                <BreadcrumbLink asChild>
                  <Link href={nodeHref(courseSlug, ancestor)}>
                    {ancestor.label} {ancestor.title}
                  </Link>
                </BreadcrumbLink>
              </BreadcrumbItem>
            </Fragment>
          ))}
        </BreadcrumbList>
      </Breadcrumb>

      <header className="mt-2 flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-lg text-muted-foreground tabular-nums">{context.label}</p>
          <h1 className="text-3xl leading-tight md:text-4xl">{note.title}</h1>
          <p className="mt-2 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
            <LectureNoteScopeBadges scope={note.scope} />
            {context.effectiveVisibility === "private" && <PrivateBadge />}
          </p>
        </div>
        <NodeAdminActions note={note} context={context} course={course} />
      </header>

      {note.html && <RichTextView html={note.html} className="mt-6 rounded-lg border bg-card p-4 md:p-6" />}
      {isEmpty && <p className="mt-6 text-muted-foreground">Bu başlığa henüz içerik girilmedi.</p>}
      <LectureNoteFileList files={note.files} />

      {context.children.length > 0 && (
        <section className="mt-6" aria-labelledby="alt-basliklar">
          <h2 id="alt-basliklar" className="text-xl">
            Alt başlıklar
          </h2>
          <NodeLinkList courseSlug={courseSlug} nodes={context.children} />
        </section>
      )}

      <Pager courseSlug={courseSlug} context={context} />
    </article>
  );
}
