using Erpos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erpos.Infrastructure.Persistence;

public class VehicleConfig : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> b)
    {
        b.ToTable("log_vehicles");
        b.Property(x => x.RegistrationNo).HasMaxLength(20).IsRequired();
        b.Property(x => x.MakeModel).HasMaxLength(100);
        b.Property(x => x.CapacityCbm).HasPrecision(10, 2);
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.OwnerVendor).WithMany().HasForeignKey(x => x.OwnerVendorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.RegistrationNo });
    }
}

public class DriverConfig : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> b)
    {
        b.ToTable("log_drivers");
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Cnic).HasMaxLength(15);
        b.Property(x => x.Phone).HasMaxLength(50);
        b.Property(x => x.LicenseNo).HasMaxLength(40).IsRequired();
        b.Property(x => x.LicenseCategory).HasMaxLength(20);
        b.HasIndex(x => new { x.TenantId, x.LicenseNo });
    }
}

public class FreightRouteConfig : IEntityTypeConfiguration<FreightRoute>
{
    public void Configure(EntityTypeBuilder<FreightRoute> b)
    {
        b.ToTable("log_routes");
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Origin).HasMaxLength(100).IsRequired();
        b.Property(x => x.Destination).HasMaxLength(100).IsRequired();
        b.Property(x => x.RatePerKg).HasPrecision(18, 4);
        b.Property(x => x.FuelSurchargePercent).HasPrecision(6, 2);
        b.Property(x => x.StandardHours).HasPrecision(6, 1);
        b.HasIndex(x => new { x.TenantId, x.Code });
    }
}

public class ShipmentConfig : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> b)
    {
        // Concurrency guard: a second simultaneous change to these fails with 409 instead of double-posting.
        b.Property(x => x.Status).IsConcurrencyToken();
        b.Property(x => x.InvoiceId).IsConcurrencyToken();
        b.Property(x => x.CodRemitted).IsConcurrencyToken();
        b.ToTable("log_shipments");
        b.Property(x => x.Number).HasMaxLength(30);
        foreach (var p in new[] { nameof(Shipment.ShipperName), nameof(Shipment.ConsigneeName), nameof(Shipment.ReceivedBy) }) b.Property(p).HasMaxLength(150);
        foreach (var p in new[] { nameof(Shipment.ShipperPhone), nameof(Shipment.ConsigneePhone) }) b.Property(p).HasMaxLength(50);
        foreach (var p in new[] { nameof(Shipment.ShipperAddress), nameof(Shipment.ConsigneeAddress), nameof(Shipment.GoodsDescription), nameof(Shipment.DeliveryRemarks) }) b.Property(p).HasMaxLength(500);
        b.Property(x => x.OriginCity).HasMaxLength(100).IsRequired();
        b.Property(x => x.DestinationCity).HasMaxLength(100).IsRequired();
        b.Property(x => x.WeightKg).HasPrecision(18, 2);
        b.Property(x => x.VolumeCbm).HasPrecision(10, 2);
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Route).WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Events).WithOne().HasForeignKey(e => e.ShipmentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.Number });
        b.HasIndex(x => new { x.TenantId, x.Status, x.BookingDate });
        b.HasIndex(x => x.CurrentTripId);
        b.HasIndex(x => new { x.CustomerId, x.InvoiceId });
    }
}

public class ShipmentEventConfig : IEntityTypeConfiguration<ShipmentEvent>
{
    public void Configure(EntityTypeBuilder<ShipmentEvent> b)
    {
        b.ToTable("log_shipment_events");
        b.Property(x => x.Location).HasMaxLength(100);
        b.Property(x => x.Remarks).HasMaxLength(300);
    }
}

public class TripConfig : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> b)
    {
        b.ToTable("log_trips");
        b.Property(x => x.Number).HasMaxLength(30);
        b.Property(x => x.Origin).HasMaxLength(100).IsRequired();
        b.Property(x => x.Destination).HasMaxLength(100).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Route).WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Expenses).WithOne().HasForeignKey(e => e.TripId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.Status });
    }
}

public class TripExpenseConfig : IEntityTypeConfiguration<TripExpense>
{
    public void Configure(EntityTypeBuilder<TripExpense> b)
    {
        b.ToTable("log_trip_expenses");
        b.Property(x => x.Description).HasMaxLength(200);
    }
}

public class MaintenanceRecordConfig : IEntityTypeConfiguration<MaintenanceRecord>
{
    public void Configure(EntityTypeBuilder<MaintenanceRecord> b)
    {
        b.ToTable("log_maintenance");
        b.Property(x => x.Description).HasMaxLength(300).IsRequired();
        b.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.VehicleId);
    }
}
