using System.Net;
using System.Text;
using LeadBridgeMeta.Application.Meta;

namespace LeadBridgeMeta.Infrastructure.Email;

public static class EmailTemplateBuilder
{
    public static string BuildLeadNotificationHtml(
        MetaLeadDataDto lead,
        string formName,
        string? pageName,
        string leadgenId,
        string frontendBaseUrl)
    {
        var sb = new StringBuilder();
        var safeFormName = WebUtility.HtmlEncode(formName);
        var safePageName = WebUtility.HtmlEncode(pageName ?? "Facebook Page");
        var safeLeadgenId = WebUtility.HtmlEncode(leadgenId);
        var timestamp = lead.CreatedTimeUtc.ToString("MMM dd, yyyy · hh:mm tt 'UTC'");
        var dashboardUrl = $"{frontendBaseUrl.TrimEnd('/')}/dashboard";

        sb.Append($@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>New Meta Lead - {safeFormName}</title>
  <style>
    body {{
      margin: 0;
      padding: 0;
      background-color: #f4f5f7;
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
      color: #0f172a;
      -webkit-font-smoothing: antialiased;
    }}
    .wrapper {{
      width: 100%;
      background-color: #f4f5f7;
      padding: 30px 15px;
      box-sizing: border-box;
    }}
    .container {{
      max-width: 600px;
      margin: 0 auto;
      background: #ffffff;
      border-radius: 12px;
      overflow: hidden;
      border: 1px solid #e2e8f0;
      box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.05), 0 2px 4px -2px rgba(0, 0, 0, 0.05);
    }}
    .header {{
      background: linear-gradient(135deg, #1e3a8a 0%, #2563eb 100%);
      padding: 28px 32px;
      text-align: left;
    }}
    .brand-title {{
      color: #ffffff;
      font-size: 22px;
      font-weight: 700;
      letter-spacing: -0.5px;
      margin: 0 0 6px 0;
    }}
    .brand-title span {{
      color: #93c5fd;
      font-weight: 400;
    }}
    .badge {{
      display: inline-block;
      background: rgba(255, 255, 255, 0.2);
      color: #ffffff;
      padding: 4px 10px;
      border-radius: 9999px;
      font-size: 12px;
      font-weight: 600;
      letter-spacing: 0.5px;
      text-transform: uppercase;
      margin-top: 4px;
    }}
    .content {{
      padding: 32px;
    }}
    .summary-card {{
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      padding: 16px 20px;
      margin-bottom: 28px;
    }}
    .summary-row {{
      display: flex;
      justify-content: space-between;
      margin-bottom: 8px;
      font-size: 13px;
    }}
    .summary-row:last-child {{
      margin-bottom: 0;
    }}
    .summary-label {{
      color: #64748b;
      font-weight: 500;
    }}
    .summary-value {{
      color: #0f172a;
      font-weight: 600;
    }}
    .section-title {{
      font-size: 16px;
      font-weight: 700;
      color: #0f172a;
      margin: 0 0 16px 0;
      padding-bottom: 8px;
      border-bottom: 2px solid #e2e8f0;
    }}
    .fields-table {{
      width: 100%;
      border-collapse: collapse;
      margin-bottom: 28px;
    }}
    .fields-table tr {{
      border-bottom: 1px solid #edf2f7;
    }}
    .fields-table tr:last-child {{
      border-bottom: none;
    }}
    .fields-table td {{
      padding: 12px 10px;
      vertical-align: top;
      font-size: 14px;
    }}
    .field-label {{
      width: 38%;
      color: #475569;
      font-weight: 600;
      background: #fafbfc;
      border-radius: 6px;
    }}
    .field-value {{
      width: 62%;
      color: #0f172a;
      font-weight: 500;
      word-break: break-word;
    }}
    .field-value a {{
      color: #2563eb;
      text-decoration: none;
    }}
    .field-value a:hover {{
      text-decoration: underline;
    }}
    .btn-container {{
      text-align: center;
      margin-top: 10px;
      margin-bottom: 10px;
    }}
    .btn {{
      display: inline-block;
      background: #2563eb;
      color: #ffffff !important;
      padding: 12px 28px;
      border-radius: 8px;
      font-size: 14px;
      font-weight: 600;
      text-decoration: none;
      box-shadow: 0 2px 4px rgba(37, 99, 235, 0.2);
    }}
    .footer {{
      background: #f8fafc;
      border-top: 1px solid #e2e8f0;
      padding: 20px 32px;
      text-align: center;
      font-size: 12px;
      color: #94a3b8;
      line-height: 1.5;
    }}
    .footer a {{
      color: #64748b;
      text-decoration: none;
    }}
  </style>
</head>
<body>
  <div class=""wrapper"">
    <div class=""container"">
      <div class=""header"">
        <div class=""brand-title"">LeadBridge<span>Meta</span></div>
        <div class=""badge"">New Meta Lead Received</div>
      </div>

      <div class=""content"">
        <div class=""summary-card"">
          <table style=""width:100%; border:none; border-collapse:collapse;"">
            <tr>
              <td class=""summary-label"" style=""padding:4px 0;"">Lead Form:</td>
              <td class=""summary-value"" style=""padding:4px 0; text-align:right;"">{safeFormName}</td>
            </tr>
            <tr>
              <td class=""summary-label"" style=""padding:4px 0;"">Facebook Page:</td>
              <td class=""summary-value"" style=""padding:4px 0; text-align:right;"">{safePageName}</td>
            </tr>
            <tr>
              <td class=""summary-label"" style=""padding:4px 0;"">Date & Time:</td>
              <td class=""summary-value"" style=""padding:4px 0; text-align:right;"">{timestamp}</td>
            </tr>
            <tr>
              <td class=""summary-label"" style=""padding:4px 0;"">Lead ID:</td>
              <td class=""summary-value"" style=""padding:4px 0; text-align:right; font-family:monospace; font-size:12px;"">{safeLeadgenId}</td>
            </tr>
          </table>
        </div>

        <div class=""section-title"">Lead Form Response Details</div>

        <table class=""fields-table"">");

        foreach (var field in lead.FieldData)
        {
            var cleanKey = FormatFieldLabel(field.Name);
            var rawValue = string.Join(", ", field.Values);
            var safeValue = WebUtility.HtmlEncode(rawValue);

            // Enhance emails and phone numbers with clickable links
            if (field.Name.Contains("email", StringComparison.OrdinalIgnoreCase))
            {
                safeValue = $@"<a href=""mailto:{safeValue}"">{safeValue}</a>";
            }
            else if (field.Name.Contains("phone", StringComparison.OrdinalIgnoreCase))
            {
                safeValue = $@"<a href=""tel:{safeValue}"">{safeValue}</a>";
            }

            sb.Append($@"
            <tr>
              <td class=""field-label"">{cleanKey}</td>
              <td class=""field-value"">{(string.IsNullOrWhiteSpace(safeValue) ? "<em style='color:#94a3b8;'>Not provided</em>" : safeValue)}</td>
            </tr>");
        }

        sb.Append($@"
        </table>

        <div class=""btn-container"">
          <a href=""{dashboardUrl}"" class=""btn"">Open Lead in LeadBridge Dashboard &rarr;</a>
        </div>
      </div>

      <div class=""footer"">
        Sent automatically by <strong>LeadBridge Meta Lead Synchronization</strong>.<br>
        Delivered via Mandav Consultancy Email Integration.
      </div>
    </div>
  </div>
</body>
</html>");

        return sb.ToString();
    }

    private static string FormatFieldLabel(string rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey)) return "Question";

        // Remove prefix like "custom." or underscores
        var cleaned = rawKey.Replace('_', ' ').Trim();
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            }
        }
        return WebUtility.HtmlEncode(string.Join(" ", words));
    }
}
