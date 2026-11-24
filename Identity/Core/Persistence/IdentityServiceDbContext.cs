using ChatFusion.Identity.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace ChatFusion.Identity.Core.Persistence;

/// <summary>
/// Контекст базы данных сервиса. Собственный класс на основе <see cref="DbContext"/>,
/// ASP.NET Core Identity не используется.
/// </summary>
public sealed class IdentityServiceDbContext(DbContextOptions<IdentityServiceDbContext> options) : DbContext(options)
{
    public const string Schema = "identity";

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityServiceDbContext).Assembly);
    }
}
