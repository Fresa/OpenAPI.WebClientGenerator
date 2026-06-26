using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi;
using OpenAPI.WebClientGenerator.CodeGeneration.Authentication;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class AuthenticationGenerator(Dictionary<OpenApiSecuritySchemeReference, (ParameterGenerator Parameters, List<string> Scopes)>[] securityRequirementObjects, SecuritySchemaTranslations securitySchemaTranslations)
{
    internal SourceCode Generate(string @namespace, IReadOnlyList<string> nestingClassNames) =>
        new($"{string.Join(".", nestingClassNames)}.Authentication.g.cs",
$$"""
#nullable enable
namespace {{@namespace}};
{{NestedClassGenerator.Wrap(nestingClassNames, GenerateClass)}}
#nullable restore
""");

    private string GenerateClass() =>
$$"""
internal abstract partial class Authentication
{{{securityRequirementObjects.AggregateToString(securityRequirementObject =>
    securityRequirementObject.Count == 1
        ? GenerateSingleSchemeRequirement(securityRequirementObject)
        : GenerateMultiSchemeRequirement(securityRequirementObject))}}
    internal abstract void AddTo(RequestBuilder requestBuilder);
}
""";

    private string GenerateSingleSchemeRequirement(
        Dictionary<OpenApiSecuritySchemeReference, (ParameterGenerator Parameters, List<string> Scopes)> securityRequirementObject)
    {
        var className = GetAuthenticationClassName(securityRequirementObject);
        var schemeReference = securityRequirementObject.Single().Key;
        var schemeClassName = securitySchemaTranslations.GetSecuritySchemeName(schemeReference).ToPascalCase();
        var constructorArguments = schemeReference.GetSchemeConstructorArguments();
        return
$$"""
    internal sealed partial class {{className}} : Authentication
    {
        private readonly SecuritySchemes.{{schemeClassName}} _scheme;

        internal {{className}}({{constructorArguments.GetMethodParameterList()}}) =>
            _scheme = new SecuritySchemes.{{schemeClassName}}({{constructorArguments.GetMethodArgumentList()}});

        internal override void AddTo(RequestBuilder requestBuilder) => _scheme.AddTo(requestBuilder);
    }
""";
    }

    private string GenerateMultiSchemeRequirement(
        Dictionary<OpenApiSecuritySchemeReference, (ParameterGenerator Parameters, List<string> Scopes)> securityRequirementObject)
    {
        var className = GetAuthenticationClassName(securityRequirementObject);
        return
$$"""
    internal sealed partial class {{className}} : Authentication
    {{{securityRequirementObject.AggregateToString(securityRequirement =>
        {
            var schemeClassName = securitySchemaTranslations.GetSecuritySchemeName(securityRequirement.Key).ToPascalCase();
            return
$$"""
        internal required SecuritySchemes.{{schemeClassName}} {{schemeClassName}} { init; get; }
""";
        })}}

        internal override void AddTo(RequestBuilder requestBuilder)
        {{{securityRequirementObject.AggregateToString(securityRequirement =>
        {
            var schemeClassName = securitySchemaTranslations.GetSecuritySchemeName(securityRequirement.Key).ToPascalCase();
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
    private string GetAuthenticationClassName(
        Dictionary<OpenApiSecuritySchemeReference, (ParameterGenerator Parameters, List<string> Scopes)>
            securityRequirementObject)
    {
        var name = 
            string.Join("And", securityRequirementObject.Keys
                .Select(reference => securitySchemaTranslations.GetSecuritySchemeName(reference).ToPascalCase())
                .OrderBy(name => name));
        var i = 1;
        while (!_requirementGroupNames.Add(name))
        {
            i++;
            name += i;
        }

        return name;
    }
}