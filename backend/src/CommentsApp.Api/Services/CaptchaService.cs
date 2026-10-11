using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Microsoft.Extensions.Caching.Distributed;

namespace CommentsApp.Api.Services;

public class CaptchaService : ICaptchaService
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 5;
    private const int Width = 160;
    private const int Height = 60;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private static readonly Lazy<FontFamily> Family = new(ResolveFontFamily);

    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = Ttl
    };

    private readonly IDistributedCache _cache;

    public CaptchaService(IDistributedCache cache) => _cache = cache;

    public async Task<CaptchaChallenge> CreateAsync(CancellationToken ct)
    {
        var chars = new char[CodeLength];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];

        var code = new string(chars);
        var id = Guid.NewGuid().ToString("N");

        await _cache.SetStringAsync(Key(id), code, CacheOptions, ct);

        var image = "data:image/png;base64," + Convert.ToBase64String(Render(code));
        return new CaptchaChallenge(id, image);
    }

    public async Task<bool> ValidateAsync(string id, string answer, CancellationToken ct)
    {
        var key = Key(id);
        var code = await _cache.GetStringAsync(key, ct);
        if (code is null)
            return false;

        await _cache.RemoveAsync(key, CancellationToken.None);

        return string.Equals(code, answer.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string Key(string id) => $"captcha:{id}";

    private static byte[] Render(string code)
    {
        var font = Family.Value.CreateFont(34, FontStyle.Bold);

        using var image = new Image<Rgba32>(Width, Height);
        image.Mutate(ctx =>
        {
            ctx.Fill(Color.White);

            for (var i = 0; i < 6; i++)
                ctx.DrawLine(RandomColor(120, 200), 1.5f, RandomPoint(), RandomPoint());

            var step = (Width - 30) / (float)code.Length;
            for (var i = 0; i < code.Length; i++)
            {
                var options = new RichTextOptions(font)
                {
                    Origin = new PointF(15 + i * step, 8 + RandomNumberGenerator.GetInt32(-4, 7))
                };
                ctx.DrawText(options, code[i].ToString(), RandomColor(20, 110));
            }

            for (var i = 0; i < 3; i++)
                ctx.DrawLine(RandomColor(60, 160), 1f, RandomPoint(), RandomPoint());
        });

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static PointF RandomPoint() => new(
        RandomNumberGenerator.GetInt32(0, Width),
        RandomNumberGenerator.GetInt32(0, Height));

    private static Color RandomColor(int min, int max) => Color.FromRgb(
        (byte)RandomNumberGenerator.GetInt32(min, max),
        (byte)RandomNumberGenerator.GetInt32(min, max),
        (byte)RandomNumberGenerator.GetInt32(min, max));

    private static FontFamily ResolveFontFamily()
    {
        foreach (var name in new[] { "Arial", "Verdana", "DejaVu Sans", "Liberation Sans" })
            if (SystemFonts.TryGet(name, out var family))
                return family;

        if (SystemFonts.Families.Any())
            return SystemFonts.Families.First();

        throw new InvalidOperationException(
            "No system fonts found. In Docker install them, e.g. 'apt-get install -y fonts-dejavu-core'.");
    }
}