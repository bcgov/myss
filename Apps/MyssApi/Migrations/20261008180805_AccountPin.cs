using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Myss.Api.Migrations
{
    /// <inheritdoc />
    public partial class AccountPin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "pin_failed_attempts",
                schema: "forms",
                table: "myss_user_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "pin_locked_until",
                schema: "forms",
                table: "myss_user_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "taapcd_aae_passcode",
                schema: "forms",
                table: "myss_user_profiles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pin_failed_attempts",
                schema: "forms",
                table: "myss_user_profiles");

            migrationBuilder.DropColumn(
                name: "pin_locked_until",
                schema: "forms",
                table: "myss_user_profiles");

            migrationBuilder.DropColumn(
                name: "taapcd_aae_passcode",
                schema: "forms",
                table: "myss_user_profiles");
        }
    }
}
