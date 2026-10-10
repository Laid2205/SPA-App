using CommentsApp.Api.Services;

namespace CommentsApp.Tests;

public class MessageSanitizerTests
{
    private readonly MessageSanitizer _sanitizer = new();

    [Theory]
    [InlineData("Hello", "Hello")]
    [InlineData("<strong>a</strong>", "<strong>a</strong>")]
    [InlineData("<i>a</i> <code>b</code>", "<i>a</i> <code>b</code>")]
    [InlineData("<STRONG>x</STRONG>", "<strong>x</strong>")]
    [InlineData("<strong><i>x</i></strong>", "<strong><i>x</i></strong>")]
    [InlineData("2 < 3 & 4 > 1", "2 &lt; 3 &amp; 4 &gt; 1")]
    [InlineData("<a href=\"https://example.com\" title=\"Site\">link</a>",
                "<a href=\"https://example.com\" title=\"Site\">link</a>")]
    [InlineData("<a href=\"https://example.com\">link</a>",
                "<a href=\"https://example.com\">link</a>")]
    public void Valid_input_is_accepted_and_normalized(string input, string expected)
    {
        var result = _sanitizer.Sanitize(input);

        Assert.True(result.IsValid, result.Error);
        Assert.Equal(expected, result.Html);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<b>bold</b>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<strong>text")]
    [InlineData("text</strong>")]
    [InlineData("<i><strong>a</i></strong>")]
    [InlineData("<strong class=\"x\">a</strong>")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>")]
    [InlineData("<a href=\"https://x.com\" onclick=\"evil()\">x</a>")]
    [InlineData("<a href='https://x.com'>x</a>")]
    [InlineData("<a>no href</a>")]
    [InlineData("<a href=\"https://a.com\"><a href=\"https://b.com\">x</a></a>")]
    public void Invalid_input_is_rejected_with_error(string input)
    {
        var result = _sanitizer.Sanitize(input);

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void Unterminated_tag_is_escaped_as_plain_text()
    {
        var result = _sanitizer.Sanitize("<script");

        Assert.True(result.IsValid);
        Assert.Equal("&lt;script", result.Html);
    }
}