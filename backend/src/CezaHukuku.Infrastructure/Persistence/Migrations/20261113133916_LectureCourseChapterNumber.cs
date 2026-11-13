using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CezaHukuku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LectureCourseChapterNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "chapter_number",
                table: "lecture_courses",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "chapter_number",
                table: "lecture_courses");
        }
    }
}
