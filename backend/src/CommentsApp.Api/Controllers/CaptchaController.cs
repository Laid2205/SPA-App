using CommentsApp.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CommentsApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CaptchaController : ControllerBase
{
    private readonly ICaptchaService _captcha;

    public CaptchaController(ICaptchaService captcha) => _captcha = captcha;

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public ActionResult<CaptchaChallenge> Get() => Ok(_captcha.Create());
}