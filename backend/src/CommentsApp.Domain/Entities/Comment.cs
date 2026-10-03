namespace CommentsApp.Domain.Entities;

public class Comment
{
    private Comment() { }

    public Comment(User user, string text, string ipAddress, string userAgent, Comment? parent = null)
    {
        User = user;
        Text = text;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        Parent = parent;
        CreatedAt = DateTime.UtcNow;
    }

    public int Id { get; private set; }
    public string Text { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public string IpAddress { get; private set; } = null!;
    public string UserAgent { get; private set; } = null!;

    public int UserId { get; private set; }
    public User User { get; private set; } = null!;

    public int? ParentId { get; private set; }
    public Comment? Parent { get; private set; }
    public ICollection<Comment> Replies { get; private set; } = new List<Comment>();

    public Attachment? Attachment { get; private set; }

    public void AttachFile(Attachment attachment) => Attachment = attachment;
}