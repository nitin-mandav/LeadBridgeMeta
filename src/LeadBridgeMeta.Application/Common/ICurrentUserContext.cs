namespace LeadBridgeMeta.Application.Common;

/// <summary>Resolved from the authenticated JWT for the current request. Every query/command scopes to TenantId to enforce multi-tenant isolation.</summary>
public interface ICurrentUserContext
{
    Guid UserId { get; }
    Guid TenantId { get; }
    string Email { get; }
}
