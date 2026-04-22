using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Data.Configuration;

internal sealed class AuthorizationSuperadminEntityConfiguration : IEntityTypeConfiguration<AuthzSuperadminEntity>
{
    public void Configure(EntityTypeBuilder<AuthzSuperadminEntity> builder)
    {
        builder.ToTable("superadmins", "authz");
        builder.HasKey(x => x.OsobaId);
        builder.Property(x => x.OsobaId).HasColumnName("osoba_id");
        builder.Property(x => x.Poznamka).HasColumnName("poznamka");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
    }
}

internal sealed class AuthorizationPermissionCategoryEntityConfiguration : IEntityTypeConfiguration<AuthzPermissionCategoryEntity>
{
    public void Configure(EntityTypeBuilder<AuthzPermissionCategoryEntity> builder)
    {
        builder.ToTable("permission_categories", "authz");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Kod).HasColumnName("kod");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.SortOrder).HasColumnName("sort_order");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
    }
}

internal sealed class AuthorizationPermissionEntityConfiguration : IEntityTypeConfiguration<AuthzPermissionEntity>
{
    public void Configure(EntityTypeBuilder<AuthzPermissionEntity> builder)
    {
        builder.ToTable("permissions", "authz");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Klic).HasColumnName("klic");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.CategoryId).HasColumnName("category_id");
        builder.Property(x => x.ScopeLevel).HasColumnName("scope_level");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.IsSystem).HasColumnName("is_system");
    }
}

internal sealed class AuthorizationRoleEntityConfiguration : IEntityTypeConfiguration<AuthzRoleEntity>
{
    public void Configure(EntityTypeBuilder<AuthzRoleEntity> builder)
    {
        builder.ToTable("roles", "authz");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Kod).HasColumnName("kod");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.Popis).HasColumnName("popis");
        builder.Property(x => x.IsSystem).HasColumnName("is_system");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.Scope)
            .HasColumnName("scope")
            .HasMaxLength(16)
            .IsRequired()
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<RoleScope>(v, true));
    }
}

internal sealed class AuthorizationRolePermissionEntityConfiguration : IEntityTypeConfiguration<AuthzRolePermissionEntity>
{
    public void Configure(EntityTypeBuilder<AuthzRolePermissionEntity> builder)
    {
        builder.ToTable("role_permissions", "authz");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RoleId).HasColumnName("role_id");
        builder.Property(x => x.PermissionId).HasColumnName("permission_id");
        builder.Property(x => x.ScopeMode)
            .HasColumnName("scope_mode")
            .HasMaxLength(16)
            .IsRequired()
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<ScopeMode>(v, true));
        builder.Property(x => x.IsAllowed).HasColumnName("is_allowed");
    }
}

internal sealed class AuthorizationRolePermissionProjectEntityConfiguration : IEntityTypeConfiguration<AuthzRolePermissionProjectEntity>
{
    public void Configure(EntityTypeBuilder<AuthzRolePermissionProjectEntity> builder)
    {
        builder.ToTable("role_permission_projects", "authz");
        builder.HasKey(x => new { x.RolePermissionId, x.ProjektId });
        builder.Property(x => x.RolePermissionId).HasColumnName("role_permission_id");
        builder.Property(x => x.ProjektId).HasColumnName("projekt_id");
    }
}

internal sealed class AuthorizationUserRoleEntityConfiguration : IEntityTypeConfiguration<AuthzUserRoleEntity>
{
    public void Configure(EntityTypeBuilder<AuthzUserRoleEntity> builder)
    {
        builder.ToTable("user_roles", "authz");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.OsobaId).HasColumnName("osoba_id");
        builder.Property(x => x.RoleId).HasColumnName("role_id");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    }
}

internal sealed class AuthorizationAuditLogEntityConfiguration : IEntityTypeConfiguration<AuthzAuditLogEntity>
{
    public void Configure(EntityTypeBuilder<AuthzAuditLogEntity> builder)
    {
        builder.ToTable("audit_log", "authz");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ActorOsobaId).HasColumnName("actor_osoba_id");
        builder.Property(x => x.EntityType).HasColumnName("entity_type");
        builder.Property(x => x.EntityId).HasColumnName("entity_id");
        builder.Property(x => x.Action).HasColumnName("action");
        builder.Property(x => x.OldValue).HasColumnName("old_value");
        builder.Property(x => x.NewValue).HasColumnName("new_value");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    }
}
