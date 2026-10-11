using System.Text.Json;
using CommentsApp.Api.Dtos;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CommentsApp.Api.Caching;

public interface ICommentsCache
{
    Task<string?> CreateKeyAsync(CommentQuery query, CancellationToken ct);

    Task<PagedResult<CommentDto>?> GetAsync(string key, CancellationToken ct);

    Task SetAsync(string key, PagedResult<CommentDto> value, CancellationToken ct);

    Task InvalidateAsync(CancellationToken ct);
}

public sealed class DistributedCommentsCache : ICommentsCache
{
    private const string VersionKey = "comments:version";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly DistributedCacheEntryOptions PageOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedCommentsCache> _logger;

    public DistributedCommentsCache(IDistributedCache cache, ILogger<DistributedCommentsCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<string?> CreateKeyAsync(CommentQuery query, CancellationToken ct)
    {
        try
        {
            var version = await _cache.GetStringAsync(VersionKey, ct);
            if (version is null)
            {
                version = NewVersion();
                await _cache.SetStringAsync(VersionKey, version, ct);
            }

            var page = Math.Max(query.Page, 1);
            var direction = query.Desc ? "desc" : "asc";
            return $"comments:page:{version}:{NormalizeSort(query.SortBy)}:{direction}:{page}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Cache is unavailable, reading from the database.");
            return null;
        }
    }

    public async Task<PagedResult<CommentDto>?> GetAsync(string key, CancellationToken ct)
    {
        try
        {
            var json = await _cache.GetStringAsync(key, ct);
            return json is null ? null : JsonSerializer.Deserialize<PagedResult<CommentDto>>(json, Json);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not read the cache entry.");
            return null;
        }
    }

    public async Task SetAsync(string key, PagedResult<CommentDto> value, CancellationToken ct)
    {
        try
        {
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(value, Json), PageOptions, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not write the cache entry.");
        }
    }

    public async Task InvalidateAsync(CancellationToken ct)
    {
        try
        {
            await _cache.SetStringAsync(VersionKey, NewVersion(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not invalidate the cache; stale pages will expire by TTL.");
        }
    }

    private static string NormalizeSort(string? sortBy)
    {
        var sort = sortBy?.ToLowerInvariant();
        return sort is "username" or "email" ? sort : "date";
    }

    private static string NewVersion() => Guid.NewGuid().ToString("N");
}