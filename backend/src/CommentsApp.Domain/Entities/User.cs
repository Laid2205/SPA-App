namespace CommentsApp.Domain.Entities;

public class User
{
    private User() { } // для EF

    public User(string userName, string email, string? homePage)
    {
        UserName = userName;
        Email = email;
        HomePage = homePage;
        CreatedAt = DateTime.UtcNow;
    }

    public int Id { get; private set; }
    public string UserName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string? HomePage { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public ICollection<Comment> Comments { get; private set; } = new List<Comment>();

    public void UpdateHomePage(string? homePage) => HomePage = homePage;
}