using System.Linq.Expressions;
using CommentsApp.Api.Dtos;
using CommentsApp.Domain.Entities;
using CommentsApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CommentsApp.Api.Services;

public class CommentService : ICommentService
{
    public const int PageSize = 25;

    private readonly AppDbContext _db;

    public CommentService(AppDbContext db) => _db = db;

    private static readonly Expression<Func<Comment, CommentDto>> ToDto = c => new CommentDto
    {
        Id = c.Id,
        ParentId = c.ParentId,
        UserName = c.User.UserName,
        Email = c.User.Email,
        HomePage = c.User.HomePage,
        Text = c.Text,
        CreatedAt = c.CreatedAt
    };

    public async Task<PagedResult<CommentDto>> GetPageAsync(CommentQuery query, CancellationToken ct)
    {
        var page = Math.Max(query.Page, 1);

        var roots = _db.Comments.AsNoTracking().Where(c => c.ParentId == null);
        var total = await roots.CountAsync(ct);

        var ordered = (query.SortBy.ToLowerInvariant(), query.Desc) switch
        {
            ("username", true) => roots.OrderByDescending(c => c.User.UserName).ThenByDescending(c => c.Id),
            ("username", false) => roots.OrderBy(c => c.User.UserName).ThenBy(c => c.Id),
            ("email", true) => roots.OrderByDescending(c => c.User.Email).ThenByDescending(c => c.Id),
            ("email", false) => roots.OrderBy(c => c.User.Email).ThenBy(c => c.Id),
            (_, false) => roots.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id),
            _ => roots.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
        };

        var rootItems = await ordered
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(ToDto)
            .ToListAsync(ct);

        await LoadRepliesAsync(rootItems, ct);

        return new PagedResult<CommentDto>
        {
            Items = rootItems,
            Page = page,
            PageSize = PageSize,
            TotalCount = total
        };
    }

    // Загружаем ответы уровень за уровнем: один запрос на каждый уровень вложенности.
    private async Task LoadRepliesAsync(List<CommentDto> roots, CancellationToken ct)
    {
        var byId = roots.ToDictionary(c => c.Id);
        var currentIds = roots.Select(c => c.Id).ToList();

        while (currentIds.Count > 0)
        {
            var children = await _db.Comments.AsNoTracking()
                .Where(c => c.ParentId != null && currentIds.Contains(c.ParentId.Value))
                .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
                .Select(ToDto)
                .ToListAsync(ct);

            foreach (var child in children)
            {
                byId[child.ParentId!.Value].Replies.Add(child);
                byId[child.Id] = child;
            }

            currentIds = children.Select(c => c.Id).ToList();
        }
    }

    public async Task<CommentDto?> CreateAsync(
        CreateCommentRequest request, string ip, string userAgent, CancellationToken ct)
    {
        Comment? parent = null;
        if (request.ParentId is not null)
        {
            parent = await _db.Comments.FindAsync(new object[] { request.ParentId.Value }, ct);
            if (parent is null) return null;
        }

        // Один человек (имя + e-mail) = одна запись в Users, а не новая на каждый комментарий.
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.UserName == request.UserName && u.Email == request.Email, ct);

        if (user is null)
        {
            user = new User(request.UserName, request.Email, request.HomePage);
            _db.Users.Add(user);
        }
        else if (request.HomePage is not null)
        {
            user.UpdateHomePage(request.HomePage);
        }

        var ua = userAgent.Length > 512 ? userAgent[..512] : userAgent;
        var comment = new Comment(user, request.Text, ip, ua, parent);
        _db.Comments.Add(comment);

        await _db.SaveChangesAsync(ct);

        return new CommentDto
        {
            Id = comment.Id,
            ParentId = comment.ParentId,
            UserName = user.UserName,
            Email = user.Email,
            HomePage = user.HomePage,
            Text = comment.Text,
            CreatedAt = comment.CreatedAt
        };
    }
}