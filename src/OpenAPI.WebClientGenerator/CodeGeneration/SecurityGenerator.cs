using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.OpenApi;
using OpenAPI.WebClientGenerator.CodeGeneration.Authentication;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class SecurityGenerator
{
    private readonly IDictionary<string, IOpenApiSecurityScheme> _securitySchemes;
    private readonly Dictionary<OpenApiSecuritySchemeReference, List<string>>[] _topLevelSecuritySchemeGroups;
    private readonly SecuritySchemaTranslations _securitySchemaTranslations;

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
        var constructorParameters = scheme.GetSchemeConstructorArguments().GetMethodParameterList();
        return scheme.Type == null ? string.Empty :
$$"""

    internal const string {{className}}Key = "{{pair.Key}}";
{{scheme.Description.AsComment("summary", "para").Indent(4)}}
    internal sealed partial class {{className}}
    {
        private readonly System.Action<RequestBuilder> _apply;
{{scheme.Type switch
{
    SecuritySchemeType.ApiKey =>
$"""
        internal {className}({constructorParameters}) =>
            _apply = requestBuilder => requestBuilder.Add{scheme.In?.GetDisplayName().ToPascalCase()}(Name, {SecurityScheme.ApiKey.Key.Name});
""",
    SecuritySchemeType.Http when string.Equals(scheme.Scheme, "basic", StringComparison.OrdinalIgnoreCase) =>
$$"""
        internal {{className}}({{constructorParameters}}) =>
            _apply = requestBuilder => requestBuilder.AddHeader("Authorization", $"Basic {System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{{{SecurityScheme.Http.Username.Name}}}:{{{SecurityScheme.Http.Password.Name}}}"))}");
""",
    _ when scheme.Type is SecuritySchemeType.OAuth2 or SecuritySchemeType.OpenIdConnect
           || string.Equals(scheme.Scheme, "bearer", StringComparison.OrdinalIgnoreCase) =>
$$"""
        internal {{className}}({{constructorParameters}}) =>
            _apply = requestBuilder => requestBuilder.AddHeader("Authorization", $"Bearer {{{SecurityScheme.Bearer.Token.Name}}}");
""",
    SecuritySchemeType.MutualTLS =>
$$"""
        internal {{className}}({{constructorParameters}}) =>
            _apply = _ => { };
""",
    _ =>
$$"""
        internal {{className}}({{constructorParameters}}) =>
            _apply = {{SecurityScheme.Custom.Apply}};
"""
}}}

        internal void AddTo(RequestBuilder requestBuilder) => _apply(requestBuilder);
    
    {{new []
    {
        GenerateConst(nameof(scheme.Type), scheme.Type?.GetDisplayName()),
        GenerateConst(nameof(scheme.Scheme), scheme.Scheme),
        GenerateConst(nameof(scheme.BearerFormat), scheme.BearerFormat),
        GenerateConst(nameof(scheme.OpenIdConnectUrl), scheme.OpenIdConnectUrl?.ToString()),
        GenerateGetParameterMethods(scheme),
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

    private static string GenerateGetParameterMethods(IOpenApiSecurityScheme scheme)
    {
        if (scheme.Name == null || scheme.In == null)
        {
            return string.Empty;
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

        var securitySchemeGroups = securityRequirementGroups
            .Select(group => group.ToDictionary(
                scheme => scheme.Key,
                scheme => (
                    Parameters: parameters.FirstOrDefault(generator => generator.IsSecuritySchemeParameter(scheme.Key)),
                    Scopes: scheme.Value)))
            .ToArray();
        authenticationGenerator = new AuthenticationGenerator(securitySchemeGroups, _securitySchemaTranslations);
        return true;
    }
}