using CommentsApp.Domain.Entities;

namespace CommentsApp.Api.Services;

public sealed record PreparedFile(
    AttachmentType Type, string OriginalFileName, string Extension, byte[] Content);

public sealed record PrepareResult(PreparedFile? File, string? Error)
{
    public static PrepareResult Ok(PreparedFile file) => new(file, null);
    public static PrepareResult Fail(string error) => new(null, error);
}

public sealed record AttachmentFile(string PhysicalPath, string ContentType);

public interface IAttachmentService
{
    Task<PrepareResult> PrepareAsync(IFormFile file, CancellationToken ct);

    Task<Attachment> SaveAsync(PreparedFile file, CancellationToken ct);

    void Delete(Attachment attachment);

    Task<AttachmentFile?> OpenAsync(int id, CancellationToken ct);
}