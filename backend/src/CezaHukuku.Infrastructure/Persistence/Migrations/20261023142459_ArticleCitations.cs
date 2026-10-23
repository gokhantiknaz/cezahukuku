using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CezaHukuku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArticleCitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "article_citations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paragraph_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    start = table.Column<int>(type: "integer", nullable: false),
                    length = table.Column<int>(type: "integer", nullable: false),
                    target_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    target_law = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    target_article_slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_article_citations", x => x.id);
                    table.ForeignKey(
                        name: "fk_article_citations_article_versions_source_version_id",
                        column: x => x.source_version_id,
                        principalTable: "article_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_article_citations_articles_source_article_id",
                        column: x => x.source_article_id,
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_article_citations_source_article_id",
                table: "article_citations",
                column: "source_article_id");

            migrationBuilder.CreateIndex(
                name: "ix_article_citations_source_version_id",
                table: "article_citations",
                column: "source_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_article_citations_target_article_slug",
                table: "article_citations",
                column: "target_article_slug");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "article_citations");
        }
    }
}
