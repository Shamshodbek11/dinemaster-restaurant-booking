using DineMaster.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DineMaster.Application.Abstractions;

/// <summary>
/// Application katmani uchun DbContext interfeysi.
/// Infrastructure layerdagi AppDbContext bu interfeysi implement qiladi.
/// </summary>
public interface IAppDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<Profile> Profiles { get; }
    DbSet<UserRoleEntity> UserRoles { get; }
    DbSet<Restaurant> Restaurants { get; }
    DbSet<Reservation> Reservations { get; }
    DbSet<ReservationItem> ReservationItems { get; }
    DbSet<Review> Reviews { get; }
    DbSet<Favorite> Favorites { get; }
    DbSet<Venue> Venues { get; }
    DbSet<VenueReservation> VenueReservations { get; }
    DbSet<MenuCategory> MenuCategories { get; }
    DbSet<MenuItem> MenuItems { get; }
    DbSet<RestaurantTable> RestaurantTables { get; }
    DbSet<Waitlist> Waitlist { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<YumsAudit> YumsAudits { get; }
    DbSet<EmailCode> EmailCodes { get; }
    DbSet<Payment> Payments { get; }
    DbSet<PartnerApplication> PartnerApplications { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<RestaurantStaff> RestaurantStaff { get; }

    DatabaseFacade Database { get; }
    ChangeTracker ChangeTracker { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Tranzaksiya ichida saqlangan bildirishnomalarni tashqi kanallarga yuborish (commit'dan keyin).</summary>
    void FlushNotifications();
    void DiscardPendingNotifications();
}
