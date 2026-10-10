using CommentsApp.Api.Dtos;
using CommentsApp.Domain.Entities;

namespace CommentsApp.Api.Services;

public interface ICommentService
{
    Task<PagedResult<CommentDto>> GetPageAsync(CommentQuery query, CancellationToken ct);

    Task<CommentDto?> CreateAsync(
        CreateCommentRequest request, string ip, string userAgent, Attachment? attachment, CancellationToken ct);
}