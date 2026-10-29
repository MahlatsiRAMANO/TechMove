using Microsoft.EntityFrameworkCore;
using TechMove.Api.Models;

namespace TechMove.Api.Data;

/// <summary>
/// EF Core context. All table rules (keys, lengths, precision, relationships, constraints)
/// are configured with the Fluent API so the schema is explicit and normalised.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Client>(e =>
        {
            e.ToTable("Clients");
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired().HasMaxLength(100);
            e.Property(c => c.ContactDetails).IsRequired().HasMaxLength(200);
            e.Property(c => c.Region).IsRequired().HasMaxLength(50);
            e.HasIndex(c => c.Name);
        });

        modelBuilder.Entity<Contract>(e =>
        {
            e.ToTable("Contracts", t =>
                t.HasCheckConstraint("CK_Contracts_EndAfterStart", "[EndDate] > [StartDate]"));
            e.HasKey(c => c.Id);
            e.Property(c => c.StartDate).HasColumnType("date");
            e.Property(c => c.EndDate).HasColumnType("date");
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(c => c.ServiceLevel).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(c => c.SignedAgreementFileName).HasMaxLength(255);
            e.Property(c => c.SignedAgreementStoredName).HasMaxLength(100);
            e.Ignore(c => c.HasSignedAgreement);

            // One Client -> many Contracts. Restrict: you cannot delete a client that still has contracts.
            e.HasOne(c => c.Client)
                .WithMany(cl => cl.Contracts)
                .HasForeignKey(c => c.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Supports the admin search by status and date range.
            e.HasIndex(c => new { c.Status, c.StartDate, c.EndDate });
        });

        modelBuilder.Entity<ServiceRequest>(e =>
        {
            e.ToTable("ServiceRequests", t =>
            {
                t.HasCheckConstraint("CK_ServiceRequests_CostUsd_NonNegative", "[CostUsd] >= 0");
                t.HasCheckConstraint("CK_ServiceRequests_Rate_Positive", "[ExchangeRate] > 0");
            });
            e.HasKey(s => s.Id);
            e.Property(s => s.Description).IsRequired().HasMaxLength(500);
            e.Property(s => s.CostUsd).HasPrecision(18, 2);
            e.Property(s => s.ExchangeRate).HasPrecision(18, 6);
            e.Property(s => s.Cost).HasPrecision(18, 2);
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

            // One Contract -> many ServiceRequests.
            e.HasOne(s => s.Contract)
                .WithMany(c => c.ServiceRequests)
                .HasForeignKey(s => s.ContractId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("Users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Username).IsRequired().HasMaxLength(50);
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.PasswordHash).IsRequired().HasMaxLength(200);
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20).IsRequired();

            // A customer belongs to (at most) one client company. Deleting the client unlinks the user.
            e.HasOne(u => u.Client)
                .WithMany()
                .HasForeignKey(u => u.ClientId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        SeedData.Apply(modelBuilder);
    }
}
