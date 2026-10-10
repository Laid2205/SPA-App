using Microsoft.EntityFrameworkCore;
using CommentsApp.Infrastructure;
using CommentsApp.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<ICommentService, CommentService>();

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
