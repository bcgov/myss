using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Myss.Api.Migrations.Intake
{
    /// <inheritdoc />
    public partial class ApplicationChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "channel",
                schema: "intake",
                table: "application_answers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "client");

            // Only a worker override owns its application under this prefix, and until
            // now only a worker override started applications on this form.
            migrationBuilder.Sql(
                "UPDATE intake.application_answers SET channel = 'worker-override' "
                + "WHERE owner_subject LIKE 'worker-override:%' OR form_spec_id = 'worker-override-afa';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "channel",
                schema: "intake",
                table: "application_answers");
        }
    }
}
