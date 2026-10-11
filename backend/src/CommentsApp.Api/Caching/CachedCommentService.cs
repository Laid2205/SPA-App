using CommentsApp.Api.Dtos;
using CommentsApp.Api.Services;
using CommentsApp.Domain.Entities;

namespace CommentsApp.Api.Caching;

public class CachedCommentService : ICommentService
{
    private readonly ICommentService _inner;
    private readonly ICommentsCache _cache;

    public CachedCommentService(ICommentService inner, ICommentsCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<PagedResult<CommentDto>> GetPageAsync(CommentQuery query, CancellationToken ct)
    {
        var key = await _cache.CreateKeyAsync(query, ct);

        if (key is not null)
        {
            var cached = await _cache.GetAsync(key, ct);
            if (cached is not null) return cached;
        }

        var result = await _inner.GetPageAsync(query, ct);

        if (key is not null && result.Items.Count > 0)
            await _cache.SetAsync(key, result, ct);

        return result;
    }

    public Task<CommentDto?> CreateAsync(
        CreateCommentRequest request, string ip, string userAgent, Attachment? attachment, CancellationToken ct)
        => _inner.CreateAsync(request, ip, userAgent, attachment, ct);
}