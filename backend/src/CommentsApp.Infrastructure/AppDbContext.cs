using CommentsApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CommentsApp.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    // Все DateTime читаются из БД как UTC, поэтому в JSON дата уходит с суффиксом Z
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.Property(x => x.UserName).HasMaxLength(50).IsRequired();
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.Property(x => x.HomePage).HasMaxLength(2048);
            e.HasIndex(x => new { x.UserName, x.Email }).IsUnique();
        });

        modelBuilder.Entity<Comment>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(5000).IsRequired();
            e.Property(x => x.IpAddress).HasMaxLength(45).IsRequired();
            e.Property(x => x.UserAgent).HasMaxLength(512).IsRequired();

            e.HasOne(x => x.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Restrict обязателен: в SQL Server каскадное удаление
            // по самоссылке приводит к ошибке "multiple cascade paths"
            e.HasOne(x => x.Parent)
                .WithMany(x => x.Replies)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.ParentId);
            e.HasIndex(x => x.CreatedAt); // сортировка корневых комментариев по дате
        });

        modelBuilder.Entity<Attachment>(e =>
        {
            e.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
            e.Property(x => x.StoredFileName).HasMaxLength(100).IsRequired();

            e.HasOne(x => x.Comment)
                .WithOne(c => c.Attachment)
                .HasForeignKey<Attachment>(x => x.CommentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(
        v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}