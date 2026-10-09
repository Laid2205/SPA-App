using System.ComponentModel.DataAnnotations;

namespace CommentsApp.Api.Dtos;

public class CreateCommentRequest
{
    [Required, StringLength(50), RegularExpression("^[a-zA-Z0-9]+$",
        ErrorMessage = "User Name may contain only latin letters and digits.")]
    public string UserName { get; set; } = null!;

    [Required, StringLength(254), EmailAddress,
 RegularExpression(@"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$",
     ErrorMessage = "Invalid e-mail format.")]
    public string Email { get; set; } = null!;

    [StringLength(2048), Url,
 RegularExpression(@"^https?://[^\s<>""'`]+$",
     ErrorMessage = "Home page must be a valid http:// or https:// URL without spaces or quotes.")]
    public string? HomePage { get; set; }

    [Required, StringLength(5000)]
    public string Text { get; set; } = null!;

    public int? ParentId { get; set; }

    [Required, StringLength(32)]
    public string CaptchaId { get; set; } = null!;

    [Required, StringLength(8), RegularExpression("^[a-zA-Z0-9]+$",
        ErrorMessage = "CAPTCHA may contain only latin letters and digits.")]
    public string CaptchaAnswer { get; set; } = null!;
}

public class CommentDto
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string UserName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? HomePage { get; set; }
    public string Text { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public List<CommentDto> Replies { get; set; } = new();
}

public class CommentQuery
{
    public int Page { get; set; } = 1;
    public string SortBy { get; set; } = "date";
    public bool Desc { get; set; } = true;
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}