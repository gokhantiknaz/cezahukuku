using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace CezaHukuku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "search_docs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    law_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    node_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    context = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: true),
                    title_norm = table.Column<string>(type: "text", nullable: false),
                    body_norm = table.Column<string>(type: "text", nullable: false),
                    title_fold = table.Column<string>(type: "text", nullable: false),
                    body_fold = table.Column<string>(type: "text", nullable: false),
                    search = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "setweight(to_tsvector('turkish'::regconfig, title_norm), 'A')\n|| setweight(to_tsvector('turkish'::regconfig, body_norm), 'B')\n|| setweight(to_tsvector('simple'::regconfig, title_fold), 'A')\n|| setweight(to_tsvector('simple'::regconfig, body_fold), 'B')", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_search_docs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_search_docs_kind_source_id",
                table: "search_docs",
                columns: new[] { "kind", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_search_docs_law_ids",
                table: "search_docs",
                column: "law_ids")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_search_docs_node_ids",
                table: "search_docs",
                column: "node_ids")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_search_docs_owner_id",
                table: "search_docs",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_search_docs_search",
                table: "search_docs",
                column: "search")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "search_docs");
        }
    }
}
