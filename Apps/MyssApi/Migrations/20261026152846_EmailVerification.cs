using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Myss.Api.Migrations
{
    /// <inheritdoc />
    public partial class EmailVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "email_verification_requested_at",
                schema: "forms",
                table: "myss_user_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email_verification_token_hash",
                schema: "forms",
                table: "myss_user_profiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "email_verified_at",
                schema: "forms",
                table: "myss_user_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pending_email",
                schema: "forms",
                table: "myss_user_profiles",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_myss_user_profiles_email_verification_token_hash",
                schema: "forms",
                table: "myss_user_profiles",
                column: "email_verification_token_hash",
                unique: true);

            // Profiles from before verification existed (MYSS-305) keep their
            // address as verified: they have been using it, and nobody is sent
            // a surprise link. Their registration time stands in for when.
            migrationBuilder.Sql(
                "UPDATE forms.myss_user_profiles SET email_verified_at = created_at WHERE email_verified_at IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_myss_user_profiles_email_verification_token_hash",
                schema: "forms",
                table: "myss_user_profiles");

            migrationBuilder.DropColumn(
                name: "email_verification_requested_at",
                schema: "forms",
                table: "myss_user_profiles");

            migrationBuilder.DropColumn(
                name: "email_verification_token_hash",
                schema: "forms",
                table: "myss_user_profiles");

            migrationBuilder.DropColumn(
                name: "email_verified_at",
                schema: "forms",
                table: "myss_user_profiles");

            migrationBuilder.DropColumn(
                name: "pending_email",
                schema: "forms",
                table: "myss_user_profiles");
        }
    }
}
