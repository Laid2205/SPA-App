using CommentsApp.Api.Dtos;
using CommentsApp.Api.Events;
using CommentsApp.Api.Notifications;
using CommentsApp.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CommentsApp.Tests;

public class EventPublisherTests
{
    private sealed record TestEvent : IDomainEvent { }

    private sealed class FailingHandler : IEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent domainEvent, CancellationToken ct)
            => throw new InvalidOperationException("boom");
    }

    private sealed class RecordingHandler : IEventHandler<TestEvent>
    {
        public int Calls { get; private set; }

        public Task HandleAsync(TestEvent domainEvent, CancellationToken ct)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Failing_handler_does_not_throw_and_does_not_stop_other_handlers()
    {
        var recording = new RecordingHandler();
        var services = new ServiceCollection()
            .AddSingleton<IEventHandler<TestEvent>>(new FailingHandler())
            .AddSingleton<IEventHandler<TestEvent>>(recording)
            .BuildServiceProvider();

        var publisher = new EventPublisher(services, NullLogger<EventPublisher>.Instance);

        await publisher.PublishAsync(new TestEvent());

        Assert.Equal(1, recording.Calls);
    }
}

public class EnqueueReplyNotificationHandlerTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly FakeQueue _queue = new();
    private readonly CommentService _comments;
    private readonly EnqueueReplyNotificationHandler _handler;

    public EnqueueReplyNotificationHandlerTests()
    {
        _comments = new CommentService(_db.Context);
        _handler = new EnqueueReplyNotificationHandler(
            _db.Context, _queue, NullLogger<EnqueueReplyNotificationHandler>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Reply_from_another_user_notifies_the_parent_author()
    {
        var root = await AddAsync("Alice", "alice@example.com");
        var reply = await AddAsync("Bob", "bob@example.com", root.Id);

        await _handler.HandleAsync(EventFor(reply), CancellationToken.None);

        var job = Assert.Single(_queue.Jobs);
        Assert.Equal("alice@example.com", job.RecipientEmail);
        Assert.Equal("Bob", job.ReplyAuthor);
        Assert.Equal(root.Id, job.ParentCommentId);
    }

    [Fact]
    public async Task Reply_from_the_same_email_is_not_notified()
    {
        var root = await AddAsync("Alice", "alice@example.com");
        var reply = await AddAsync("Alice", "ALICE@example.com", root.Id);

        await _handler.HandleAsync(EventFor(reply), CancellationToken.None);

        Assert.Empty(_queue.Jobs);
    }

    [Fact]
    public async Task Root_comment_is_not_notified()
    {
        var root = await AddAsync("Alice", "alice@example.com");

        await _handler.HandleAsync(EventFor(root), CancellationToken.None);

        Assert.Empty(_queue.Jobs);
    }

    private async Task<CommentDto> AddAsync(string userName, string email, int? parentId = null)
    {
        var created = await _comments.CreateAsync(
            new CreateCommentRequest
            {
                UserName = userName,
                Email = email,
                Text = "text",
                ParentId = parentId,
                CaptchaId = "x",
                CaptchaAnswer = "y"
            },
            "127.0.0.1", "tests", null, CancellationToken.None);

        return created!;
    }

    private static CommentCreated EventFor(CommentDto c)
        => new(c.Id, c.ParentId, c.UserName, c.Email, c.CreatedAt);

    private sealed class FakeQueue : INotificationQueue
    {
        public List<ReplyNotification> Jobs { get; } = new();

        public bool TryEnqueue(ReplyNotification job)
        {
            Jobs.Add(job);
            return true;
        }

        public IAsyncEnumerable<ReplyNotification> ReadAllAsync(CancellationToken ct)
            => throw new NotSupportedException();
    }
}