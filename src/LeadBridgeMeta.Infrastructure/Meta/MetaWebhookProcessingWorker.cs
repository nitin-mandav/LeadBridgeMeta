using LeadBridgeMeta.Application.Leads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LeadBridgeMeta.Infrastructure.Meta;

public class MetaWebhookProcessingWorker : BackgroundService
{
    private readonly IMetaWebhookQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MetaWebhookProcessingWorker> _logger;

    public MetaWebhookProcessingWorker(IMetaWebhookQueue queue, IServiceScopeFactory scopeFactory, ILogger<MetaWebhookProcessingWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var notification in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var leadProcessing = scope.ServiceProvider.GetRequiredService<ILeadProcessingService>();
                await leadProcessing.ProcessLeadgenNotificationAsync(
                    notification.PageId, notification.FormId, notification.LeadgenId, notification.RawPayload, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing queued leadgen notification {LeadgenId}.", notification.LeadgenId);
            }
        }
    }
}
