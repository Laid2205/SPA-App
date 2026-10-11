using CommentsApp.Api.Caching;
using CommentsApp.Api.Notifications;
using CommentsApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CommentsApp.Api.Events;

public sealed record CommentCreated(
    int CommentId, int? ParentId, string UserName, string Email, DateTime CreatedAt) : IDomainEvent;

public sealed class InvalidateCommentsCacheHandler : IEventHandler<CommentCreated>
{
    private readonly ICommentsCache _cache;

    public InvalidateCommentsCacheHandler(ICommentsCache cache) => _cache = cache;

    public Task HandleAsync(CommentCreated domainEvent, CancellationToken ct)
        => _cache.InvalidateAsync(ct);
}

public sealed class EnqueueReplyNotificationHandler : IEventHandler<CommentCreated>
{
    private readonly AppDbContext _db;
    private readonly INotificationQueue _queue;
    private readonly ILogger<EnqueueReplyNotificationHandler> _logger;

    public EnqueueReplyNotificationHandler(
        AppDbContext db, INotificationQueue queue, ILogger<EnqueueReplyNotificationHandler> logger)
    {
        _db = db;
        _queue = queue;
        _logger = logger;
    }

    public async Task HandleAsync(CommentCreated domainEvent, CancellationToken ct)
    {
        if (domainEvent.ParentId is null) return;

        var parentId = domainEvent.ParentId.Value;

        var parentAuthor = await _db.Comments.AsNoTracking()
            .Where(c => c.Id == parentId)
            .Select(c => new { c.User.UserName, c.User.Email })
            .FirstOrDefaultAsync(ct);

        if (parentAuthor is null) return;

        if (string.Equals(parentAuthor.Email, domainEvent.Email, StringComparison.OrdinalIgnoreCase))
            return;

        var job = new ReplyNotification(
            domainEvent.CommentId, parentId, parentAuthor.UserName, parentAuthor.Email, domainEvent.UserName);

        if (!_queue.TryEnqueue(job))
            _logger.LogWarning("Notification queue is full, dropped notification for comment {CommentId}",
                domainEvent.CommentId);
    }
}