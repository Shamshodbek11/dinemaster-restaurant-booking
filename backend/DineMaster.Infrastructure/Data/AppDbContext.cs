using DineMaster.Application.Abstractions;
using DineMaster.Domain.Entities;
using DineMaster.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DineMaster.Infrastructure.Data;

public class AppDbContext : DbContext, IAppDbContext
{
    private readonly INotificationPublisher? _publisher;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>DI: saqlangan bildirishnomalar tashqi kanallarga (Telegram) ham uzatiladi.</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options, INotificationPublisher publisher) : base(options)
    {
        _publisher = publisher;
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationItem> ReservationItems => Set<ReservationItem>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<VenueReservation> VenueReservations => Set<VenueReservation>();
    public DbSet<YumsAudit> YumsAudits => Set<YumsAudit>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<RestaurantTable> RestaurantTables => Set<RestaurantTable>();
    public DbSet<Waitlist> Waitlist => Set<Waitlist>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<EmailCode> EmailCodes => Set<EmailCode>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PartnerApplication> PartnerApplications => Set<PartnerApplication>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RestaurantStaff> RestaurantStaff => Set<RestaurantStaff>();

    // IAppDbContext: DatabaseFacade
    DatabaseFacade IAppDbContext.Database => base.Database;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.HasOne(x => x.Profile)
                .WithOne(p => p.User)
                .HasForeignKey<Profile>(p => p.Id)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserRoleEntity>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Role }).IsUnique();
            e.Property(x => x.Role).HasConversion<string>();
            e.HasOne(x => x.User)
                .WithMany(u => u.Roles)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Restaurant>(e =>
        {
            e.HasOne(x => x.Owner)
                .WithMany()
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.IsApproved);
            e.HasIndex(x => x.City);
            e.HasIndex(x => x.OwnerId);
            e.HasIndex(x => x.CuisineType);
            e.HasIndex(x => new { x.IsApproved, x.IsSuspended });
        });

        builder.Entity<Reservation>(e =>
        {
            e.HasOne(x => x.User)
                .WithMany(u => u.Reservations)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Restaurant)
                .WithMany(r => r.Reservations)
                .HasForeignKey(x => x.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Table)
                .WithMany()
                .HasForeignKey(x => x.TableId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.RestaurantId, x.Date });
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Date);
            e.HasIndex(x => x.TableId);
        });

        builder.Entity<ReservationItem>(e =>
        {
            e.HasKey(x => new { x.ReservationId, x.MenuItemId });
            e.HasOne(x => x.Reservation)
                .WithMany(r => r.Items)
                .HasForeignKey(x => x.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.MenuItem)
                .WithMany()
                .HasForeignKey(x => x.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Review>(e =>
        {
            e.HasOne(x => x.User)
                .WithMany(u => u.Reviews)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Restaurant)
                .WithMany(r => r.Reviews)
                .HasForeignKey(x => x.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.RestaurantId);
            e.HasIndex(x => x.UserId);
            // BUG FIX: Bir foydalanuvchi bir restoranga faqat bir sharh
            e.HasIndex(x => new { x.UserId, x.RestaurantId }).IsUnique();
        });

        builder.Entity<Favorite>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.RestaurantId }).IsUnique();
            e.HasOne(x => x.User)
                .WithMany(u => u.Favorites)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Restaurant)
                .WithMany(r => r.Favorites)
                .HasForeignKey(x => x.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RestaurantStaff>(e =>
        {
            e.HasOne(x => x.Restaurant)
                .WithMany(r => r.Staff)
                .HasForeignKey(x => x.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.RestaurantId, x.UserId }).IsUnique();
            e.HasIndex(x => x.UserId);
        });

        builder.Entity<Profile>(e =>
        {
            e.HasIndex(x => x.ReferralCode).IsUnique().HasFilter("referral_code IS NOT NULL");
            e.HasIndex(x => x.TelegramLinkCode).IsUnique().HasFilter("telegram_link_code IS NOT NULL");
            e.HasIndex(x => x.ReferredBy);
        });

        builder.Entity<Venue>(e =>
        {
            e.HasOne(x => x.Owner)
                .WithMany()
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.IsApproved);
        });

        builder.Entity<VenueReservation>(e =>
        {
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Venue)
                .WithMany(v => v.Reservations)
                .HasForeignKey(x => x.VenueId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<YumsAudit>(e =>
        {
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.CreatedAt);
        });

        builder.Entity<MenuCategory>(e =>
        {
            e.HasOne(x => x.Restaurant)
                .WithMany(r => r.MenuCategories)
                .HasForeignKey(x => x.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.RestaurantId);
        });

        builder.Entity<MenuItem>(e =>
        {
            e.HasOne(x => x.Category)
                .WithMany(c => c.Items)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Restaurant)
                .WithMany(r => r.MenuItems)
                .HasForeignKey(x => x.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.RestaurantId);
            e.HasIndex(x => x.CategoryId);
            e.HasIndex(x => x.IsAvailable);
        });

        builder.Entity<RestaurantTable>(e =>
        {
            e.HasOne(x => x.Restaurant)
                .WithMany(r => r.Tables)
                .HasForeignKey(x => x.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.RestaurantId);
            e.HasIndex(x => new { x.RestaurantId, x.Capacity });
        });

        builder.Entity<Waitlist>(e =>
        {
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Restaurant)
                .WithMany(r => r.WaitlistEntries)
                .HasForeignKey(x => x.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.RestaurantId, x.Date });
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.Status);
        });

        builder.Entity<Notification>(e =>
        {
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.IsRead });
            e.HasIndex(x => x.CreatedAt);
        });

        builder.Entity<EmailCode>(e =>
        {
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.Purpose });
            e.HasIndex(x => x.ExpiresAt);
        });

        builder.Entity<PartnerApplication>(e =>
        {
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => x.UserId);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => new { x.RestaurantId, x.CreatedAt });
            e.HasIndex(x => x.EntityType);
            e.HasIndex(x => x.ActorId);
        });

        builder.Entity<Payment>(e =>
        {
            e.Property(x => x.Number).UseIdentityAlwaysColumn();
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => new { x.Provider, x.ProviderTransactionId })
                .IsUnique()
                .HasFilter("provider_transaction_id IS NOT NULL");
            e.HasIndex(x => x.ReservationId);
            // To'lov tarixi bron o'chirilganda yo'qolmasin
            e.HasOne(x => x.Reservation)
                .WithMany()
                .HasForeignKey(x => x.ReservationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        var added = _publisher is null ? null : ChangeTracker.Entries<Notification>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => new NotificationMessage(e.Entity.UserId, e.Entity.Title, e.Entity.Body, e.Entity.Link))
            .ToList();
        var result = await base.SaveChangesAsync(cancellationToken);
        // Tranzaksiya ichida bo'lsak ham — xabar faqat saqlangandan keyin; rollback bo'lsa ham zarari kam
        // (Telegram'ga takroriy xabar emas, bitta ortiqcha xabar bo'lishi mumkin)
        if (added is { Count: > 0 } && Database.CurrentTransaction is null) _publisher!.Publish(added);
        else if (added is { Count: > 0 }) _pendingAfterCommit.AddRange(added);
        return result;
    }

    // Tranzaksiya ichida saqlangan bildirishnomalar — commit'dan keyin yuboriladi
    private readonly List<NotificationMessage> _pendingAfterCommit = new();

    /// <summary>Tranzaksiya commit qilingach chaqiriladi (BookingService/PaymentService).</summary>
    public void FlushNotifications()
    {
        if (_publisher is null || _pendingAfterCommit.Count == 0) return;
        _publisher.Publish(_pendingAfterCommit.ToList());
        _pendingAfterCommit.Clear();
    }

    public void DiscardPendingNotifications() => _pendingAfterCommit.Clear();

    private void UpdateTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Properties.Any(p => p.Metadata.Name == "CreatedAt"))
                    entry.Property("CreatedAt").CurrentValue = now;
                if (entry.Properties.Any(p => p.Metadata.Name == "UpdatedAt"))
                    entry.Property("UpdatedAt").CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                if (entry.Properties.Any(p => p.Metadata.Name == "UpdatedAt"))
                    entry.Property("UpdatedAt").CurrentValue = now;
            }
        }
    }
}
