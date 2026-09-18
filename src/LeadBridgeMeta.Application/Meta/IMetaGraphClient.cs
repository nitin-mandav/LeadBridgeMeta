namespace LeadBridgeMeta.Application.Meta;

public record MetaTokenResult(string AccessToken, DateTime ExpiresAtUtc);

public record MetaPageDto(string PageId, string PageName, string PageAccessToken);

public record MetaLeadFormDto(string FormId, string FormName);

public record MetaLeadFieldData(string Name, IReadOnlyList<string> Values);

public record MetaLeadDataDto(
    string LeadgenId,
    string FormId,
    string PageId,
    DateTime CreatedTimeUtc,
    IReadOnlyList<MetaLeadFieldData> FieldData);

/// <summary>Thin wrapper over the Meta Graph API surface this app needs: OAuth code exchange, long-lived token
/// exchange, page/form discovery, leadgen webhook subscription, and reading a single lead's submitted data.</summary>
public interface IMetaGraphClient
{
    string BuildLoginDialogUrl(string state, string redirectUri);

    Task<MetaTokenResult> ExchangeCodeForUserTokenAsync(string code, string redirectUri, CancellationToken ct = default);

    /// <summary>Exchanges a short-lived user token for a long-lived one (~60 days).</summary>
    Task<MetaTokenResult> GetLongLivedUserTokenAsync(string shortLivedToken, CancellationToken ct = default);

    Task<(string FacebookUserId, string Name)> GetMeAsync(string userAccessToken, CancellationToken ct = default);

    Task<IReadOnlyList<MetaPageDto>> GetManagedPagesAsync(string userAccessToken, CancellationToken ct = default);

    Task<IReadOnlyList<MetaLeadFormDto>> GetLeadFormsAsync(string pageId, string pageAccessToken, CancellationToken ct = default);

    /// <summary>Subscribes the page to the "leadgen" webhook field so new form submissions are pushed to our webhook.</summary>
    Task SubscribePageToLeadgenAsync(string pageId, string pageAccessToken, CancellationToken ct = default);

    /// <summary>Unsubscribes the page from the "leadgen" webhook field.</summary>
    Task UnsubscribePageFromLeadgenAsync(string pageId, string pageAccessToken, CancellationToken ct = default);

    Task<MetaLeadDataDto> GetLeadDataAsync(string leadgenId, string pageAccessToken, CancellationToken ct = default);
    Task<IReadOnlyList<MetaFormQuestionDto>> GetLeadFormQuestionsAsync(string formId, string pageAccessToken, CancellationToken ct = default);
}

public record MetaFormQuestionDto(string Key, string Label, string? Type);
