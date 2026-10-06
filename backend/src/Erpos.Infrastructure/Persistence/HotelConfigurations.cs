using Erpos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erpos.Infrastructure.Persistence;

public class RoomTypeConfig : IEntityTypeConfiguration<RoomType>
{
    public void Configure(EntityTypeBuilder<RoomType> b)
    {
        b.ToTable("htl_room_types");
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EntityId, x.Code });
    }
}

public class RoomConfig : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> b)
    {
        b.ToTable("htl_rooms");
        b.Property(x => x.Number).HasMaxLength(20).IsRequired();
        b.Property(x => x.Floor).HasMaxLength(20);
        b.Property(x => x.HousekeepingNote).HasMaxLength(300);
        b.HasOne(x => x.RoomType).WithMany().HasForeignKey(x => x.RoomTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EntityId, x.Number });
    }
}

public class GuestConfig : IEntityTypeConfiguration<Guest>
{
    public void Configure(EntityTypeBuilder<Guest> b)
    {
        b.ToTable("htl_guests");
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(50);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.Cnic).HasMaxLength(15);
        b.Property(x => x.PassportNo).HasMaxLength(30);
        b.Property(x => x.Nationality).HasMaxLength(60);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => new { x.TenantId, x.FullName });
        b.HasIndex(x => new { x.TenantId, x.Phone });
    }
}

public class ReservationConfig : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> b)
    {
        // Concurrency guard: a second simultaneous change to these fails with 409 instead of double-posting.
        b.Property(x => x.Status).IsConcurrencyToken();
        b.Property(x => x.InvoiceId).IsConcurrencyToken();
        b.ToTable("htl_reservations");
        b.Property(x => x.Number).HasMaxLength(30);
        b.Property(x => x.ExternalReference).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Guest).WithMany().HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.BillToContact).WithMany().HasForeignKey(x => x.BillToContactId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Rooms).WithOne().HasForeignKey(r => r.ReservationId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.EntityId, x.Status, x.ArrivalDate });
        b.HasIndex(x => new { x.EntityId, x.DepartureDate });
    }
}

public class ReservationRoomConfig : IEntityTypeConfiguration<ReservationRoom>
{
    public void Configure(EntityTypeBuilder<ReservationRoom> b)
    {
        b.ToTable("htl_reservation_rooms");
        b.HasOne(x => x.RoomType).WithMany().HasForeignKey(x => x.RoomTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.RoomId);
    }
}

public class FolioChargeConfig : IEntityTypeConfiguration<FolioCharge>
{
    public void Configure(EntityTypeBuilder<FolioCharge> b)
    {
        b.ToTable("htl_folio_charges");
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();
        b.Property(x => x.Quantity).HasPrecision(18, 4);
        b.Property(x => x.UnitPrice).HasPrecision(18, 4);
        b.HasOne(x => x.TaxRate).WithMany().HasForeignKey(x => x.TaxRateId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ReservationId, x.Date });
    }
}

public class FolioDepositConfig : IEntityTypeConfiguration<FolioDeposit>
{
    public void Configure(EntityTypeBuilder<FolioDeposit> b)
    {
        b.ToTable("htl_folio_deposits");
        b.Property(x => x.Reference).HasMaxLength(100);
        b.HasIndex(x => x.ReservationId);
    }
}
