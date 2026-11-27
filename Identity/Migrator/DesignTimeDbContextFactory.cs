using ChatFusion.Identity.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ChatFusion.Identity.Migrator;

/// <summary>
/// Фабрика для <c>dotnet ef</c>: создание миграций не требует запуска мигратора и доступа к БД.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<IdentityServiceDbContext>
{
    public IdentityServiceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityServiceDbContext>();
        DatabaseConfiguration.Configure(options, "Host=localhost;Database=chatfusion_identity_design");
        return new IdentityServiceDbContext(options.Options);
    }
}
