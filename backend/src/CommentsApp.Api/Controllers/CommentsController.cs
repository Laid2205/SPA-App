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
    private readonly ICaptchaService _captcha;

    public CommentsController(
        ICommentService service, IMessageSanitizer sanitizer, ICaptchaService captcha)
    {
        _service = service;
        _sanitizer = sanitizer;
        _captcha = captcha;
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

        if (!_captcha.Validate(request.CaptchaId, request.CaptchaAnswer))
        {
            ModelState.AddModelError(nameof(request.CaptchaAnswer), "Invalid or expired CAPTCHA.");
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