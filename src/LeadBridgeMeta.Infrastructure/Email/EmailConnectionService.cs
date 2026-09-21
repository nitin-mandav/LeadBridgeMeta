using System.Net;
using System.Net.Mail;
using LeadBridgeMeta.Application.Common;
using LeadBridgeMeta.Application.Email;
using LeadBridgeMeta.Application.Meta;
using LeadBridgeMeta.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeadBridgeMeta.Infrastructure.Email;

public class EmailConnectionService : IEmailConnectionService
{
    private readonly IAppDbContext _db;
    private readonly EmailOptions _options;
    private readonly IConfiguration _config;
    private readonly ILogger<EmailConnectionService> _logger;

    public EmailConnectionService(
        IAppDbContext db,
        IOptions<EmailOptions> options,
        IConfiguration config,
        ILogger<EmailConnectionService> logger)
    {
        _db = db;
        _options = options.Value;
        _config = config;
        _logger = logger;
    }

    public string GetSenderEmail() => _options.Mailid;

    public async Task<List<EmailConnectionDto>> GetConnectionsAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.EmailConnections
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .Select(e => new EmailConnectionDto(e.Id, e.Email, e.IsActive, e.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<EmailConnectionDto> AddConnectionAsync(Guid tenantId, string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedEmail) || !normalizedEmail.Contains('@'))
        {
            throw new ArgumentException("A valid email address is required.", nameof(email));
        }

        var existing = await _db.EmailConnections
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Email == normalizedEmail, ct);

        if (existing != null)
        {
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                await _db.SaveChangesAsync(ct);
            }
            return new EmailConnectionDto(existing.Id, existing.Email, existing.IsActive, existing.CreatedAtUtc);
        }

        var connection = new EmailConnection
        {
            TenantId = tenantId,
            Email = normalizedEmail,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.EmailConnections.Add(connection);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Added new email connection {Email} for tenant {TenantId}.", normalizedEmail, tenantId);

        return new EmailConnectionDto(connection.Id, connection.Email, connection.IsActive, connection.CreatedAtUtc);
    }

    public async Task<bool> DisconnectAsync(Guid tenantId, Guid connectionId, CancellationToken ct = default)
    {
        var connection = await _db.EmailConnections
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == connectionId, ct);

        if (connection == null) return false;

        _db.EmailConnections.Remove(connection);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Removed email connection {Email} (ID {Id}) for tenant {TenantId}.", connection.Email, connectionId, tenantId);
        return true;
    }

    public async Task<bool> ToggleActiveAsync(Guid tenantId, Guid connectionId, bool isActive, CancellationToken ct = default)
    {
        var connection = await _db.EmailConnections
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == connectionId, ct);

        if (connection == null) return false;

        connection.IsActive = isActive;
        await _db.SaveChangesAsync(ct);

        return true;
    }

    public async Task<EmailSendResult> SendLeadEmailToConnectionsAsync(
        Guid tenantId,
        MetaLeadDataDto lead,
        string formName,
        string? pageName,
        string leadgenId,
        CancellationToken ct = default)
    {
        var activeConnections = await _db.EmailConnections
            .Where(e => e.TenantId == tenantId && e.IsActive)
            .ToListAsync(ct);

        if (activeConnections.Count == 0)
        {
            _logger.LogInformation("No active email connections configured for tenant {TenantId}. Skipping email delivery.", tenantId);
            return new EmailSendResult(0, Array.Empty<string>(), Array.Empty<string>());
        }

        var frontendBaseUrl = _config["App:FrontendBaseUrl"] ?? "http://localhost:4200";
        var htmlBody = EmailTemplateBuilder.BuildLeadNotificationHtml(lead, formName, pageName, leadgenId, frontendBaseUrl);
        var subject = $"New Meta Lead: {formName} ({leadgenId})";

        var recipientEmails = activeConnections.Select(c => c.Email).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var sentEmails = new List<string>();
        var failedEmails = new List<string>();

        foreach (var recipient in recipientEmails)
        {
            try
            {
                await SendSingleEmailAsync(recipient, subject, htmlBody, ct);
                sentEmails.Add(recipient);
                _logger.LogInformation("Lead notification for leadgen {LeadgenId} sent to {Recipient}.", leadgenId, recipient);
            }
            catch (Exception ex)
            {
                failedEmails.Add(recipient);
                _logger.LogError(ex, "Failed to send lead email for {LeadgenId} to {Recipient}.", leadgenId, recipient);
            }
        }

        return new EmailSendResult(sentEmails.Count, sentEmails, failedEmails);
    }

    public async Task<bool> SendTestEmailAsync(Guid tenantId, string targetEmail, CancellationToken ct = default)
    {
        var normalizedEmail = targetEmail.Trim();
        var sampleLead = new MetaLeadDataDto(
            LeadgenId: "test_leadgen_999999",
            FormId: "test_form_111111",
            PageId: "test_page_222222",
            CreatedTimeUtc: DateTime.UtcNow,
            FieldData: new List<MetaLeadFieldData>
            {
                new("full_name", new List<string> { "John Doe" }),
                new("email", new List<string> { normalizedEmail }),
                new("phone_number", new List<string> { "+1 (555) 234-5678" }),
                new("company_name", new List<string> { "Acme Corporation" }),
                new("city", new List<string> { "New York" }),
                new("preferred_service", new List<string> { "Digital Marketing & Meta Lead Sync" })
            });

        var frontendBaseUrl = _config["App:FrontendBaseUrl"] ?? "http://localhost:4200";
        var htmlBody = EmailTemplateBuilder.BuildLeadNotificationHtml(
            sampleLead,
            "Sample Meta Lead Form",
            "Mandav Consultancy Official Page",
            sampleLead.LeadgenId,
            frontendBaseUrl);

        var subject = "Test Lead Notification - LeadBridge Meta Integration";
        await SendSingleEmailAsync(normalizedEmail, subject, htmlBody, ct);
        return true;
    }

    private async Task SendSingleEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.Mailid) || string.IsNullOrWhiteSpace(_options.MailPwd))
        {
            throw new InvalidOperationException("SMTP credentials (Mailid and MailPwd) are not configured.");
        }

        using var client = new SmtpClient(_options.MailHost, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_options.Mailid, _options.MailPwd),
            Timeout = 15000
        };

        using var message = new MailMessage
        {
            From = new MailAddress(_options.Mailid, _options.SenderDisplayName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8
        };

        message.To.Add(toEmail);

        await client.SendMailAsync(message, ct);
    }
}
