namespace CommentsApp.Api.Services;

public sealed record CaptchaChallenge(string Id, string Image);

public interface ICaptchaService
{
    CaptchaChallenge Create();

    bool Validate(string id, string answer);
}