using CommentsApp.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CommentsApp.Api.Events;

public sealed class BroadcastCommentCreatedHandler : IEventHandler<CommentCreated>
{
    private readonly IHubContext<CommentsHub> _hub;

    public BroadcastCommentCreatedHandler(IHubContext<CommentsHub> hub) => _hub = hub;

    public Task HandleAsync(CommentCreated domainEvent, CancellationToken ct)
        => _hub.Clients.All.SendAsync(
            "commentCreated",
            new CommentCreatedNotice(domainEvent.CommentId, domainEvent.ParentId, domainEvent.CreatedAt),
            ct);
}