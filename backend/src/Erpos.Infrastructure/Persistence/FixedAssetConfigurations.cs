using Erpos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erpos.Infrastructure.Persistence;

public class AssetCategoryConfig : IEntityTypeConfiguration<AssetCategory>
{
    public void Configure(EntityTypeBuilder<AssetCategory> b)
    {
        b.ToTable("fin_asset_categories");
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.ReducingRate).HasPrecision(9, 6);
        b.HasIndex(x => new { x.TenantId, x.Code });
    }
}

public class FixedAssetConfig : IEntityTypeConfiguration<FixedAsset>
{
    public void Configure(EntityTypeBuilder<FixedAsset> b)
    {
        b.ToTable("fin_fixed_assets");
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.SerialNo).HasMaxLength(100);
        b.Property(x => x.Location).HasMaxLength(150);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.ReducingRate).HasPrecision(9, 6);
        // Concurrency guard: two depreciation runs or a run and a disposal can't both move the same asset.
        b.Property(x => x.AccumulatedDepreciation).IsConcurrencyToken();
        b.Property(x => x.Status).IsConcurrencyToken();
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.Code });
        b.HasIndex(x => new { x.EntityId, x.Status });
    }
}

public class DepreciationRunConfig : IEntityTypeConfiguration<DepreciationRun>
{
    public void Configure(EntityTypeBuilder<DepreciationRun> b)
    {
        b.ToTable("fin_depreciation_runs");
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.RunId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.EntityId, x.Year, x.Month });
    }
}

public class DepreciationLineConfig : IEntityTypeConfiguration<DepreciationLine>
{
    public void Configure(EntityTypeBuilder<DepreciationLine> b)
    {
        b.ToTable("fin_depreciation_lines");
        b.HasIndex(x => x.AssetId);
    }
}
