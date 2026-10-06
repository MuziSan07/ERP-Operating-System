using Erpos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erpos.Infrastructure.Persistence;

public class TenantConfig : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenants");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.ContactEmail).HasMaxLength(256);
        b.Property(x => x.ContactPhone).HasMaxLength(50);
        b.Property(x => x.Country).HasMaxLength(100);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

public class BusinessEntityConfig : IEntityTypeConfiguration<BusinessEntity>
{
    public void Configure(EntityTypeBuilder<BusinessEntity> b)
    {
        b.ToTable("entities");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        // ~37 chars per level; 700 allows ~18 levels of nesting and still fits a MySQL index.
        b.Property(x => x.Path).HasMaxLength(700).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.Phone).HasMaxLength(50);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.Country).HasMaxLength(100);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.TimeZone).HasMaxLength(64);
        b.Property(x => x.TaxNumber).HasMaxLength(64);

        b.HasOne<Tenant>().WithMany(t => t.Entities).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.Code });
        b.HasIndex(x => new { x.TenantId, x.Path });
    }
}

public class EntityModuleConfig : IEntityTypeConfiguration<EntityModule>
{
    public void Configure(EntityTypeBuilder<EntityModule> b)
    {
        b.ToTable("entity_modules");
        b.Property(x => x.ModuleCode).HasMaxLength(50).IsRequired();
        b.HasOne(x => x.Entity).WithMany(e => e.Modules).HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.EntityId, x.ModuleCode }).IsUnique();
    }
}

public class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(50);
        b.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PrimaryEntity).WithMany().HasForeignKey(x => x.PrimaryEntityId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.Email);
        b.HasIndex(x => new { x.TenantId, x.PrimaryEntityId });
    }
}

public class RoleConfig : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("roles");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => new { x.TenantId, x.Name });
    }
}

public class RolePermissionConfig : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("role_permissions");
        b.HasKey(x => new { x.RoleId, x.PermissionCode });
        b.Property(x => x.PermissionCode).HasMaxLength(100);
        b.HasOne(x => x.Role).WithMany(r => r.Permissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserRoleAssignmentConfig : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> b)
    {
        b.ToTable("user_role_assignments");
        b.HasOne(x => x.User).WithMany(u => u.RoleAssignments).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.RoleId, x.EntityId }).IsUnique();
    }
}

public class UserPermissionOverrideConfig : IEntityTypeConfiguration<UserPermissionOverride>
{
    public void Configure(EntityTypeBuilder<UserPermissionOverride> b)
    {
        b.ToTable("user_permission_overrides");
        b.Property(x => x.PermissionCode).HasMaxLength(100).IsRequired();
        b.HasOne(x => x.User).WithMany(u => u.PermissionOverrides).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.EntityId, x.PermissionCode }).IsUnique();
    }
}

public class RefreshTokenConfig : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.UserId);
    }
}

public class AuditLogConfig : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.Property(x => x.Action).HasMaxLength(20).IsRequired();
        b.Property(x => x.TableName).HasMaxLength(100).IsRequired();
        b.Property(x => x.RecordId).HasMaxLength(100);
        b.Property(x => x.Changes).HasColumnType("longtext");
        b.HasIndex(x => new { x.TenantId, x.Id });
    }
}
