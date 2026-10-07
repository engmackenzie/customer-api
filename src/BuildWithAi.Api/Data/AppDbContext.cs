using BuildWithAi.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace BuildWithAi.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var customer = modelBuilder.Entity<Customer>();

        customer.HasKey(customer => customer.Id);
        customer.Property(customer => customer.FirstName).HasMaxLength(100).IsRequired();
        customer.Property(customer => customer.LastName).HasMaxLength(100).IsRequired();
        customer.Property(customer => customer.Email)
            .HasMaxLength(254)
            .UseCollation("Latin1_General_100_CI_AS")
            .IsRequired();
        customer.HasIndex(customer => customer.Email).IsUnique();
        customer.Property(customer => customer.PhoneNumber).HasMaxLength(32).IsRequired();
        customer.Property(customer => customer.CreatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}
