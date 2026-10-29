using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechMove.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "ClientId", "CreatedAtUtc", "PasswordHash", "Role", "Username" },
                values: new object[,]
                {
                    { 1, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "100000.y1tko+eUTpxwgJL7otFRPA==.EdMzdFhw401WrjeT9j7751PKAk4O+qtBfpTDXUh9i34=", "Admin", "admin" },
                    { 2, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "100000.hUcA+UwZAgPhdjH0u7bqlw==.VfFWigawQsHjmkpiqfXP0v+SLxdnZtuw+o1tl789Ydk=", "Staff", "staff" },
                    { 3, 1, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "100000.HsPhJZ/pweaYjwbh4bBONA==.Nyhtm8EBdtd7OZgVsmNxYWibjTQYD6v0frss1njy5Ic=", "Customer", "acme" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ClientId",
                table: "Users",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
