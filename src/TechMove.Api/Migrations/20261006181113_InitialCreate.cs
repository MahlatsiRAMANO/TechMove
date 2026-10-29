using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TechMove.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContactDetails = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ServiceLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignedAgreementFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SignedAgreementStoredName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SignedAgreementUploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                    table.CheckConstraint("CK_Contracts_EndAfterStart", "[EndDate] > [StartDate]");
                    table.ForeignKey(
                        name: "FK_Contracts_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CostUsd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRequests", x => x.Id);
                    table.CheckConstraint("CK_ServiceRequests_CostUsd_NonNegative", "[CostUsd] >= 0");
                    table.CheckConstraint("CK_ServiceRequests_Rate_Positive", "[ExchangeRate] > 0");
                    table.ForeignKey(
                        name: "FK_ServiceRequests_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Clients",
                columns: new[] { "Id", "ContactDetails", "Name", "Region" },
                values: new object[,]
                {
                    { 1, "ops@acmelogistics.co.za | 011 555 0101", "Acme Logistics", "Gauteng" },
                    { 2, "info@capefreight.co.za | 021 555 0202", "Cape Freight Co", "Western Cape" },
                    { 3, "admin@dps.co.za | 031 555 0303", "Durban Port Services", "KwaZulu-Natal" }
                });

            migrationBuilder.InsertData(
                table: "Contracts",
                columns: new[] { "Id", "ClientId", "EndDate", "ServiceLevel", "SignedAgreementFileName", "SignedAgreementStoredName", "SignedAgreementUploadedAtUtc", "StartDate", "Status" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2027, 12, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), "Gold", null, null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Active" },
                    { 2, 2, new DateTime(2025, 12, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), "Silver", null, null, null, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Expired" },
                    { 3, 3, new DateTime(2027, 2, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), "Platinum", null, null, null, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "OnHold" },
                    { 4, 1, new DateTime(2027, 10, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), "Bronze", null, null, null, new DateTime(2026, 11, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Draft" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Name",
                table: "Clients",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ClientId",
                table: "Contracts",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_Status_StartDate_EndDate",
                table: "Contracts",
                columns: new[] { "Status", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_ContractId",
                table: "ServiceRequests",
                column: "ContractId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceRequests");

            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "Clients");
        }
    }
}
