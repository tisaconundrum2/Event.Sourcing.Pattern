using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ConferenceManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attendee_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    attendee_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    booked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "conferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    total_seats = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conferences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sequence_number = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "seat_availability",
                columns: table => new
                {
                    conference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conference_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    total_seats = table.Column<int>(type: "integer", nullable: false),
                    booked_seats = table.Column<int>(type: "integer", nullable: false),
                    last_event_sequence = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seat_availability", x => x.conference_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_conference_id",
                table: "bookings",
                column: "conference_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_conference_id",
                table: "events",
                column: "conference_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_sequence",
                table: "events",
                column: "sequence_number");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "conferences");

            migrationBuilder.DropTable(
                name: "events");

            migrationBuilder.DropTable(
                name: "seat_availability");
        }
    }
}
