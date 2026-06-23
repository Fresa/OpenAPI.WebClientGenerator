using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.OpenApi;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class SecurityGenerator
{
    private readonly IDictionary<string, IOpenApiSecurityScheme> _securitySchemes;
    private readonly Dictionary<string, List<string>>[] _topLevelSecuritySchemeGroups;
    private readonly SecuritySchemaTranslations _securitySchemaTranslations;
    
    private readonly ConcurrentDictionary<string, HashSet<(OpenApiOperation Operation, ParameterGenerator? Parameter)>> _securitySchemeParameters = new();

    public SecurityGenerator(OpenApiDocument openApiDocument)
    {
        _securitySchemes = openApiDocument.Components?.SecuritySchemes ??
                           new Dictionary<string, IOpenApiSecurityScheme>();
        _securitySchemaTranslations = new SecuritySchemaTranslations(openApiDocument);
        _topLevelSecuritySchemeGroups = _securitySchemaTranslations.GetSecuritySchemeGroups(openApiDocument.Security) ?? [];
    }

    internal SourceCode? GenerateSecuritySchemeClass(string @namespace)
    {
        if (!_securitySchemes.Any())
        {
            return null;
        }
        return new SourceCode("SecuritySchemes.g.cs", 
$$"""
using System.Collections.Immutable;

namespace {{@namespace}};

/// <summary>
/// Defines security schemes that can be used by the operations
/// </summary>
internal static class SecuritySchemes 
{{{_securitySchemes.AggregateToString(pair =>
    {
        var schemeName = pair.Key;
        var className = schemeName.ToPascalCase();
        var scheme = pair.Value;
        return scheme.Type == null ? string.Empty : 
$$"""
    internal const string {{className}}Key = "{{pair.Key}}";
{{scheme.Description.AsComment("summary", "para").Indent(4)}}
    internal static class {{className}}
    {{{new []
    {
        GenerateConst(nameof(scheme.Type), scheme.Type?.GetDisplayName()),
        GenerateConst(nameof(scheme.Scheme), scheme.Scheme),
        GenerateConst(nameof(scheme.BearerFormat), scheme.BearerFormat),
        GenerateConst(nameof(scheme.OpenIdConnectUrl), scheme.OpenIdConnectUrl?.ToString()),
        GenerateGetParameterMethods(schemeName, scheme),
        $"internal const bool {nameof(scheme.Deprecated)} = {scheme.Deprecated.ToString().ToLowerInvariant()};",
        GenerateFlowsObject(nameof(scheme.Flows), scheme.Flows)
    }.RemoveEmptyLines().AggregateToString().Indent(8)}}
    }
""";
    })}}
}
""");
    }

    private static string GenerateConst(string name, string? value) =>
        value == null
            ? string.Empty
            : $"""
               internal const string {name} = "{value}";
               """;

    private string GenerateGetParameterMethods(string schemeName, IOpenApiSecurityScheme scheme)
    {
        if (scheme.Name == null || scheme.In == null)
        {
            return string.Empty;
        }

        var hasNonDefinedParameters = true;
        var parameterGenerators = Array.Empty<ParameterGenerator>();
        var parameterFullyQualifiedTypeNames = Array.Empty<string>();
        if (_securitySchemeParameters.TryGetValue(schemeName, out var securitySchemeParameters))
        {
            hasNonDefinedParameters = securitySchemeParameters.Any(tuple => tuple.Parameter == null);
            parameterGenerators = securitySchemeParameters
                .Where(tuple => tuple.Parameter != null)
                .Select(tuple => tuple.Parameter!)
                .ToArray();
            parameterFullyQualifiedTypeNames = parameterGenerators.Select(generator => generator.FullyQualifiedTypeName)
                .Distinct()
                .ToArray();
        }
        
        return 
$"""
{GenerateConst(nameof(scheme.Name), scheme.Name)}
{GenerateConst(nameof(scheme.In), scheme.In.GetDisplayName())}

""";
    }
    
    private static string GenerateFlowsObject(string className, OpenApiOAuthFlows? flows) =>
        flows == null ? string.Empty : 
$$"""
internal static class {{className}}
{{{new []
{
    GenerateFlowObject(nameof(flows.AuthorizationCode), flows.AuthorizationCode),
    GenerateFlowObject(nameof(flows.ClientCredentials), flows.ClientCredentials),
    GenerateFlowObject(nameof(flows.DeviceAuthorization), flows.DeviceAuthorization),
    GenerateFlowObject(nameof(flows.Implicit), flows.Implicit),
    GenerateFlowObject(nameof(flows.Password), flows.Password)
}.RemoveEmptyLines().AggregateToString().Indent(4)}}
}
""";

    private static string GenerateFlowObject(string className, OpenApiOAuthFlow? flow) =>
        flow == null ? string.Empty : 
$$"""
internal static class {{className}}
{{{new []
{
    GenerateConst(nameof(flow.AuthorizationUrl), flow.AuthorizationUrl?.ToString()),
    GenerateConst(nameof(flow.DeviceAuthorizationUrl), flow.DeviceAuthorizationUrl?.ToString()),
    GenerateConst(nameof(flow.RefreshUrl), flow.RefreshUrl?.ToString()),
    GenerateConst(nameof(flow.TokenUrl), flow.TokenUrl?.ToString()),
    flow.Scopes == null ? string.Empty : 
$$"""
internal static readonly ImmutableDictionary<string, string> {{nameof(flow.Scopes)}} = 
    ImmutableDictionary.CreateRange<string, string>([{{flow.Scopes.AggregateToString(scope => 
$"""
        new("{scope.Key}", "{scope.Value}"),
""").TrimEnd(',')}}
]);
"""
}.RemoveEmptyLines().AggregateToString().Indent(4)}}
}
""";
    
    private Dictionary<OpenApiSecuritySchemeReference, ParameterGenerator?> GetSecuritySchemes(OpenApiOperation operation, ParameterGenerator[] parameters)
    {
        var nullableSecuritySchemeParameters =
            operation.Security?
                .SelectMany(requirement =>
                    requirement
                        .Select(pair => pair.Key))
                .Distinct()
                .Select(reference => (Scheme: reference,
                    Parameter: parameters.FirstOrDefault(generator => generator.IsSecuritySchemeParameter(reference)) ?? null))
                .ToArray()
            ?? [];
        
        foreach (var (scheme, parameter) in nullableSecuritySchemeParameters)
        {
            _securitySchemeParameters.AddOrUpdate(_securitySchemaTranslations.GetSecuritySchemeName(scheme),
                _ => [(operation, parameter)],
                (_, list) =>
                {
                    list.Add((operation, parameter));
                    return list;
                });
        }

        return nullableSecuritySchemeParameters
            .ToDictionary(pair => pair.Scheme, pair => pair.Parameter);
    }
    
    internal bool TryGetAuthenticationGenerator(
        OpenApiOperation operation, 
        ParameterGenerator[] parameters,
        [NotNullWhen(true)] out AuthenticationGenerator? authenticationGenerator)
    {
        var securityRequirementGroups =
            _securitySchemaTranslations.GetSecuritySchemeGroups(operation.Security) ?? _topLevelSecuritySchemeGroups;
        var requiresAuth = securityRequirementGroups.Any();
        if (!requiresAuth)
        {
            authenticationGenerator = null;
            return false;
        }

        var securitySchemes = GetSecuritySchemes(operation, parameters);
        authenticationGenerator = new AuthenticationGenerator(securitySchemes, _securitySchemaTranslations);
        return true;
    }
}