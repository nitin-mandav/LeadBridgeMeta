using System.Threading.Channels;

namespace LeadBridgeMeta.Infrastructure.Meta;

public record LeadgenNotification(string PageId, string FormId, string LeadgenId, string RawPayload);

public interface IMetaWebhookQueue
{
    ValueTask EnqueueAsync(LeadgenNotification notification, CancellationToken ct = default);
    IAsyncEnumerable<LeadgenNotification> DequeueAllAsync(CancellationToken ct);
}

/// <summary>In-process queue so the webhook endpoint can ack Meta immediately (it expects a fast 200) while the
/// actual fetch-map-send work happens on a background worker.</summary>
public class MetaWebhookQueue : IMetaWebhookQueue
{
    private readonly Channel<LeadgenNotification> _channel = Channel.CreateUnbounded<LeadgenNotification>();

    public ValueTask EnqueueAsync(LeadgenNotification notification, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(notification, ct);

    public IAsyncEnumerable<LeadgenNotification> DequeueAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
