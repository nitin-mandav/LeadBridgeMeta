using LeadBridgeMeta.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LeadBridgeMeta.Application.Common;

/// <summary>Persistence seam the Application layer codes against, so it stays free of an EF Core package reference.
/// Infrastructure's AppDbContext implements this directly.</summary>
public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<MetaConnection> MetaConnections { get; }
    DbSet<MetaPage> MetaPages { get; }
    DbSet<MetaLeadForm> MetaLeadForms { get; }
    DbSet<GhlConnection> GhlConnections { get; }
    DbSet<FieldMapping> FieldMappings { get; }
    DbSet<LeadEvent> LeadEvents { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
