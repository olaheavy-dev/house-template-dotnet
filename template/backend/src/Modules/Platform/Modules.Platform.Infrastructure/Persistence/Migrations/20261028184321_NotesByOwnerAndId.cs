using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotesByOwnerAndId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notes_OwnerId",
                schema: "platform",
                table: "notes");

            migrationBuilder.CreateIndex(
                name: "IX_notes_OwnerId_Id",
                schema: "platform",
                table: "notes",
                columns: new[] { "OwnerId", "Id" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notes_OwnerId_Id",
                schema: "platform",
                table: "notes");

            migrationBuilder.CreateIndex(
                name: "IX_notes_OwnerId",
                schema: "platform",
                table: "notes",
                column: "OwnerId");
        }
    }
}
