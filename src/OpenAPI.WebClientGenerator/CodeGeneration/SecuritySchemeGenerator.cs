using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.OpenApi;
using OpenAPI.WebClientGenerator.CodeGeneration.Authentication;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class SecuritySchemeGenerator
{
    private readonly IDictionary<string, IOpenApiSecurityScheme> _securitySchemes;
    private readonly Dictionary<OpenApiSecuritySchemeReference, List<string>>[] _topLevelSecuritySchemeGroups;
    private readonly SecuritySchemaTranslations _securitySchemaTranslations;

    public SecuritySchemeGenerator(OpenApiDocument openApiDocument)
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
{{{_securitySchemes.AggregateToString(pair => GenerateScheme(pair.Key, pair.Value))}}
}
""");
    }

    private static string GenerateScheme(string schemaName, IOpenApiSecurityScheme scheme) =>
        scheme.Type switch
        {
            null => string.Empty,
            SecuritySchemeType.ApiKey => GenerateApiKey(schemaName, scheme),
            SecuritySchemeType.Http when string.Equals(scheme.Scheme, "basic", StringComparison.OrdinalIgnoreCase) =>
                GenerateBasic(schemaName, scheme),
            _ when scheme.Type is SecuritySchemeType.OAuth2 or SecuritySchemeType.OpenIdConnect
                   || string.Equals(scheme.Scheme, "bearer", StringComparison.OrdinalIgnoreCase) =>
                GenerateBearer(schemaName, scheme),
            SecuritySchemeType.MutualTLS => GenerateMutualTls(schemaName, scheme),
            _ => GenerateCustom(schemaName, scheme),
        };

    private static string GenerateApiKey(string schemaName, IOpenApiSecurityScheme scheme)
    {
        var className = schemaName.ToPascalCase();
        return $$"""

    internal const string {{className}}Key = "{{schemaName}}";
{{GenerateSchemeComment(scheme).Indent(4)}}
    internal sealed partial class {{className}}<T>
        where T : struct, Corvus.Json.IJsonValue<T>
    {
        private readonly System.Action<RequestBuilder> _apply;
        internal {{className}}({{SecurityScheme.ApiKey.Key.GetMethodParameter()}}, bool isRequired, string schemaLocation, string parameterSpecificationAsJson) =>
            _apply = requestBuilder => requestBuilder.Add{{scheme.In?.GetDisplayName().ToPascalCase()}}<T>(Name, {{SecurityScheme.ApiKey.Key.Name}}, isRequired, schemaLocation, parameterSpecificationAsJson);

        internal void AddTo(RequestBuilder requestBuilder) => _apply(requestBuilder);
{{GenerateSchemeConstants(scheme).Indent(8)}}
    }
""";
    }

    private static string GenerateBasic(string schemaName, IOpenApiSecurityScheme scheme)
    {
        var className = schemaName.ToPascalCase();
        return $$"""

    internal const string {{className}}Key = "{{schemaName}}";
{{GenerateSchemeComment(scheme).Indent(4)}}
    internal sealed partial class {{className}}
    {
        private readonly System.Action<RequestBuilder> _apply;
        internal {{className}}({{SecurityScheme.Http.Username.GetMethodParameter()}}, {{SecurityScheme.Http.Password.GetMethodParameter()}}) =>
            _apply = requestBuilder => requestBuilder.AddHeader("Authorization", $"Basic {System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{{{SecurityScheme.Http.Username.Name}}}:{{{SecurityScheme.Http.Password.Name}}}"))}");

        internal void AddTo(RequestBuilder requestBuilder) => _apply(requestBuilder);
{{GenerateSchemeConstants(scheme).Indent(8)}}
    }
""";
    }

    private static string GenerateBearer(string schemaName, IOpenApiSecurityScheme scheme)
    {
        var className = schemaName.ToPascalCase();
        return $$"""

    internal const string {{className}}Key = "{{schemaName}}";
{{GenerateSchemeComment(scheme).Indent(4)}}
    internal sealed partial class {{className}}
    {
        private readonly System.Action<RequestBuilder> _apply;
        internal {{className}}({{SecurityScheme.Bearer.Token.GetMethodParameter()}}) =>
            _apply = requestBuilder => requestBuilder.AddHeader("Authorization", $"Bearer {{{SecurityScheme.Bearer.Token.Name}}}");

        internal void AddTo(RequestBuilder requestBuilder) => _apply(requestBuilder);
{{GenerateSchemeConstants(scheme).Indent(8)}}
    }
""";
    }

    private static string GenerateCustom(string schemaName, IOpenApiSecurityScheme scheme)
    {
        var className = schemaName.ToPascalCase();
        return $$"""

    internal const string {{className}}Key = "{{schemaName}}";
{{GenerateSchemeComment(scheme).Indent(4)}}
    internal sealed partial class {{className}}
    {
        private readonly System.Action<RequestBuilder> _apply;
        internal {{className}}({{SecurityScheme.Custom.Apply.GetMethodParameter()}}) =>
            _apply = {{SecurityScheme.Custom.Apply.Name}};

        internal void AddTo(RequestBuilder requestBuilder) => _apply(requestBuilder);
{{GenerateSchemeConstants(scheme).Indent(8)}}
    }
""";
    }

    private static string GenerateMutualTls(string schemaName, IOpenApiSecurityScheme scheme)
    {
        var className = schemaName.ToPascalCase();
        return $$"""

    internal const string {{className}}Key = "{{schemaName}}";
{{GenerateSchemeComment(scheme).Indent(4)}}
    internal sealed partial class {{className}}
    {
        private readonly System.Action<RequestBuilder> _apply;
        internal {{className}}() =>
            _apply = _ => { };

        internal void AddTo(RequestBuilder requestBuilder) => _apply(requestBuilder);
{{GenerateSchemeConstants(scheme).Indent(8)}}
    }
""";
    }

    private static string GenerateSchemeConstants(IOpenApiSecurityScheme scheme) =>
        new []
        {
            GenerateConst(nameof(scheme.Type), scheme.Type?.GetDisplayName()),
            GenerateConst(nameof(scheme.Scheme), scheme.Scheme),
            GenerateConst(nameof(scheme.BearerFormat), scheme.BearerFormat),
            GenerateConst(nameof(scheme.OpenIdConnectUrl), scheme.OpenIdConnectUrl?.ToString()),
            GenerateGetParameterMethods(scheme),
            $"internal const bool {nameof(scheme.Deprecated)} = {scheme.Deprecated.ToString().ToLowerInvariant()};",
            GenerateFlowsObject(nameof(scheme.Flows), scheme.Flows)
        }.RemoveEmptyLines().AggregateToString();

    private static string GenerateSchemeComment(IOpenApiSecurityScheme scheme) =>
        scheme.Description.AsComment("summary", "para");

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
        [NotNullWhen(true)] out SecurityRequirementGenerator? authenticationGenerator)
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
                    SecurityParameter: parameters.FirstOrDefault(generator => generator.IsSecuritySchemeParameter(scheme.Key)),
                    Scopes: scheme.Value)))
            .ToArray();
        authenticationGenerator = new SecurityRequirementGenerator(securitySchemeGroups, _securitySchemaTranslations);
        return true;
    }
}