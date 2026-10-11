using CommentsApp.Api.Caching;
using CommentsApp.Api.Dtos;
using CommentsApp.Api.Services;
using CommentsApp.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CommentsApp.Tests;

public class CachedCommentServiceTests
{
    private readonly CountingService _inner = new();
    private readonly DistributedCommentsCache _cache;
    private readonly CachedCommentService _service;

    public CachedCommentServiceTests()
    {
        var store = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        _cache = new DistributedCommentsCache(store, NullLogger<DistributedCommentsCache>.Instance);
        _service = new CachedCommentService(_inner, _cache);
    }

    [Fact]
    public async Task Second_identical_request_is_served_from_cache()
    {
        var first = await _service.GetPageAsync(new CommentQuery(), CancellationToken.None);
        var second = await _service.GetPageAsync(new CommentQuery(), CancellationToken.None);

        Assert.Equal(1, _inner.GetPageCalls);
        Assert.Equal(first.Items[0].Id, second.Items[0].Id);
    }

    [Fact]
    public async Task Invalidation_forces_a_fresh_read()
    {
        await _service.GetPageAsync(new CommentQuery(), CancellationToken.None);
        await _cache.InvalidateAsync(CancellationToken.None);
        await _service.GetPageAsync(new CommentQuery(), CancellationToken.None);

        Assert.Equal(2, _inner.GetPageCalls);
    }

    [Fact]
    public async Task Different_pages_and_sorting_are_cached_separately()
    {
        await _service.GetPageAsync(new CommentQuery { Page = 1 }, CancellationToken.None);
        await _service.GetPageAsync(new CommentQuery { Page = 2 }, CancellationToken.None);
        await _service.GetPageAsync(new CommentQuery { SortBy = "email" }, CancellationToken.None);

        Assert.Equal(3, _inner.GetPageCalls);
    }

    [Fact]
    public async Task Unknown_sort_field_shares_the_cache_entry_with_date_sort()
    {
        await _service.GetPageAsync(new CommentQuery { SortBy = "date" }, CancellationToken.None);
        await _service.GetPageAsync(new CommentQuery { SortBy = "garbage" }, CancellationToken.None);

        Assert.Equal(1, _inner.GetPageCalls);
    }

    [Fact]
    public async Task Broken_cache_does_not_break_reads()
    {
        var service = new CachedCommentService(
            _inner,
            new DistributedCommentsCache(new BrokenCache(), NullLogger<DistributedCommentsCache>.Instance));

        var result = await service.GetPageAsync(new CommentQuery(), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(1, _inner.GetPageCalls);
    }

    private sealed class CountingService : ICommentService
    {
        public int GetPageCalls { get; private set; }

        public Task<PagedResult<CommentDto>> GetPageAsync(CommentQuery query, CancellationToken ct)
        {
            GetPageCalls++;
            return Task.FromResult(new PagedResult<CommentDto>
            {
                Items = new List<CommentDto>
                {
                    new() { Id = GetPageCalls, UserName = "u", Email = "u@example.com", Text = "t" }
                },
                Page = query.Page,
                PageSize = 25,
                TotalCount = 1
            });
        }

        public Task<CommentDto?> CreateAsync(
            CreateCommentRequest request, string ip, string userAgent, Attachment? attachment, CancellationToken ct)
            => Task.FromResult<CommentDto?>(null);
    }

    private sealed class BrokenCache : IDistributedCache
    {
        private static InvalidOperationException Down() => new("cache is down");

        public byte[]? Get(string key) => throw Down();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Down();
        public void Refresh(string key) => throw Down();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Down();
        public void Remove(string key) => throw Down();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Down();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Down();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default) => throw Down();
    }
}