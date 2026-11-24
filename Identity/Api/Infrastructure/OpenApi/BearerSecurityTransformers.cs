using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ChatFusion.Identity.Api.Infrastructure.OpenApi;

/// <summary>
/// Описание документа и схема безопасности Bearer.
/// </summary>
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public const string SchemeName = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "ChatFusion.Identity API",
            Version = "v1",
            Description = "Сервис аутентификации и авторизации ChatFusion: регистрация, вход, JWT, сессии и роли. "
                + "Другие сервисы проверяют токены по открытым ключам из /.well-known/jwks.json.",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access-токен из ответа POST /api/v1/auth/login.",
        };

        return Task.CompletedTask;
    }
}

/// <summary>
/// Проставляет требование Bearer только на операции, требующие аутентификации.
/// </summary>
public sealed class BearerSecurityRequirementTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var isAnonymous = context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
        if (isAnonymous)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(BearerSecuritySchemeTransformer.SchemeName, context.Document)] = [],
        });

        return Task.CompletedTask;
    }
}
