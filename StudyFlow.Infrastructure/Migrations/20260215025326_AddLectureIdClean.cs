using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyFlow.Infrastructure.Migrations
{
    public partial class AddLectureIdClean : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LectureId",
                table: "Questions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_LectureId",
                table: "Questions",
                column: "LectureId");

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Lectures_LectureId",
                table: "Questions",
                column: "LectureId",
                principalTable: "Lectures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Lectures_LectureId",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_LectureId",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "LectureId",
                table: "Questions");
        }
    }
}