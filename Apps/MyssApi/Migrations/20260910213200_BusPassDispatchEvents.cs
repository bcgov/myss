using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Myss.Api.Migrations
{
    /// <inheritdoc />
    public partial class BusPassDispatchEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bus_pass_dispatches",
                schema: "forms");

            migrationBuilder.CreateTable(
                name: "bus_pass_dispatch_events",
                schema: "forms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    request_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    reference_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    error_message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bus_pass_dispatch_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_bus_pass_dispatch_events_form_submissions_submission_id",
                        column: x => x.submission_id,
                        principalSchema: "forms",
                        principalTable: "form_submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bus_pass_dispatch_events_attempt_id",
                schema: "forms",
                table: "bus_pass_dispatch_events",
                column: "attempt_id");

            migrationBuilder.CreateIndex(
                name: "ix_bus_pass_dispatch_events_submission_id",
                schema: "forms",
                table: "bus_pass_dispatch_events",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "ux_bus_pass_dispatch_events_accepted",
                schema: "forms",
                table: "bus_pass_dispatch_events",
                column: "submission_id",
                unique: true,
                filter: "type = 'Accepted'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bus_pass_dispatch_events",
                schema: "forms");

            migrationBuilder.CreateTable(
                name: "bus_pass_dispatches",
                schema: "forms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    error_message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reference_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bus_pass_dispatches", x => x.id);
                    table.ForeignKey(
                        name: "FK_bus_pass_dispatches_form_submissions_submission_id",
                        column: x => x.submission_id,
                        principalSchema: "forms",
                        principalTable: "form_submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bus_pass_dispatches_submission_id",
                schema: "forms",
                table: "bus_pass_dispatches",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "ux_bus_pass_dispatches_accepted",
                schema: "forms",
                table: "bus_pass_dispatches",
                column: "submission_id",
                unique: true,
                filter: "outcome = 'Accepted'");
        }
    }
}
