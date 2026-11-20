using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Myss.Api.Migrations.Attachments
{
    /// <inheritdoc />
    public partial class AttachmentSubmissionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_attachments_submission_id",
                schema: "attachments",
                table: "attachments",
                column: "submission_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_attachments_submission_id",
                schema: "attachments",
                table: "attachments");
        }
    }
}
