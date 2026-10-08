using CommentsApp.Api.Dtos;
using CommentsApp.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CommentsApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommentsController : ControllerBase
{
    private readonly ICommentService _service;
    private readonly IMessageSanitizer _sanitizer;

    public CommentsController(ICommentService service, IMessageSanitizer sanitizer)
    {
        _service = service;
        _sanitizer = sanitizer;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<CommentDto>>> Get(
        [FromQuery] CommentQuery query, CancellationToken ct)
        => Ok(await _service.GetPageAsync(query, ct));

    [HttpPost]
    public async Task<ActionResult<CommentDto>> Create(
    CreateCommentRequest request, CancellationToken ct)
    {
        var sanitized = _sanitizer.Sanitize(request.Text);
        if (!sanitized.IsValid)
        {
            ModelState.AddModelError(nameof(request.Text), sanitized.Error!);
            return ValidationProblem(ModelState);
        }

        if (sanitized.Html.Length > 5000)
        {
            ModelState.AddModelError(nameof(request.Text), "Text is too long.");
            return ValidationProblem(ModelState);
        }

        request.Text = sanitized.Html;

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers.UserAgent.ToString();

        var created = await _service.CreateAsync(request, ip, userAgent, ct);
        if (created is null)
            return NotFound(new { error = "Parent comment not found." });

        return StatusCode(StatusCodes.Status201Created, created);
    }
}