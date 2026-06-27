using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheLedger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "whatsapp_contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DefaultAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_contacts", x => x.Id);
                });

            // PhoneNumber is GLOBALLY unique: a WhatsApp number maps to exactly one person/household in v1,
            // and inbound resolution looks a sender up by phone alone (no tenant resolved yet). A per-tenant
            // composite would let the same number resolve to an arbitrary tenant's contact, so the uniqueness
            // is on PhoneNumber only.
            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_contacts_PhoneNumber",
                table: "whatsapp_contacts",
                column: "PhoneNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "whatsapp_contacts");
        }
    }
}
