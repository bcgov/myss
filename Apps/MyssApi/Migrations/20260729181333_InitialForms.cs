using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Myss.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "forms");

            migrationBuilder.CreateTable(
                name: "form_submissions",
                schema: "forms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_spec_id = table.Column<string>(type: "text", nullable: false),
                    form_spec_version = table.Column<int>(type: "integer", nullable: false),
                    answers = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_form_submissions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_form_submissions_form_spec_id_form_spec_version",
                schema: "forms",
                table: "form_submissions",
                columns: new[] { "form_spec_id", "form_spec_version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "form_submissions",
                schema: "forms");
        }
    }
}
