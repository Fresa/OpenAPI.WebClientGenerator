using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi;
using OpenAPI.WebClientGenerator.CodeGeneration.Authentication;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class SecurityRequirementGenerator(
    Dictionary<OpenApiSecuritySchemeReference, (ParameterGenerator Parameters, List<string> Scopes)>[]
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
internal abstract partial class SecurityRequirement
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
internal sealed partial class Anonymous : SecurityRequirement
{
    internal override void AddTo(RequestBuilder requestBuilder) { }
}
""";

    private string GenerateSingleSchemeRequirement(
        Dictionary<OpenApiSecuritySchemeReference, (ParameterGenerator Parameters, List<string> Scopes)>
            securityRequirementObject)
    {
        var securityRequirement = securityRequirementObject.Single();
        var className = GetSecurityRequirementsClassName(securityRequirementObject);
        var schemeReference = securityRequirement.Key;
        var scopes = securityRequirement.Value.Scopes; 
        var schemeClassName = GetSecurityRequirementClassName(schemeReference);
        var constructorArguments = schemeReference.GetSchemeConstructorArguments();
        return
$$"""
internal sealed partial class {{className}} : SecurityRequirement
{{{(scopes.Any() ?
$$"""

    internal static class Scopes
    {{{scopes.AggregateToString(scope => 
$"""
        internal const string {scope.ToPascalCase()} = "{scope}";
""")}}
    }

""" : "")}}
    private readonly SecuritySchemes.{{schemeClassName}} _scheme;

    internal {{className}}({{constructorArguments.GetMethodParameterList()}}) =>
        _scheme = new SecuritySchemes.{{schemeClassName}}({{constructorArguments.GetMethodArgumentList()}});

    internal override void AddTo(RequestBuilder requestBuilder) => _scheme.AddTo(requestBuilder);
}
""";
    }

    private string GenerateMultiSchemeRequirement(
        Dictionary<OpenApiSecuritySchemeReference, (ParameterGenerator Parameters, List<string> Scopes)>
            securityRequirementObject)
    {
        var className = GetSecurityRequirementsClassName(securityRequirementObject);
        var scopesPerSecurityRequirement =
            securityRequirementObject
                .Where(pair => pair.Value.Scopes.Any())
                .ToDictionary(pair => GetSecurityRequirementClassName(pair.Key),
                    pair => pair.Value.Scopes);
        return
$$"""
internal sealed partial class {{className}} : SecurityRequirement
{{{(scopesPerSecurityRequirement.Any() ?
$$"""

    internal static class Scopes
    {{{scopesPerSecurityRequirement.AggregateToString(securityRequirement =>
$$"""
        internal static class {{securityRequirement.Key}}
        {{{securityRequirement.Value.AggregateToString(scope =>
$"""
            internal const string {scope.ToPascalCase()} = "{scope}";
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
    internal required SecuritySchemes.{{schemeClassName}} {{schemeClassName}} { init; get; }
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
        Dictionary<OpenApiSecuritySchemeReference, (ParameterGenerator Parameters, List<string> Scopes)>
            securityRequirementObject)
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