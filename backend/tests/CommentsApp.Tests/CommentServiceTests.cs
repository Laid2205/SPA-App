using CommentsApp.Api.Dtos;
using CommentsApp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace CommentsApp.Tests;

public class CommentServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly CommentService _service;

    public CommentServiceTests() => _service = new CommentService(_db.Context);

    public void Dispose() => _db.Dispose();

    private Task<CommentDto?> AddAsync(
        string text, string userName = "Anonym", string email = "anonym@example.com", int? parentId = null)
        => _service.CreateAsync(
            new CreateCommentRequest
            {
                UserName = userName,
                Email = email,
                Text = text,
                ParentId = parentId,
                CaptchaId = "x",
                CaptchaAnswer = "y"
            },
            "127.0.0.1", "tests", null, CancellationToken.None);

    private Task<PagedResult<CommentDto>> GetAsync(CommentQuery? query = null)
        => _service.GetPageAsync(query ?? new CommentQuery(), CancellationToken.None);

    [Fact]
    public async Task Page_contains_25_root_comments_and_second_page_has_the_rest()
    {
        for (var i = 1; i <= 30; i++)
            await AddAsync($"text {i}");

        var page1 = await GetAsync(new CommentQuery { Page = 1 });
        var page2 = await GetAsync(new CommentQuery { Page = 2 });

        Assert.Equal(25, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);
        Assert.Equal(30, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
    }

    [Fact]
    public async Task Default_order_is_newest_first()
    {
        await AddAsync("first");
        await AddAsync("second");
        await AddAsync("third");

        var result = await GetAsync();

        Assert.Equal(new[] { "third", "second", "first" }, result.Items.Select(c => c.Text));
    }

    [Fact]
    public async Task Sort_by_username_ascending_works()
    {
        await AddAsync("1", userName: "Charlie", email: "c@example.com");
        await AddAsync("2", userName: "Alice", email: "a@example.com");
        await AddAsync("3", userName: "Bob", email: "b@example.com");

        var result = await GetAsync(new CommentQuery { SortBy = "username", Desc = false });

        Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, result.Items.Select(c => c.UserName));
    }

    [Fact]
    public async Task Sort_by_email_descending_works()
    {
        await AddAsync("1", userName: "A", email: "a@example.com");
        await AddAsync("2", userName: "B", email: "c@example.com");
        await AddAsync("3", userName: "C", email: "b@example.com");

        var result = await GetAsync(new CommentQuery { SortBy = "email", Desc = true });

        Assert.Equal(
            new[] { "c@example.com", "b@example.com", "a@example.com" },
            result.Items.Select(c => c.Email));
    }

    [Fact]
    public async Task Unknown_sort_field_falls_back_to_date()
    {
        await AddAsync("first");
        await AddAsync("second");

        var result = await GetAsync(new CommentQuery { SortBy = "'; DROP TABLE Comments;--" });

        Assert.Equal(new[] { "second", "first" }, result.Items.Select(c => c.Text));
    }

    [Fact]
    public async Task Replies_are_nested_and_do_not_count_toward_pagination()
    {
        var root = await AddAsync("root");
        var reply = await AddAsync("reply", parentId: root!.Id);
        await AddAsync("reply to reply", parentId: reply!.Id);

        var result = await GetAsync();

        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        var level1 = Assert.Single(item.Replies);
        Assert.Equal("reply", level1.Text);
        var level2 = Assert.Single(level1.Replies);
        Assert.Equal("reply to reply", level2.Text);
    }

    [Fact]
    public async Task Creating_a_reply_to_missing_parent_returns_null()
    {
        var result = await AddAsync("orphan", parentId: 9999);

        Assert.Null(result);
        Assert.Equal(0, await _db.Context.Comments.CountAsync());
    }

    [Fact]
    public async Task Same_name_and_email_reuse_one_user_record()
    {
        await AddAsync("one");
        await AddAsync("two");

        Assert.Equal(1, await _db.Context.Users.CountAsync());
        Assert.Equal(2, await _db.Context.Comments.CountAsync());
    }
}