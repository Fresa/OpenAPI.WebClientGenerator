using System;
using System.Collections.Generic;
using Microsoft.OpenApi;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class AuthenticationGenerator(Dictionary<OpenApiSecuritySchemeReference, ParameterGenerator?> securitySchemes, SecuritySchemaTranslations securitySchemaTranslations)
{
    internal string GenerateClass() =>
$$"""
internal sealed partial class Authentication
{
    private readonly System.Action<RequestBuilder> _apply;

    private Authentication(System.Action<RequestBuilder> apply) => _apply = apply;
    {{securitySchemes.AggregateToString(scheme =>
    {
      var schemeReference = scheme.Key;
      var schemeClassName = securitySchemaTranslations.GetSecuritySchemeName(schemeReference).ToPascalCase();
      var comment = schemeReference.Description;
      if (schemeReference.Type == SecuritySchemeType.MutualTLS)
      {
          comment += "\n" + "Mutual TLS needs to be configured on the HttpClient handler's client certificate";
      }

      return
$$"""
{{comment.AsComment("summary", "para").Indent(4)}}
{{scheme.Key.Type switch
{
    SecuritySchemeType.ApiKey =>
$"""
    internal static Authentication {schemeClassName}(string apiKey) =>
        new(requestBuilder => requestBuilder.Add{schemeReference.In?.GetDisplayName().ToPascalCase()}(SecuritySchemes.{schemeClassName}.Name, apiKey));
 """,
    SecuritySchemeType.Http when string.Equals(schemeReference.Scheme, "basic", StringComparison.OrdinalIgnoreCase) =>
$$"""
    internal static Authentication {{schemeClassName}}(string username, string password) =>
        new(requestBuilder => requestBuilder.AddHeader("Authorization", $"Basic {System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"))}"));
  """,
    _ when schemeReference.Type is SecuritySchemeType.OAuth2 or SecuritySchemeType.OpenIdConnect
           || string.Equals(schemeReference.Scheme, "bearer", StringComparison.OrdinalIgnoreCase) =>
$$"""
    internal static Authentication {{schemeClassName}}(string token) =>
        new(requestBuilder => requestBuilder.AddHeader("Authorization", $"Bearer {token}"));
""",
    SecuritySchemeType.MutualTLS =>
$$"""
    internal static Authentication {{schemeClassName}}() =>
        new(_ => { });
""",
    _ => 
$$"""
    internal static partial Authentication {{schemeClassName}}(System.Action<RequestBuilder> action);
"""
}}}
""";
})}}
    internal void AddTo(RequestBuilder requestBuilder) => _apply(requestBuilder);
}
""";
}