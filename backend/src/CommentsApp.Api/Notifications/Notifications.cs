using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace CommentsApp.Api.Notifications;

public sealed record ReplyNotification(
	int CommentId, int ParentCommentId, string RecipientName, string RecipientEmail, string ReplyAuthor);

public interface INotificationQueue
{
	bool TryEnqueue(ReplyNotification job);

	IAsyncEnumerable<ReplyNotification> ReadAllAsync(CancellationToken ct);
}

public sealed class NotificationQueue : INotificationQueue
{
	private readonly Channel<ReplyNotification> _channel = Channel.CreateBounded<ReplyNotification>(
		new BoundedChannelOptions(1000)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = true
		});

	public bool TryEnqueue(ReplyNotification job) => _channel.Writer.TryWrite(job);

	public IAsyncEnumerable<ReplyNotification> ReadAllAsync(CancellationToken ct)
		=> _channel.Reader.ReadAllAsync(ct);
}

public interface INotificationSender
{
	Task SendReplyNotificationAsync(ReplyNotification notification, CancellationToken ct);
}

public sealed class LoggingNotificationSender : INotificationSender
{
	private readonly ILogger<LoggingNotificationSender> _logger;

	public LoggingNotificationSender(ILogger<LoggingNotificationSender> logger) => _logger = logger;

	public Task SendReplyNotificationAsync(ReplyNotification n, CancellationToken ct)
	{
		_logger.LogInformation(
			"Notification to {Recipient} ({Email}): {Author} replied to your comment #{ParentId} (reply #{CommentId})",
			n.RecipientName, MaskEmail(n.RecipientEmail), n.ReplyAuthor, n.ParentCommentId, n.CommentId);

		return Task.CompletedTask;
	}

	private static string MaskEmail(string email)
	{
		var at = email.IndexOf('@');
		return at > 0 ? email[0] + "***" + email[at..] : "***";
	}
}