using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi;
using Microsoft.OpenApi.MicrosoftExtensions;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class AuthenticationGenerator(Dictionary<OpenApiSecuritySchemeReference, (ParameterGenerator Parameters, List<string> Scopes)>[] securityRequirementObjects, SecuritySchemaTranslations securitySchemaTranslations)
{
    internal string GenerateClass() =>
$$"""
internal abstract partial class Authentication
{{{securityRequirementObjects.WithIndex().AggregateToString(securityRequirementObject  =>
$$"""
    internal sealed partial class Requirement{{securityRequirementObject.I}} : Authentication
    {{{securityRequirementObject.Item.AggregateToString(securityRequirement => 
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
        {{{securityRequirementObject.Item.AggregateToString(securityRequirement => 
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
}