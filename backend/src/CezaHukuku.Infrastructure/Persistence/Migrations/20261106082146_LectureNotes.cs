using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CezaHukuku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LectureNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lecture_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    html = table.Column<string>(type: "text", nullable: false),
                    plain_text = table.Column<string>(type: "text", nullable: false),
                    visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    law_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    law_id = table.Column<Guid>(type: "uuid", nullable: true),
                    article_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lecture_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_lecture_notes_articles_article_id",
                        column: x => x.article_id,
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lecture_notes_law_groups_law_group_id",
                        column: x => x.law_group_id,
                        principalTable: "law_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lecture_notes_laws_law_id",
                        column: x => x.law_id,
                        principalTable: "laws",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lecture_notes_article_id",
                table: "lecture_notes",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "ix_lecture_notes_law_group_id",
                table: "lecture_notes",
                column: "law_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_lecture_notes_law_id",
                table: "lecture_notes",
                column: "law_id");

            migrationBuilder.CreateIndex(
                name: "ix_lecture_notes_slug",
                table: "lecture_notes",
                column: "slug",
                unique: true,
                filter: "deleted_at IS NULL");

            // Kanun ve maddelere girilmiş ders notları yeni bölüme taşınır: bağlantıları korunur, başlığı yoksa
            // kanun/madde adından üretilir; kanunu private ise not da private kalır. Eski kayıtlar silinmiş sayılır.
            migrationBuilder.Sql("""
                INSERT INTO lecture_notes
                    (id, title, slug, html, plain_text, visibility, law_group_id, law_id, article_id, created_at, updated_at)
                SELECT n.id,
                       left(coalesce(nullif(btrim(n.title), ''),
                                     'Ders notu — ' || coalesce(l.short_name, l.number) || coalesce(' Madde ' || a.number, '')), 300),
                       'ders-notu-' || substring(replace(n.id::text, '-', '') from 21 for 12),
                       n.html, n.plain_text,
                       CASE WHEN n.visibility = 'Private' OR l.visibility = 'Private' THEN 'Private' ELSE 'Public' END,
                       l.group_id, n.law_id, n.article_id, n.created_at, n.updated_at
                FROM editorial_notes n
                JOIN laws l ON l.id = n.law_id
                LEFT JOIN articles a ON a.id = n.article_id
                WHERE n.kind = 'DersNotu' AND n.deleted_at IS NULL;

                UPDATE editorial_notes SET deleted_at = now() WHERE kind = 'DersNotu' AND deleted_at IS NULL;

                -- Arama indeksi uygulama açılışında baştan kurulur (boşsa); ders notları yeni adresleriyle girer.
                DELETE FROM search_docs;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lecture_notes");
        }
    }
}
