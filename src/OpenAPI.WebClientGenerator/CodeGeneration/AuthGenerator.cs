using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class AuthGenerator
{
    private readonly IDictionary<string, IOpenApiSecurityScheme> _securitySchemes;
    private readonly Dictionary<string, List<string>>[] _topLevelSecuritySchemeGroups;

    private readonly ConcurrentDictionary<string, HashSet<(OpenApiOperation Operation, ParameterGenerator? Parameter)>> _securitySchemeParameters = new();

    public AuthGenerator(OpenApiDocument openApiDocument)
    {
        _securitySchemes = openApiDocument.Components?.SecuritySchemes ??
                           new Dictionary<string, IOpenApiSecurityScheme>();
        _topLevelSecuritySchemeGroups = GetSecuritySchemeGroups(openApiDocument.Security) ?? [];
        HasSecuritySchemes = _securitySchemes.Any();
    }

    internal bool HasSecuritySchemes { get; }
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
    
    private Dictionary<string, List<string>>[]? GetSecuritySchemeGroups(IList<OpenApiSecurityRequirement>? securityRequirements) =>
        securityRequirements?
            .Select(requirement =>
                requirement.ToDictionary(
                    pair => GetSecuritySchemeName(pair.Key), 
                    pair => pair.Value))
            .ToArray();
    private string GetSecuritySchemeName(OpenApiSecuritySchemeReference reference)
        => _securitySchemes.First(pair => pair.Value == reference.Target).Key;

    
    private Dictionary<OpenApiSecuritySchemeReference, ParameterGenerator> GetSecuritySchemeParameters(OpenApiOperation operation, ParameterGenerator[] parameters)
    {
        var nullableSecuritySchemeParameters =
            operation.Security?
                .SelectMany(requirement =>
                    requirement.Where(pair => pair.Key.In != null && pair.Key.Name != null)
                        .Select(pair => pair.Key))
                .Distinct()
                .Select(reference => (Scheme: reference,
                    Parameter: parameters.FirstOrDefault(generator => generator.IsSecuritySchemeParameter(reference)) ?? null))
                .ToArray()
            ?? [];
        
        foreach (var (scheme, parameter) in nullableSecuritySchemeParameters)
        {
            _securitySchemeParameters.AddOrUpdate(GetSecuritySchemeName(scheme),
                _ => [(operation, parameter)],
                (_, list) =>
                {
                    list.Add((operation, parameter));
                    return list;
                });
        }

        return nullableSecuritySchemeParameters
            .Where(pair => pair.Parameter != null)
            .ToDictionary(pair => pair.Scheme, pair => pair.Parameter!);
    }
    
    internal string GenerateAuthParameters(OpenApiOperation operation, ParameterGenerator[] parameters,
        out bool requiresAuth)
    {
        var securityRequirementGroups =
            GetSecuritySchemeGroups(operation.Security) ?? _topLevelSecuritySchemeGroups;
        requiresAuth = securityRequirementGroups.Any();
        if (!requiresAuth)
        {
            return string.Empty;
        }

        var securitySchemeParameters = GetSecuritySchemeParameters(operation, parameters);
        var hasSecuritySchemeParameters = securitySchemeParameters.Any();
        return (hasSecuritySchemeParameters ? 
$$"""
/// todo: add parameters and make sure those parameters are not generated in any of the other input structures

""" :   
$$"""

/// todo: add inferred auth parameters
""");
    }
}
