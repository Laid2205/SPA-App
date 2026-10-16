using Microsoft.EntityFrameworkCore;
using CommentsApp.Infrastructure;
using CommentsApp.Api.Services;
using CommentsApp.Api.Caching;
using CommentsApp.Api.Events;
using CommentsApp.Api.Notifications;
using CommentsApp.Api.Hubs;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddSingleton<IMessageSanitizer, MessageSanitizer>();
builder.Services.AddSingleton<ICaptchaService, CaptchaService>();

var redis = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redis))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redis;
        options.InstanceName = "spa-app:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddSingleton<ICommentsCache, DistributedCommentsCache>();
builder.Services.AddScoped<CommentService>();
builder.Services.AddScoped<ICommentService>(sp => new CachedCommentService(
    sp.GetRequiredService<CommentService>(),
    sp.GetRequiredService<ICommentsCache>()));

builder.Services.AddScoped<IEventPublisher, EventPublisher>();
builder.Services.AddScoped<IEventHandler<CommentCreated>, InvalidateCommentsCacheHandler>();
builder.Services.AddScoped<IEventHandler<CommentCreated>, EnqueueReplyNotificationHandler>();
builder.Services.AddScoped<IEventHandler<CommentCreated>, BroadcastCommentCreatedHandler>();

builder.Services.AddSignalR(options => options.MaximumReceiveMessageSize = 1024);

builder.Services.AddSingleton<INotificationQueue, NotificationQueue>();
builder.Services.AddSingleton<INotificationSender, LoggingNotificationSender>();
builder.Services.AddHostedService<NotificationWorker>();

builder.Services.AddCors(options =>
    options.AddPolicy("frontend", policy => policy
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var behindProxy = builder.Configuration.GetValue<bool>("UseForwardedHeaders");
if (behindProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

var app = builder.Build();

if (behindProxy) app.UseForwardedHeaders();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    for (var attempt = 1; ; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            break;
        }
        catch (Exception ex) when (attempt < 10)
        {
            logger.LogWarning(ex, "Database is not ready (attempt {Attempt}/10), retrying in 3 s", attempt);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("frontend");

app.UseAuthorization();

app.MapControllers();

app.MapHub<CommentsHub>("/hubs/comments");

app.MapFallbackToFile("{*path:regex(^(?!api/|hubs/).*$)}", "index.html");

app.Run();