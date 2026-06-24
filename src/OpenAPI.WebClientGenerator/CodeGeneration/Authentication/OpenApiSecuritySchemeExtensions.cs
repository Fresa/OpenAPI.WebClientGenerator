using System;
using Microsoft.OpenApi;

namespace OpenAPI.WebClientGenerator.CodeGeneration.Authentication;

public static class OpenApiSecuritySchemeExtensions
{
    internal static SecurityScheme.Argument[] GetSchemeConstructorArguments(this IOpenApiSecurityScheme scheme) =>
        scheme.Type switch
        {
            SecuritySchemeType.ApiKey => [SecurityScheme.ApiKey.Key],
            SecuritySchemeType.Http when string.Equals(scheme.Scheme, "basic", StringComparison.OrdinalIgnoreCase) =>
                [SecurityScheme.Http.Username, SecurityScheme.Http.Password],
            _ when scheme.Type is SecuritySchemeType.OAuth2 or SecuritySchemeType.OpenIdConnect
                   || string.Equals(scheme.Scheme, "bearer", StringComparison.OrdinalIgnoreCase) =>
                [SecurityScheme.Bearer.Token],
            SecuritySchemeType.MutualTLS => [],
            _ => [SecurityScheme.Custom.Apply],
        };
}