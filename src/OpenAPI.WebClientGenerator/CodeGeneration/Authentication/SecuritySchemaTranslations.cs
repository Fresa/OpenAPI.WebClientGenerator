using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi;

namespace OpenAPI.WebClientGenerator.CodeGeneration.Authentication;

internal sealed class SecuritySchemaTranslations(OpenApiDocument openApiDocument)
{
    private readonly IDictionary<string, IOpenApiSecurityScheme> _securitySchemes = openApiDocument.Components?.SecuritySchemes ??
        new Dictionary<string, IOpenApiSecurityScheme>();
    
    internal string GetSecuritySchemeName(OpenApiSecuritySchemeReference reference)
        => _securitySchemes.First(pair => pair.Value == reference.Target).Key;
    
    internal Dictionary<OpenApiSecuritySchemeReference, List<string>>[]? GetSecuritySchemeGroups(IList<OpenApiSecurityRequirement>? securityRequirements) =>
        securityRequirements?
            .Select(requirement =>
                requirement.ToDictionary(
                    pair => pair.Key, 
                    pair => pair.Value))
            .ToArray();
}