using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class AuthenticationGenerator(Dictionary<OpenApiSecuritySchemeReference, (ParameterGenerator Parameters, List<string> Scopes)>[] securityRequirementObjects, SecuritySchemaTranslations securitySchemaTranslations)
{
    internal string GenerateClass() =>
$$"""
internal abstract partial class Authentication
{{{securityRequirementObjects.AggregateToString(securityRequirementObject  =>
$$"""
    internal sealed partial class {{GetAuthenticationClassName(securityRequirementObject)}} : Authentication
    {{{securityRequirementObject.AggregateToString(securityRequirement => 
        {
            var schemeReference = securityRequirement.Key;
            var parameters = securityRequirement.Value.Parameters;
            var scopes = securityRequirement.Value.Scopes;
            var schemeClassName = securitySchemaTranslations.GetSecuritySchemeName(schemeReference).ToPascalCase();
            var comment = schemeReference.Description;
            if (schemeReference.Type == SecuritySchemeType.MutualTLS)
            {
              comment += "\n" + "Mutual TLS needs to be configured on the HttpClient handler's client certificate";
            }
            return
$$"""
{{comment.AsComment("summary", "para").Indent(8)}}
        internal required SecuritySchemes.{{schemeClassName}} {{schemeClassName}} { init; get; }
""";
        })}}
        
        internal void AddTo(RequestBuilder requestBuilder)
        {{{securityRequirementObject.AggregateToString(securityRequirement => 
        {
            var schemeReference = securityRequirement.Key;
            var schemeClassName = securitySchemaTranslations.GetSecuritySchemeName(schemeReference).ToPascalCase();
            return
$$"""
            {{schemeClassName}}.AddTo(requestBuilder);
""";
        })}}
        }
    }
""")}}
}
""";

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