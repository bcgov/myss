using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Myss.Api.Migrations
{
    /// <inheritdoc />
    public partial class PinResetToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "pin_reset_requested_at",
                schema: "forms",
                table: "myss_user_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pin_reset_token_hash",
                schema: "forms",
                table: "myss_user_profiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_myss_user_profiles_pin_reset_token_hash",
                schema: "forms",
                table: "myss_user_profiles",
                column: "pin_reset_token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_myss_user_profiles_pin_reset_token_hash",
                schema: "forms",
                table: "myss_user_profiles");

            migrationBuilder.DropColumn(
                name: "pin_reset_requested_at",
                schema: "forms",
                table: "myss_user_profiles");

            migrationBuilder.DropColumn(
                name: "pin_reset_token_hash",
                schema: "forms",
                table: "myss_user_profiles");
        }
    }
}
