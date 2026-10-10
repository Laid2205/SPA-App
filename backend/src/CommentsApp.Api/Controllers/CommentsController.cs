using CommentsApp.Api.Dtos;
using CommentsApp.Api.Services;
using CommentsApp.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace CommentsApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommentsController : ControllerBase
{
    private readonly ICommentService _service;
    private readonly IMessageSanitizer _sanitizer;
    private readonly ICaptchaService _captcha;
    private readonly IAttachmentService _attachments;

    public CommentsController(
        ICommentService service,
        IMessageSanitizer sanitizer,
        ICaptchaService captcha,
        IAttachmentService attachments)
    {
        _service = service;
        _sanitizer = sanitizer;
        _captcha = captcha;
        _attachments = attachments;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<CommentDto>>> Get(
        [FromQuery] CommentQuery query, CancellationToken ct)
        => Ok(await _service.GetPageAsync(query, ct));

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<CommentDto>> Create(
        [FromForm] CreateCommentRequest request, CancellationToken ct)
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

        PreparedFile? prepared = null;
        if (request.File is not null)
        {
            var result = await _attachments.PrepareAsync(request.File, ct);
            if (result.File is null)
            {
                ModelState.AddModelError(nameof(request.File), result.Error!);
                return ValidationProblem(ModelState);
            }
            prepared = result.File;
        }

        if (!_captcha.Validate(request.CaptchaId, request.CaptchaAnswer))
        {
            ModelState.AddModelError(nameof(request.CaptchaAnswer), "Invalid or expired CAPTCHA.");
            return ValidationProblem(ModelState);
        }

        request.Text = sanitized.Html;

        Attachment? attachment = null;
        if (prepared is not null)
            attachment = await _attachments.SaveAsync(prepared, ct);

        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = Request.Headers.UserAgent.ToString();

            var created = await _service.CreateAsync(request, ip, userAgent, attachment, ct);
            if (created is null)
            {
                if (attachment is not null) _attachments.Delete(attachment);
                return NotFound(new { error = "Parent comment not found." });
            }

            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch
        {
            if (attachment is not null) _attachments.Delete(attachment);
            throw;
        }
    }
}