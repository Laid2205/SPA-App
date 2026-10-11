using Microsoft.EntityFrameworkCore;
using CommentsApp.Infrastructure;
using CommentsApp.Api.Services;
using CommentsApp.Api.Caching;
using CommentsApp.Api.Events;
using CommentsApp.Api.Notifications;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IAttachmentService, AttachmentService>();

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

builder.Services.AddSingleton<INotificationQueue, NotificationQueue>();
builder.Services.AddSingleton<INotificationSender, LoggingNotificationSender>();
builder.Services.AddHostedService<NotificationWorker>();
builder.Services.AddSingleton<IMessageSanitizer, MessageSanitizer>();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICaptchaService, CaptchaService>();

builder.Services.AddScoped<IAttachmentService, AttachmentService>();

builder.Services.AddCors(options =>
    options.AddPolicy("frontend", policy => policy
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors("frontend");

app.UseAuthorization();

app.MapControllers();

app.Run();
