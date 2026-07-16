using CanteenConnect.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CanteenConnect.API.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Profile> Profiles { get; set; }
    public DbSet<MenuItem> MenuItems { get; set; }
    public DbSet<Slot> Slots { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<WalletTransaction> WalletTransactions { get; set; }
    public DbSet<Rating> Ratings { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Rating>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.OrderId).IsUnique();
            e.HasOne(r => r.Order)
             .WithMany()
             .HasForeignKey(r => r.OrderId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Profile
        builder.Entity<Profile>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.UserId).IsUnique();
            e.HasOne<AppUser>()
             .WithOne()
             .HasForeignKey<Profile>(p => p.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // MenuItem
        builder.Entity<MenuItem>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Price).HasPrecision(10, 2);
            e.Property(m => m.Rating).HasPrecision(3, 1);
        });

        // Slot — date+time unique
        builder.Entity<Slot>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.SlotDate, s.SlotTime }).IsUnique();
        });

        // Order
        builder.Entity<Order>(e =>
        {
            e.HasKey(o => o.Id);
            e.HasIndex(o => o.QrToken).IsUnique();
            e.Property(o => o.TotalAmount).HasPrecision(10, 2);
            e.HasOne<AppUser>()
             .WithMany()
             .HasForeignKey(o => o.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(o => o.Slot)
             .WithMany()
             .HasForeignKey(o => o.SlotId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // OrderItem
        builder.Entity<OrderItem>(e =>
        {
            e.HasKey(oi => oi.Id);
            e.Property(oi => oi.UnitPrice).HasPrecision(10, 2);
            e.HasOne(oi => oi.Order)
             .WithMany(o => o.Items)
             .HasForeignKey(oi => oi.OrderId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(oi => oi.MenuItem)
             .WithMany()
             .HasForeignKey(oi => oi.MenuItemId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // WalletTransaction
        builder.Entity<WalletTransaction>(e =>
        {
            e.HasKey(w => w.Id);
            e.Property(w => w.Amount).HasPrecision(10, 2);
            e.HasOne<AppUser>()
             .WithMany()
             .HasForeignKey(w => w.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}