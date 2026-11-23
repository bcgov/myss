using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Myss.Api.Migrations
{
    /// <inheritdoc />
    public partial class AccountCreationContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "account_creation_context",
                schema: "forms",
                table: "myss_user_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "helper_authorized",
                schema: "forms",
                table: "myss_user_profiles",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "account_documents",
                schema: "forms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attachment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    attached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_account_documents_myss_user_profiles_profile_id",
                        column: x => x.profile_id,
                        principalSchema: "forms",
                        principalTable: "myss_user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "account_representatives",
                schema: "forms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    first_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    last_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    agency = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_representatives", x => x.id);
                    table.ForeignKey(
                        name: "FK_account_representatives_myss_user_profiles_profile_id",
                        column: x => x.profile_id,
                        principalSchema: "forms",
                        principalTable: "myss_user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_documents_attachment_id",
                schema: "forms",
                table: "account_documents",
                column: "attachment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_documents_profile_id",
                schema: "forms",
                table: "account_documents",
                column: "profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_account_representatives_profile_id",
                schema: "forms",
                table: "account_representatives",
                column: "profile_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_documents",
                schema: "forms");

            migrationBuilder.DropTable(
                name: "account_representatives",
                schema: "forms");

            migrationBuilder.DropColumn(
                name: "account_creation_context",
                schema: "forms",
                table: "myss_user_profiles");

            migrationBuilder.DropColumn(
                name: "helper_authorized",
                schema: "forms",
                table: "myss_user_profiles");
        }
    }
}
