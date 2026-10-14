using System.Text;
using CommentsApp.Domain.Entities;
using CommentsApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;

namespace CommentsApp.Api.Services;

public class AttachmentService : IAttachmentService
{
    public const long MaxTextBytes = 100 * 1024;
    public const long MaxImageBytes = 5 * 1024 * 1024;
    public const int MaxWidth = 320;
    public const int MaxHeight = 240;
    private const long MaxPixels = 25_000_000;

    private readonly AppDbContext _db;
    private readonly string _root;

    public AttachmentService(AppDbContext db, IWebHostEnvironment env, IConfiguration config)
    {
        _db = db;
        _root = config["Storage:UploadsPath"] ?? Path.Combine(env.ContentRootPath, "uploads");
    }

    public async Task<PrepareResult> PrepareAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return PrepareResult.Fail("File is empty.");

        var originalName = CleanName(file.FileName);
        var ext = Path.GetExtension(originalName).ToLowerInvariant();

        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" => await PrepareImageAsync(file, originalName, ct),
            ".txt" => await PrepareTextAsync(file, originalName, ct),
            _ => PrepareResult.Fail("Allowed files: JPG, GIF, PNG images and TXT text files.")
        };
    }

    private static async Task<PrepareResult> PrepareImageAsync(
        IFormFile file, string originalName, CancellationToken ct)
    {
        if (file.Length > MaxImageBytes)
            return PrepareResult.Fail("Image is too large (max 5 MB).");

        await using var input = file.OpenReadStream();

        try
        {
            var info = await Image.IdentifyAsync(input, ct);
            if ((long)info.Width * info.Height > MaxPixels)
                return PrepareResult.Fail("Image dimensions are too large.");

            input.Position = 0;
            using var image = await Image.LoadAsync(input, ct);

            var format = image.Metadata.DecodedImageFormat;
            var ext = format?.Name switch
            {
                "JPEG" => ".jpg",
                "PNG" => ".png",
                "GIF" => ".gif",
                _ => null
            };
            if (format is null || ext is null)
                return PrepareResult.Fail("Allowed image formats: JPG, GIF, PNG.");

            if (image.Width > MaxWidth || image.Height > MaxHeight)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(MaxWidth, MaxHeight),
                    Mode = ResizeMode.Max
                }));
            }

            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.XmpProfile = null;

            using var ms = new MemoryStream();
            await image.SaveAsync(ms, format, ct);

            return PrepareResult.Ok(
                new PreparedFile(AttachmentType.Image, originalName, ext, ms.ToArray()));
        }
        catch (Exception ex) when (ex is UnknownImageFormatException
                                      or InvalidImageContentException
                                      or NotSupportedException)
        {
            return PrepareResult.Fail("File is not a valid JPG, GIF or PNG image.");
        }
    }

    private static async Task<PrepareResult> PrepareTextAsync(
        IFormFile file, string originalName, CancellationToken ct)
    {
        if (file.Length > MaxTextBytes)
            return PrepareResult.Fail("Text file is too large (max 100 KB).");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        if (Array.IndexOf(bytes, (byte)0) >= 0)
            return PrepareResult.Fail("File is not a valid text file.");

        try
        {
            new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return PrepareResult.Fail("Text file must be UTF-8 encoded.");
        }

        return PrepareResult.Ok(new PreparedFile(AttachmentType.Text, originalName, ".txt", bytes));
    }

    public async Task<Attachment> SaveAsync(PreparedFile file, CancellationToken ct)
    {
        Directory.CreateDirectory(_root);

        var storedName = $"{Guid.NewGuid():N}{file.Extension}";
        await File.WriteAllBytesAsync(Path.Combine(_root, storedName), file.Content, ct);

        return new Attachment(file.Type, file.OriginalFileName, storedName, file.Content.LongLength);
    }

    public void Delete(Attachment attachment)
    {
        var path = Path.Combine(_root, attachment.StoredFileName);
        if (File.Exists(path)) File.Delete(path);
    }

    public async Task<AttachmentFile?> OpenAsync(int id, CancellationToken ct)
    {
        var a = await _db.Attachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return null;

        var path = Path.Combine(_root, a.StoredFileName);
        if (!File.Exists(path)) return null;

        var contentType = Path.GetExtension(a.StoredFileName) switch
        {
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            _ => "text/plain; charset=utf-8"
        };

        return new AttachmentFile(path, contentType);
    }

    private static string CleanName(string name)
    {
        var clean = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(clean)) clean = "file";
        return clean.Length > 255 ? clean[^255..] : clean;
    }
}