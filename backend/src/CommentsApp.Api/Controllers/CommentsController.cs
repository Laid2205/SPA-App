using CommentsApp.Api.Dtos;
using CommentsApp.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CommentsApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommentsController : ControllerBase
{
    private readonly ICommentService _service;

    public CommentsController(ICommentService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<PagedResult<CommentDto>>> Get(
        [FromQuery] CommentQuery query, CancellationToken ct)
        => Ok(await _service.GetPageAsync(query, ct));

    [HttpPost]
    public async Task<ActionResult<CommentDto>> Create(
        CreateCommentRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers.UserAgent.ToString();

        var created = await _service.CreateAsync(request, ip, userAgent, ct);
        if (created is null)
            return NotFound(new { error = "Parent comment not found." });

        return StatusCode(StatusCodes.Status201Created, created);
    }
}