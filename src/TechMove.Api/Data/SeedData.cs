using Microsoft.EntityFrameworkCore;
using TechMove.Api.Models;

namespace TechMove.Api.Data;

/// <summary>Demo data so the app has something to show straight after the first migration.</summary>
internal static class SeedData
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>().HasData(
            new Client { Id = 1, Name = "Acme Logistics", ContactDetails = "ops@acmelogistics.co.za | 011 555 0101", Region = "Gauteng" },
            new Client { Id = 2, Name = "Cape Freight Co", ContactDetails = "info@capefreight.co.za | 021 555 0202", Region = "Western Cape" },
            new Client { Id = 3, Name = "Durban Port Services", ContactDetails = "admin@dps.co.za | 031 555 0303", Region = "KwaZulu-Natal" });

        modelBuilder.Entity<Contract>().HasData(
            new Contract { Id = 1, ClientId = 1, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2027, 12, 31), Status = ContractStatus.Active, ServiceLevel = ServiceLevel.Gold },
            new Contract { Id = 2, ClientId = 2, StartDate = new DateTime(2024, 1, 1), EndDate = new DateTime(2025, 12, 31), Status = ContractStatus.Expired, ServiceLevel = ServiceLevel.Silver },
            new Contract { Id = 3, ClientId = 3, StartDate = new DateTime(2026, 3, 1), EndDate = new DateTime(2027, 2, 28), Status = ContractStatus.OnHold, ServiceLevel = ServiceLevel.Platinum },
            new Contract { Id = 4, ClientId = 1, StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2027, 10, 31), Status = ContractStatus.Draft, ServiceLevel = ServiceLevel.Bronze });

        // Demo accounts - passwords: admin/Admin@123, staff/Staff@123, acme/Customer@123 (PBKDF2 hashes).
        var created = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<AppUser>().HasData(
            new AppUser { Id = 1, Username = "admin", Role = UserRole.Admin, CreatedAtUtc = created,
                PasswordHash = "100000.y1tko+eUTpxwgJL7otFRPA==.EdMzdFhw401WrjeT9j7751PKAk4O+qtBfpTDXUh9i34=" },
            new AppUser { Id = 2, Username = "staff", Role = UserRole.Staff, CreatedAtUtc = created,
                PasswordHash = "100000.hUcA+UwZAgPhdjH0u7bqlw==.VfFWigawQsHjmkpiqfXP0v+SLxdnZtuw+o1tl789Ydk=" },
            new AppUser { Id = 3, Username = "acme", Role = UserRole.Customer, ClientId = 1, CreatedAtUtc = created,
                PasswordHash = "100000.HsPhJZ/pweaYjwbh4bBONA==.Nyhtm8EBdtd7OZgVsmNxYWibjTQYD6v0frss1njy5Ic=" });
    }
}
