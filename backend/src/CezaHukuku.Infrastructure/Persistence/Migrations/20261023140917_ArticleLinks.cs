using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CezaHukuku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArticleLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "article_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_article_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    external_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_article_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_article_links_articles_source_article_id",
                        column: x => x.source_article_id,
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_article_links_articles_target_article_id",
                        column: x => x.target_article_id,
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_article_links_source_article_id",
                table: "article_links",
                column: "source_article_id");

            migrationBuilder.CreateIndex(
                name: "ix_article_links_target_article_id",
                table: "article_links",
                column: "target_article_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "article_links");
        }
    }
}
