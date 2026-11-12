using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CezaHukuku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LectureCourses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "course_id",
                table: "lecture_notes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_id",
                table: "lecture_notes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "lecture_notes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "lecture_courses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lecture_courses", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lecture_notes_course_id_parent_id_sort_order",
                table: "lecture_notes",
                columns: new[] { "course_id", "parent_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_lecture_notes_parent_id",
                table: "lecture_notes",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_lecture_courses_slug",
                table: "lecture_courses",
                column: "slug",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_lecture_notes_lecture_courses_course_id",
                table: "lecture_notes",
                column: "course_id",
                principalTable: "lecture_courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_lecture_notes_lecture_notes_parent_id",
                table: "lecture_notes",
                column: "parent_id",
                principalTable: "lecture_notes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_lecture_notes_lecture_courses_course_id",
                table: "lecture_notes");

            migrationBuilder.DropForeignKey(
                name: "fk_lecture_notes_lecture_notes_parent_id",
                table: "lecture_notes");

            migrationBuilder.DropTable(
                name: "lecture_courses");

            migrationBuilder.DropIndex(
                name: "ix_lecture_notes_course_id_parent_id_sort_order",
                table: "lecture_notes");

            migrationBuilder.DropIndex(
                name: "ix_lecture_notes_parent_id",
                table: "lecture_notes");

            migrationBuilder.DropColumn(
                name: "course_id",
                table: "lecture_notes");

            migrationBuilder.DropColumn(
                name: "parent_id",
                table: "lecture_notes");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "lecture_notes");
        }
    }
}
