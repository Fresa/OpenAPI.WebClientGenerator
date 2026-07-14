using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using Microsoft.OpenApi;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class ResponseBodyContentGenerator
{
    internal string ClassName { get; }
    internal MediaTypeHeaderValue ContentType { get; }
    private readonly TypeDeclaration _typeDeclaration;
    private readonly bool _isSequentialMediaType;
    
    public ResponseBodyContentGenerator(KeyValuePair<string, IOpenApiMediaType> contentMediaType, TypeDeclaration typeDeclaration)
    {
        ContentType = MediaTypeHeaderValue.Parse(contentMediaType.Key);
        _typeDeclaration = typeDeclaration;
        _isSequentialMediaType = contentMediaType.Value.ItemSchema != null;
        var isContentTypeRange = ContentType.MediaType.EndsWith("*");
        var contentVariableName = ContentType.MediaType switch
        {
            "*/*" => "any",
            not null when isContentTypeRange =>
                $"any{ContentType.MediaType.TrimEnd('*').TrimEnd('/').ToLower().ToPascalCase()}",
            null => throw new InvalidOperationException("Content type is null"),
            _ => ContentType.MediaType.ToLower().ToCamelCase()
        };

        ClassName = contentVariableName.ToPascalCase();
    }

    private string SchemaLocation => _typeDeclaration.RelativeSchemaLocation;

    public SourceCode GenerateContent(
        string @namespace,
        IReadOnlyList<string> nestingClassNames,
        string responseClassName)
    {
        var rootBaseClassName = nestingClassNames[^1];
        return new SourceCode(
            $"{string.Join(".", nestingClassNames)}.{responseClassName}.{ClassName}.g.cs",
$$"""
#nullable enable
using Corvus.Json;
using System.Net.Http.Headers;
using System.Text.Json;

namespace {{@namespace}};
{{NestedClassGenerator.Wrap(nestingClassNames.Append(responseClassName).ToArray(), () =>
    GenerateResponseClass(responseClassName, rootBaseClassName))}}
#nullable restore
""");
    }

    private string GenerateResponseClass(string responseClassName, string rootBaseClassName) =>
        _isSequentialMediaType ? 
$$"""
/// <summary>
/// Response for content {{ContentType}}
/// </summary>
public sealed class {{ClassName}} : {{responseClassName}}, IAcceptContent
{
    public {{ClassName}}Enumerable<{{_typeDeclaration.FullyQualifiedDotnetTypeName()}}> Content { get; }

    private {{ClassName}}(Stream stream, HttpResponseMessage response, WebClientConfiguration configuration) :
        base(response)
    {
        Content = new(stream, configuration);
    }
    
    /// <summary>
    /// Construct response for content {{ContentType}}
    /// </summary>
    /// <param name="response">Response message</param>
    /// <param name="configuration">Web client configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    internal new static async Task<{{rootBaseClassName}}> BindAsync(HttpResponseMessage response, WebClientConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return new {{ClassName}}(stream, response, configuration);
    }
    
    public static MediaTypeWithQualityHeaderValue MediaType { get; } = MediaTypeWithQualityHeaderValue.Parse("{{ContentType}}");
    
    private const string ContentSchemaLocation = "{{SchemaLocation}}";
    /// <inheritdoc/>
    internal override ValidationContext Validate(ValidationLevel validationLevel)
    {
        var validationContext = base.Validate(validationLevel);
        return Content.Validate(ContentSchemaLocation, true, validationContext, validationLevel);
    }
}                              
""" :


$$"""
/// <summary>
/// Response for content {{ContentType}}
/// </summary>
public sealed class {{ClassName}} : {{responseClassName}}, IAcceptContent
{
    public {{_typeDeclaration.FullyQualifiedDotnetTypeName()}} Content { get; }
    
    private {{ClassName}}(JsonElement content, HttpResponseMessage response) :
        base(response)
    {
        Content = {{_typeDeclaration.FullyQualifiedDotnetTypeName()}}.FromJson(content);
    }
    
    /// <summary>
    /// Construct response for content {{ContentType}}
    /// </summary>
    /// <param name="response">Response message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    internal new static async Task<{{rootBaseClassName}}> BindAsync(HttpResponseMessage response, WebClientConfiguration _, CancellationToken cancellationToken = default)
    {
        var content = await {{responseClassName}}.ReadJsonAsync(response, cancellationToken)
            .ConfigureAwait(false);
        return new {{ClassName}}(content, response);
    }
    
    public static MediaTypeWithQualityHeaderValue MediaType { get; } = MediaTypeWithQualityHeaderValue.Parse("{{ContentType}}");
    
    private const string ContentSchemaLocation = "{{SchemaLocation}}";
    /// <inheritdoc/>
    internal override ValidationContext Validate(ValidationLevel validationLevel)
    {
        var validationContext = base.Validate(validationLevel);
        return Content.Validate(ContentSchemaLocation, true, validationContext, validationLevel);
    }
}
""";
}
