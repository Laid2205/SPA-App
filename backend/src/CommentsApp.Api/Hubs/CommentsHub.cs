using Microsoft.AspNetCore.SignalR;

namespace CommentsApp.Api.Hubs;

public sealed class CommentsHub : Hub
{
}

public sealed record CommentCreatedNotice(int CommentId, int? ParentId, DateTime CreatedAt);