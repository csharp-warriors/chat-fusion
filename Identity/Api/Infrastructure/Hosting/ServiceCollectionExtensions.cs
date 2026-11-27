using System.Net;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ChatFusion.Identity.Api.Application;
using ChatFusion.Identity.Core.Accounts;
using ChatFusion.Identity.Core.Domain;
using ChatFusion.Identity.Api.Infrastructure.Configuration;
using ChatFusion.Identity.Api.Infrastructure.OpenApi;
using ChatFusion.Identity.Core.Persistence;
using ChatFusion.Identity.Api.Infrastructure.Security;
using ChatFusion.Identity.Api.Infrastructure.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ChatFusion.Identity.Api.Infrastructure.Hosting;

/// <summary>
/// Регистрация сервисов приложения.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityServiceApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<JwtOptions>(configuration, JwtOptions.SectionName);
        services.AddValidatedOptions<UserSessionOptions>(configuration, UserSessionOptions.SectionName);
        services.AddValidatedOptions<AccountLockoutOptions>(configuration, AccountLockoutOptions.SectionName);
        services.AddValidatedOptions<PasswordHashingOptions>(configuration, PasswordHashingOptions.SectionName);
        services.AddValidatedOptions<AuthRateLimitOptions>(configuration, AuthRateLimitOptions.SectionName);
        services.AddValidatedOptions<RefreshCookieOptions>(configuration, RefreshCookieOptions.SectionName);
        services.AddValidatedOptions<CleanupOptions>(configuration, CleanupOptions.SectionName);
        services.AddValidatedOptions<TrustedProxyOptions>(configuration, TrustedProxyOptions.SectionName);

        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();

        services.AddPersistence();
        services.AddSecurity();
        services.AddApplication();
        services.AddWeb();

        return services;
    }

    private static void AddValidatedOptions<TOptions>(this IServiceCollection services, IConfiguration configuration, string sectionName)
        where TOptions : class =>
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

    private static void AddPersistence(this IServiceCollection services)
    {
        // Строка подключения читается при создании контекста, а не при регистрации:
        // так её можно переопределить в тестах и командах
        services.AddDbContextPool<IdentityServiceDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>()
                .GetConnectionString(DatabaseConfiguration.ConnectionStringName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Не задана строка подключения ConnectionStrings:{DatabaseConfiguration.ConnectionStringName}.");
            }

            DatabaseConfiguration.Configure(options, connectionString);
        });

        services.AddHealthChecks()
            .AddDbContextCheck<IdentityServiceDbContext>("database", tags: [HealthCheckTags.Ready]);
    }

    private static void AddSecurity(this IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IEmailNormalizer, EmailNormalizer>();
        services.AddSingleton<SigningKeyProvider>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<SigningKeyProvider, IOptions<JwtOptions>, TimeProvider>((options, keys, jwtOptions, timeProvider) =>
            {
                var jwt = jwtOptions.Value;

                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKeys = keys.ValidationKeys.Select(k => k.SecurityKey).ToList(),
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ClockSkew = jwt.ClockSkew,
                    NameClaimType = IdentityClaimTypes.Subject,
                    RoleClaimType = IdentityClaimTypes.Role,
                    RequireExpirationTime = true,

                    // Время берётся из TimeProvider, как и при выпуске токена
                    LifetimeValidator = (notBefore, expires, _, parameters) =>
                    {
                        var now = timeProvider.GetUtcNow().UtcDateTime;
                        return expires is { } expiresAt
                            && expiresAt > now - parameters.ClockSkew
                            && (notBefore is not { } notBeforeAt || notBeforeAt <= now + parameters.ClockSkew);
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build())
            .AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireRole(RoleCodes.Admin));
    }

    private static void AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<SessionService>();
        services.AddScoped<UserService>();
        services.AddScoped<AdminUserService>();
        services.AddHostedService<SessionCleanupBackgroundService>();
    }

    private static void AddWeb(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<UnhandledExceptionHandler>();

        services.AddSingleton<RefreshTokenCookies>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var problemDetailsService = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await problemDetailsService.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = { Status = StatusCodes.Status429TooManyRequests, Title = "Слишком много запросов. Повторите позже." },
                });
            };

            options.AddPolicy(RateLimitPolicies.Auth, httpContext =>
            {
                var limits = httpContext.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
                var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.PermitLimit,
                    Window = limits.Window,
                    QueueLimit = 0,
                });
            });
        });

        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<TrustedProxyOptions>>((options, trustedProxies) =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                foreach (var proxy in trustedProxies.Value.KnownProxies)
                {
                    options.KnownProxies.Add(IPAddress.Parse(proxy));
                }

                foreach (var network in trustedProxies.Value.KnownNetworks)
                {
                    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
                }
            });

        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddOperationTransformer<BearerSecurityRequirementTransformer>();
        });
    }
}

/// <summary>
/// Теги проверок состояния.
/// </summary>
public static class HealthCheckTags
{
    public const string Ready = "ready";
}
