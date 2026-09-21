namespace LeadBridgeMeta.Infrastructure.Email;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string Mailid { get; set; } = string.Empty;
    public string MailPwd { get; set; } = string.Empty;
    public string MailHost { get; set; } = "smtp.gmail.com";
    public string MailPort { get; set; } = "587";
    public string MailSSL { get; set; } = "true";
    public string SenderDisplayName { get; set; } = "LeadBridge";

    public int Port => int.TryParse(MailPort, out var p) ? p : 587;
    public bool EnableSsl => !bool.TryParse(MailSSL, out var ssl) || ssl;
}
