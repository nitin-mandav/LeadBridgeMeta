using LeadBridgeMeta.Application.Meta;

namespace LeadBridgeMeta.Application.Email;

public record EmailConnectionDto(
    Guid Id,
    string Email,
    bool IsActive,
    DateTime CreatedAtUtc);

public record EmailSendResult(
    int SentCount,
    IReadOnlyList<string> SentToEmails,
    IReadOnlyList<string> FailedEmails);

public interface IEmailConnectionService
{
    Task<List<EmailConnectionDto>> GetConnectionsAsync(Guid tenantId, CancellationToken ct = default);

    Task<EmailConnectionDto> AddConnectionAsync(Guid tenantId, string email, CancellationToken ct = default);

    Task<bool> DisconnectAsync(Guid tenantId, Guid connectionId, CancellationToken ct = default);

    Task<bool> ToggleActiveAsync(Guid tenantId, Guid connectionId, bool isActive, CancellationToken ct = default);

    Task<EmailSendResult> SendLeadEmailToConnectionsAsync(
        Guid tenantId,
        MetaLeadDataDto lead,
        string formName,
        string? pageName,
        string leadgenId,
        CancellationToken ct = default);

    Task<bool> SendTestEmailAsync(Guid tenantId, string targetEmail, CancellationToken ct = default);

    string GetSenderEmail();
}
