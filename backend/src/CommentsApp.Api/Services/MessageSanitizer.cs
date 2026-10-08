using System.Text;
using System.Text.RegularExpressions;

namespace CommentsApp.Api.Services;

public class MessageSanitizer : IMessageSanitizer
{
    private static readonly HashSet<string> AllowedTags = new() { "a", "code", "i", "strong" };

    private static readonly Regex TagCandidate =
        new(@"</?[a-zA-Z][^<>]*>", RegexOptions.Compiled);

    private static readonly Regex TagParts =
        new(@"^<(?<close>/)?(?<name>[a-zA-Z][a-zA-Z0-9]*)(?<attrs>[^<>]*)>$", RegexOptions.Compiled);

    private static readonly Regex Attribute =
        new(@"\G\s+(?<name>[a-zA-Z]+)\s*=\s*""(?<value>[^""<>]*)""", RegexOptions.Compiled);

    public SanitizeResult Sanitize(string input)
    {
        var result = new StringBuilder();
        var open = new Stack<string>();
        var position = 0;

        foreach (Match match in TagCandidate.Matches(input))
        {
            result.Append(EncodeText(input.Substring(position, match.Index - position)));
            position = match.Index + match.Length;

            var parts = TagParts.Match(match.Value);
            var name = parts.Groups["name"].Value.ToLowerInvariant();
            var isClosing = parts.Groups["close"].Success;
            var attrs = parts.Groups["attrs"].Value;

            if (!AllowedTags.Contains(name))
                return SanitizeResult.Fail(
                    $"Tag <{name}> is not allowed. Allowed tags: <a>, <code>, <i>, <strong>.");

            if (isClosing)
            {
                if (!string.IsNullOrWhiteSpace(attrs))
                    return SanitizeResult.Fail($"Closing tag </{name}> must not have attributes.");

                if (open.Count == 0 || open.Peek() != name)
                    return SanitizeResult.Fail($"Closing tag </{name}> does not match an open tag.");

                open.Pop();
                result.Append("</").Append(name).Append('>');
                continue;
            }

            if (name == "a")
            {
                if (open.Contains("a"))
                    return SanitizeResult.Fail("Nested links are not allowed.");

                if (!TryBuildLink(attrs, out var linkHtml, out var error))
                    return SanitizeResult.Fail(error);

                result.Append(linkHtml);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(attrs))
                    return SanitizeResult.Fail($"Tag <{name}> must not have attributes.");

                result.Append('<').Append(name).Append('>');
            }

            open.Push(name);
        }

        result.Append(EncodeText(input.Substring(position)));

        if (open.Count > 0)
            return SanitizeResult.Fail($"Tag <{open.Peek()}> is not closed.");

        return SanitizeResult.Ok(result.ToString());
    }

    private static bool TryBuildLink(string attrs, out string html, out string error)
    {
        html = string.Empty;
        error = string.Empty;

        string? href = null;
        string? title = null;
        var pos = 0;

        Match m;
        while ((m = Attribute.Match(attrs, pos)).Success)
        {
            var attrName = m.Groups["name"].Value.ToLowerInvariant();
            var value = m.Groups["value"].Value;
            pos = m.Index + m.Length;

            switch (attrName)
            {
                case "href" when href is null: href = value; break;
                case "title" when title is null: title = value; break;
                case "href" or "title":
                    error = $"Attribute '{attrName}' is specified twice.";
                    return false;
                default:
                    error = $"Attribute '{attrName}' is not allowed on <a>. Allowed: href, title.";
                    return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(attrs[pos..]))
        {
            error = "Invalid <a> tag. Use: <a href=\"https://...\" title=\"...\">.";
            return false;
        }

        if (href is null)
        {
            error = "Attribute 'href' is required on <a>.";
            return false;
        }

        if (!Uri.TryCreate(href, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = "Link must be an absolute http:// or https:// URL.";
            return false;
        }

        var sb = new StringBuilder("<a href=\"").Append(EncodeAttribute(href)).Append('"');
        if (!string.IsNullOrEmpty(title))
            sb.Append(" title=\"").Append(EncodeAttribute(title)).Append('"');
        sb.Append('>');

        html = sb.ToString();
        return true;
    }

    private static string EncodeText(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string EncodeAttribute(string s) =>
        EncodeText(s).Replace("\"", "&quot;");
}