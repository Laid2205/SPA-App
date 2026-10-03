namespace CommentsApp.Domain.Entities;

public enum AttachmentType
{
    Image = 1,
    Text = 2
}

public class Attachment
{
    private Attachment() { } // для EF

    public Attachment(AttachmentType type, string originalFileName, string storedFileName, long sizeBytes)
    {
        Type = type;
        OriginalFileName = originalFileName;
        StoredFileName = storedFileName;
        SizeBytes = sizeBytes;
    }

    public int Id { get; private set; }
    public AttachmentType Type { get; private set; }
    public string OriginalFileName { get; private set; } = null!;
    public string StoredFileName { get; private set; } = null!;
    public long SizeBytes { get; private set; }

    public int CommentId { get; private set; }
    public Comment Comment { get; private set; } = null!;
}