using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CezaHukuku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NewsQuizFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "favorites",
                columns: table => new
                {
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_favorites", x => new { x.owner_id, x.article_id });
                    table.ForeignKey(
                        name: "fk_favorites_articles_article_id",
                        column: x => x.article_id,
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_favorites_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "news_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    published_on = table.Column<DateOnly>(type: "date", nullable: false),
                    visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_news_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quiz_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    law_id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: true),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    options = table.Column<string[]>(type: "text[]", nullable: false),
                    correct_index = table.Column<int>(type: "integer", nullable: false),
                    explanation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quiz_questions", x => x.id);
                    table.ForeignKey(
                        name: "fk_quiz_questions_articles_article_id",
                        column: x => x.article_id,
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quiz_questions_laws_law_id",
                        column: x => x.law_id,
                        principalTable: "laws",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_favorites_article_id",
                table: "favorites",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "ix_news_items_published_on",
                table: "news_items",
                column: "published_on");

            migrationBuilder.CreateIndex(
                name: "ix_quiz_questions_article_id",
                table: "quiz_questions",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "ix_quiz_questions_law_id",
                table: "quiz_questions",
                column: "law_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "favorites");

            migrationBuilder.DropTable(
                name: "news_items");

            migrationBuilder.DropTable(
                name: "quiz_questions");
        }
    }
}
