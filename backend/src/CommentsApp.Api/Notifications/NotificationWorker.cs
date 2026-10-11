namespace CommentsApp.Api.Notifications;

public sealed class NotificationWorker : BackgroundService
{
    private const int MaxAttempts = 3;

    private readonly INotificationQueue _queue;
    private readonly INotificationSender _sender;
    private readonly ILogger<NotificationWorker> _logger;

    public NotificationWorker(
        INotificationQueue queue, INotificationSender sender, ILogger<NotificationWorker> logger)
    {
        _queue = queue;
        _sender = sender;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var job in _queue.ReadAllAsync(stoppingToken))
                await ProcessAsync(job, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task ProcessAsync(ReplyNotification job, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await _sender.SendReplyNotificationAsync(job, ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt == MaxAttempts)
                {
                    _logger.LogError(ex, "Notification for comment {CommentId} failed after {Attempts} attempts",
                        job.CommentId, MaxAttempts);
                    return;
                }

                _logger.LogWarning(ex, "Notification for comment {CommentId} failed (attempt {Attempt}/{Max})",
                    job.CommentId, attempt, MaxAttempts);

                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
            }
        }
    }
}