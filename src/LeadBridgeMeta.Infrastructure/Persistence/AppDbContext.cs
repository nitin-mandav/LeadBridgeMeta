using LeadBridgeMeta.Application.Common;
using LeadBridgeMeta.Domain.Entities;
using LeadBridgeMeta.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LeadBridgeMeta.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<MetaConnection> MetaConnections => Set<MetaConnection>();
    public DbSet<MetaPage> MetaPages => Set<MetaPage>();
    public DbSet<MetaLeadForm> MetaLeadForms => Set<MetaLeadForm>();
    public DbSet<GhlConnection> GhlConnections => Set<GhlConnection>();
    public DbSet<FieldMapping> FieldMappings => Set<FieldMapping>();
    public DbSet<LeadEvent> LeadEvents => Set<LeadEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });

        builder.Entity<MetaConnection>(e =>
        {
            e.HasOne(x => x.Tenant).WithMany(t => t.MetaConnections).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.FacebookUserId).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.FacebookUserId }).IsUnique();
        });

        builder.Entity<MetaPage>(e =>
        {
            e.HasOne(x => x.MetaConnection).WithMany(c => c.Pages).HasForeignKey(x => x.MetaConnectionId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.PageId).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.PageId).IsUnique();
        });

        builder.Entity<MetaLeadForm>(e =>
        {
            e.HasOne(x => x.MetaPage).WithMany(p => p.LeadForms).HasForeignKey(x => x.MetaPageId).OnDelete(DeleteBehavior.Cascade);
            // ClientSetNull (not SetNull): a DB-level ON DELETE SET NULL here plus the cascade path
            // Tenant -> MetaConnection -> MetaPage -> MetaLeadForm gives SQL Server two cascade paths into
            // MetaLeadForms, which it rejects at migration time. EF nulls the FK app-side instead.
            e.HasOne(x => x.GhlConnection).WithMany(g => g.MappedForms).HasForeignKey(x => x.GhlConnectionId).OnDelete(DeleteBehavior.ClientSetNull);
            e.Property(x => x.FormId).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.FormId).IsUnique();
        });

        builder.Entity<GhlConnection>(e =>
        {
            e.HasOne(x => x.Tenant).WithMany(t => t.GhlConnections).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.LocationId).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.LocationId }).IsUnique();
        });

        builder.Entity<FieldMapping>(e =>
        {
            // Restrict (not Cascade): FieldMapping is also reachable from Tenant via
            // Tenant -> MetaConnection -> MetaPage -> MetaLeadForm -> FieldMapping, so a second cascading
            // FK straight from Tenant hits the same "multiple cascade paths" rejection as above.
            e.HasOne(x => x.Tenant).WithMany(t => t.FieldMappings).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.MetaLeadForm).WithMany(f => f.FieldMappings).HasForeignKey(x => x.MetaLeadFormId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.MetaFieldKey).HasMaxLength(200).IsRequired();
            e.Property(x => x.GhlFieldKey).HasMaxLength(200).IsRequired();
        });

        builder.Entity<LeadEvent>(e =>
        {
            e.HasOne(x => x.MetaLeadForm).WithMany(f => f.LeadEvents).HasForeignKey(x => x.MetaLeadFormId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.LeadgenId).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.LeadgenId).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Status });
        });
    }
}
