using Erpos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erpos.Infrastructure.Persistence;

public class TourPackageConfig : IEntityTypeConfiguration<TourPackage>
{
    public void Configure(EntityTypeBuilder<TourPackage> b)
    {
        b.ToTable("tour_packages");
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Destination).HasMaxLength(150).IsRequired();
        b.Property(x => x.Summary).HasMaxLength(2000);
        b.Property(x => x.Inclusions).HasMaxLength(2000);
        b.Property(x => x.Exclusions).HasMaxLength(2000);
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Itinerary).WithOne().HasForeignKey(d => d.TourPackageId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.Code });
    }
}

public class ItineraryDayConfig : IEntityTypeConfiguration<ItineraryDay>
{
    public void Configure(EntityTypeBuilder<ItineraryDay> b)
    {
        b.ToTable("tour_itinerary_days");
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Overnight).HasMaxLength(150);
        b.Property(x => x.Meals).HasMaxLength(50);
    }
}

public class GuideConfig : IEntityTypeConfiguration<Guide>
{
    public void Configure(EntityTypeBuilder<Guide> b)
    {
        b.ToTable("tour_guides");
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(50);
        b.Property(x => x.Languages).HasMaxLength(200);
        b.Property(x => x.LicenseNo).HasMaxLength(50);
    }
}

public class TourDepartureConfig : IEntityTypeConfiguration<TourDeparture>
{
    public void Configure(EntityTypeBuilder<TourDeparture> b)
    {
        b.ToTable("tour_departures");
        b.Property(x => x.Code).HasMaxLength(60).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasOne(x => x.TourPackage).WithMany().HasForeignKey(x => x.TourPackageId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Guides).WithOne().HasForeignKey(g => g.TourDepartureId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Costs).WithOne().HasForeignKey(c => c.TourDepartureId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.StartDate });
        b.HasIndex(x => new { x.TenantId, x.Code });
    }
}

public class DepartureGuideConfig : IEntityTypeConfiguration<DepartureGuide>
{
    public void Configure(EntityTypeBuilder<DepartureGuide> b)
    {
        b.ToTable("tour_departure_guides");
        b.Property(x => x.Role).HasMaxLength(50);
        b.HasOne(x => x.Guide).WithMany().HasForeignKey(x => x.GuideId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DepartureCostConfig : IEntityTypeConfiguration<DepartureCost>
{
    public void Configure(EntityTypeBuilder<DepartureCost> b)
    {
        b.ToTable("tour_departure_costs");
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();
        b.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TravelBookingConfig : IEntityTypeConfiguration<TravelBooking>
{
    public void Configure(EntityTypeBuilder<TravelBooking> b)
    {
        // Concurrency guard: a second simultaneous change to these fails with 409 instead of double-posting.
        b.Property(x => x.Status).IsConcurrencyToken();
        b.ToTable("trv_bookings");
        b.Property(x => x.Number).HasMaxLength(30);
        b.Property(x => x.ContactPhone).HasMaxLength(50);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Passengers).WithOne().HasForeignKey(p => p.TravelBookingId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.TravelBookingId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.Status });
        b.HasIndex(x => new { x.TenantId, x.TravelDate });
    }
}

public class BookingPassengerConfig : IEntityTypeConfiguration<BookingPassenger>
{
    public void Configure(EntityTypeBuilder<BookingPassenger> b)
    {
        b.ToTable("trv_passengers");
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.PassportNo).HasMaxLength(30);
        b.Property(x => x.Nationality).HasMaxLength(60);
        b.Property(x => x.Cnic).HasMaxLength(15);
        b.Property(x => x.Phone).HasMaxLength(50);
    }
}

public class BookingItemConfig : IEntityTypeConfiguration<BookingItem>
{
    public void Configure(EntityTypeBuilder<BookingItem> b)
    {
        b.ToTable("trv_booking_items");
        b.Property(x => x.Description).HasMaxLength(300).IsRequired();
        b.Property(x => x.Quantity).HasPrecision(18, 4);
        b.Property(x => x.Airline).HasMaxLength(60);
        b.Property(x => x.Pnr).HasMaxLength(20);
        b.Property(x => x.TicketNumber).HasMaxLength(30);
        b.Property(x => x.Route).HasMaxLength(60);
        b.Property(x => x.Country).HasMaxLength(60);
        b.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TourDeparture).WithMany().HasForeignKey(x => x.TourDepartureId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.TourDepartureId);
    }
}

public class BookingDepositConfig : IEntityTypeConfiguration<BookingDeposit>
{
    public void Configure(EntityTypeBuilder<BookingDeposit> b)
    {
        // Concurrency guard: a second simultaneous change to these fails with 409 instead of double-posting.
        b.Property(x => x.Applied).IsConcurrencyToken();
        b.ToTable("trv_deposits");
        b.Property(x => x.Reference).HasMaxLength(100);
        b.HasIndex(x => x.TravelBookingId);
    }
}
