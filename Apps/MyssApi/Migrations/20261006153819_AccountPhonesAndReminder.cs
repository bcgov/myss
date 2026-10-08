using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Myss.Api.Migrations
{
    /// <inheritdoc />
    public partial class AccountPhonesAndReminder : Migration
    {
        // Hand-edited from the generated `new[] { ... }` argument (CA1861).
        private static readonly string[] ProfileIdAndTypeColumns = ["profile_id", "type"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "monthly_report_reminder",
                schema: "forms",
                table: "myss_user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "myss_user_phones",
                schema: "forms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_myss_user_phones", x => x.id);
                    table.ForeignKey(
                        name: "FK_myss_user_phones_myss_user_profiles_profile_id",
                        column: x => x.profile_id,
                        principalSchema: "forms",
                        principalTable: "myss_user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_myss_user_phones_profile_id_type",
                schema: "forms",
                table: "myss_user_phones",
                columns: ProfileIdAndTypeColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "myss_user_phones",
                schema: "forms");

            migrationBuilder.DropColumn(
                name: "monthly_report_reminder",
                schema: "forms",
                table: "myss_user_profiles");
        }
    }
}
