using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi;
using OpenAPI.WebClientGenerator.CodeGeneration.Authentication;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class SecurityRequirementGenerator(
    Dictionary<OpenApiSecuritySchemeReference, (ParameterGenerator SecurityParameter, List<string> Scopes)>[]
        securityRequirementObjects,
    SecuritySchemaTranslations securitySchemaTranslations)
{
    internal SourceCode Generate(string @namespace, IReadOnlyList<string> nestingClassNames) =>
        new($"{string.Join(".", nestingClassNames)}.SecurityRequirement.g.cs",
$$"""
#nullable enable
namespace {{@namespace}};
{{NestedClassGenerator.Wrap(nestingClassNames, GenerateClass)}}
#nullable restore
""");

    private string GenerateClass() =>
$$"""
public abstract partial class SecurityRequirement
{{{securityRequirementObjects.AggregateToString(securityRequirementObject =>
$$"""
{{(securityRequirementObject.Count switch
{
    0 => GenerateAnonymous(),
    1 => GenerateSingleSchemeRequirement(securityRequirementObject),
    _ => GenerateMultiSchemeRequirement(securityRequirementObject)
}).Indent(4)}}

""")}}

    internal abstract void AddTo(RequestBuilder requestBuilder);
}
""";

    private static string GenerateAnonymous() =>
"""
public sealed partial class Anonymous : SecurityRequirement
{
    internal override void AddTo(RequestBuilder requestBuilder) { }
}
""";

    private string GenerateSingleSchemeRequirement(
        Dictionary<OpenApiSecuritySchemeReference, (ParameterGenerator SecurityParameter, List<string> Scopes)> securityRequirementObject)
    {
        var securityRequirement = securityRequirementObject.Single();
        var className = GetSecurityRequirementsClassName(securityRequirementObject);
        var schemeReference = securityRequirement.Key;
        var scopes = securityRequirement.Value.Scopes;
        var parameter = securityRequirement.Value.SecurityParameter;
        var schemeClassName = GetSecurityRequirementClassName(schemeReference);
        return schemeReference.Type switch
        {
            SecuritySchemeType.ApiKey => GenerateApiKeyRequirement(className, schemeClassName, scopes, schemeReference, parameter),
            SecuritySchemeType.Http when string.Equals(schemeReference.Scheme, "basic", StringComparison.OrdinalIgnoreCase) =>
                GenerateBasicRequirement(className, schemeClassName, scopes),
            _ when schemeReference.Type is SecuritySchemeType.OAuth2 or SecuritySchemeType.OpenIdConnect
                   || string.Equals(schemeReference.Scheme, "bearer", StringComparison.OrdinalIgnoreCase) =>
                GenerateBearerRequirement(className, schemeClassName, scopes),
            SecuritySchemeType.MutualTLS => GenerateMutualTlsRequirement(className, schemeClassName, scopes),
            _ => GenerateCustomRequirement(className, schemeClassName, scopes),
        };
    }

    private static string GenerateApiKeyRequirement(
        string className,
        string schemeClassName,
        List<string> scopes,
        OpenApiSecuritySchemeReference schemeReference,
        ParameterGenerator? parameter)
    {
        if (parameter is not null)
        {
            return
$$"""
public sealed partial class {{className}} : SecurityRequirement
{{{GenerateScopes(scopes)}}
    /// <summary>
    /// The key is inferred from the "{{parameter.ParameterName}}" request {{parameter.Location.GetDisplayName()}} parameter.
    /// </summary>
    public {{className}}()
    {
    }

    internal override void AddTo(RequestBuilder requestBuilder) { }
}
""";
        }

        var specification =
$$"""
{
    "name": "{{schemeReference.Name}}",
    "in": "{{schemeReference.In?.GetDisplayName()}}"
}
""";
        return
$$""""
public sealed partial class {{className}} : SecurityRequirement
{{{GenerateScopes(scopes)}}
    private readonly SecuritySchemes.{{schemeClassName}} _scheme;

    public {{className}}({{SecurityScheme.ApiKey.Key.GetMethodParameter()}}) =>
        _scheme = new SecuritySchemes.{{schemeClassName}}({{SecurityScheme.ApiKey.Key.Name}}, false, string.Empty,
            """
            {{specification.Indent(12).Trim()}}
            """);

    internal override void AddTo(RequestBuilder requestBuilder) => _scheme.AddTo(requestBuilder);
}
"""";
    }

    private static string GenerateBasicRequirement(
        string className,
        string schemeClassName,
        List<string> scopes) =>
$$"""
public sealed partial class {{className}} : SecurityRequirement
{{{GenerateScopes(scopes)}}
    private readonly SecuritySchemes.{{schemeClassName}} _scheme;

    public {{className}}({{SecurityScheme.Http.Username.GetMethodParameter()}}, {{SecurityScheme.Http.Password.GetMethodParameter()}}) =>
        _scheme = new SecuritySchemes.{{schemeClassName}}({{SecurityScheme.Http.Username.Name}}, {{SecurityScheme.Http.Password.Name}});

    internal override void AddTo(RequestBuilder requestBuilder) => _scheme.AddTo(requestBuilder);
}
""";

    private static string GenerateBearerRequirement(
        string className, 
        string schemeClassName, 
        List<string> scopes) =>
$$"""
public sealed partial class {{className}} : SecurityRequirement
{{{GenerateScopes(scopes)}}
    private readonly SecuritySchemes.{{schemeClassName}} _scheme;

    public {{className}}({{SecurityScheme.Bearer.Token.GetMethodParameter()}}) =>
        _scheme = new SecuritySchemes.{{schemeClassName}}({{SecurityScheme.Bearer.Token.Name}});

    internal override void AddTo(RequestBuilder requestBuilder) => _scheme.AddTo(requestBuilder);
}
""";

    private static string GenerateMutualTlsRequirement(string className, string schemeClassName, List<string> scopes) =>
$$"""
public sealed partial class {{className}} : SecurityRequirement
{{{GenerateScopes(scopes)}}
    private readonly SecuritySchemes.{{schemeClassName}} _scheme;

    public {{className}}() =>
        _scheme = new SecuritySchemes.{{schemeClassName}}();

    internal override void AddTo(RequestBuilder requestBuilder) => _scheme.AddTo(requestBuilder);
}
""";

    private static string GenerateCustomRequirement(string className, string schemeClassName, List<string> scopes) =>
$$"""
public sealed partial class {{className}} : SecurityRequirement
{{{GenerateScopes(scopes)}}
    private readonly SecuritySchemes.{{schemeClassName}} _scheme;

    public {{className}}({{SecurityScheme.Custom.Apply.GetMethodParameter()}}) =>
        _scheme = new SecuritySchemes.{{schemeClassName}}({{SecurityScheme.Custom.Apply.Name}});

    internal override void AddTo(RequestBuilder requestBuilder) => _scheme.AddTo(requestBuilder);
}
""";

    private static string GenerateScopes(List<string> scopes) =>
        scopes.Any() ? 
$$"""

    public static class Scopes
    {{{scopes.AggregateToString(scope =>
$"""
        public const string {scope.ToPascalCase()} = "{scope}";
""")}}
    }

""" : "";

    private string GenerateMultiSchemeRequirement(
        Dictionary<OpenApiSecuritySchemeReference, (ParameterGenerator SecurityParameter, List<string> Scopes)> securityRequirementObject)
    {
        var className = GetSecurityRequirementsClassName(securityRequirementObject);
        var scopesPerSecurityRequirement =
            securityRequirementObject
                .Where(pair => pair.Value.Scopes.Any())
                .ToDictionary(pair => GetSecurityRequirementClassName(pair.Key),
                    pair => pair.Value.Scopes);
        return
$$"""
public sealed partial class {{className}} : SecurityRequirement
{{{(scopesPerSecurityRequirement.Any() ?
$$"""

    public static class Scopes
    {{{scopesPerSecurityRequirement.AggregateToString(securityRequirement =>
$$"""
        public static class {{securityRequirement.Key}}
        {{{securityRequirement.Value.AggregateToString(scope =>
$"""
            public const string {scope.ToPascalCase()} = "{scope}";
""")}}
        }

"""
    )}}
    }

""" : "")}}{{securityRequirementObject.AggregateToString(securityRequirement =>
{
  var schemeClassName = GetSecurityRequirementClassName(securityRequirement.Key);
  return
$$"""
    public required SecuritySchemes.{{schemeClassName}} {{schemeClassName}} { init; get; }
""";
})}}

    internal override void AddTo(RequestBuilder requestBuilder)
    {{{securityRequirementObject.AggregateToString(securityRequirement =>
    {
        var schemeClassName = GetSecurityRequirementClassName(securityRequirement.Key);
        return
$$"""
        {{schemeClassName}}.AddTo(requestBuilder);
""";
    })}}
    }
}
""";
    }

    private readonly HashSet<string> _requirementGroupNames = [];
    private string GetSecurityRequirementsClassName(
        Dictionary<OpenApiSecuritySchemeReference, (ParameterGenerator SecurityParameter, List<string> Scopes)> securityRequirementObject)
    {
        var name = 
            string.Join("And", securityRequirementObject.Keys
                .Select(GetSecurityRequirementClassName)
                .OrderBy(name => name));
        var i = 1;
        while (!_requirementGroupNames.Add(name))
        {
            i++;
            name += i;
        }

        return name;
    }

    private string GetSecurityRequirementClassName(OpenApiSecuritySchemeReference openApiSecuritySchemeReference) => securitySchemaTranslations.GetSecuritySchemeName(openApiSecuritySchemeReference).ToPascalCase();
}