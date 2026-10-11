namespace CommentsApp.Api.Services;

public sealed record CaptchaChallenge(string Id, string Image);

public interface ICaptchaService
{
    Task<CaptchaChallenge> CreateAsync(CancellationToken ct);

    Task<bool> ValidateAsync(string id, string answer, CancellationToken ct);
}