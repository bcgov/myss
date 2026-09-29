using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Myss.Api.Migrations.Intake
{
    /// <inheritdoc />
    public partial class InitialIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "intake");

            migrationBuilder.CreateTable(
                name: "application_answers",
                schema: "intake",
                columns: table => new
                {
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    form_spec_id = table.Column<string>(type: "text", nullable: false),
                    form_spec_version = table.Column<int>(type: "integer", nullable: false),
                    answers = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_answers", x => x.application_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_answers_owner_subject",
                schema: "intake",
                table: "application_answers",
                column: "owner_subject");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_answers",
                schema: "intake");
        }
    }
}
