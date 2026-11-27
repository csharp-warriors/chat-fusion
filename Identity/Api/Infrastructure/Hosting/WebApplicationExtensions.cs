using ChatFusion.Identity.Api.Infrastructure.OpenApi;
using ChatFusion.Identity.Api.Infrastructure.Web;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Scalar.AspNetCore;

namespace ChatFusion.Identity.Api.Infrastructure.Hosting;

/// <summary>
/// Конвейер обработки запросов.
/// </summary>
public static class WebApplicationExtensions
{
    public static WebApplication UseIdentityServiceApi(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseMiddleware<SecurityHeadersMiddleware>();

        // HTTPS завершается на обратном прокси, поэтому перенаправления на HTTPS внутри сети Docker нет
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous()
            .DisableRateLimiting();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(HealthCheckTags.Ready) })
            .AllowAnonymous()
            .DisableRateLimiting();

        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Scalar:Enabled"))
        {
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference(options => options
                    .WithTitle("ChatFusion.Identity API")
                    .AddPreferredSecuritySchemes(BearerSecuritySchemeTransformer.SchemeName))
                .AllowAnonymous();
        }

        return app;
    }
}
