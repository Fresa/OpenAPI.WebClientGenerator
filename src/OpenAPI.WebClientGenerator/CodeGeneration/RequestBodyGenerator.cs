using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class RequestBodyGenerator
{
    private readonly IOpenApiRequestBody? _body;
    private readonly List<RequestBodyContentGenerator> _contentGenerators = [];

    public static readonly RequestBodyGenerator Empty = new(null, []);

    internal bool HasBody => _body != null;
    internal bool IsRequired => _body?.Required ?? false;
    
    public RequestBodyGenerator(
        IOpenApiRequestBody? body,
        List<RequestBodyContentGenerator> contentGenerators)
    {
        _body = body;
        _contentGenerators = contentGenerators;
    }
    
    internal SourceCode Generate(string @namespace, IReadOnlyList<string> nestingClassNames) =>
        new($"{string.Join(".", nestingClassNames)}.Content.g.cs",
$$"""
#nullable enable
using Corvus.Json;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using System.Text;

namespace {{@namespace}};
{{NestedClassGenerator.Wrap(nestingClassNames, GenerateClass)}}
#nullable restore
""");

    public string GenerateClass()
    {
        if (!_contentGenerators.Any())
        {
            return string.Empty;
        }
        
        return 
$$$"""
internal abstract class Content
{
    internal abstract string? MediaType { get; }

    /// <summary>
    /// Ensures that the specified content type matches the specification
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the specified content type does not match the specification</exception>
    /// </summary>
    /// <param name="contentType">Content type</param>
    /// <param name="expectedContentType">Expected content type</param>
    protected void EnsureExpectedContentType(MediaTypeHeaderValue contentType, MediaTypeHeaderValue expectedContentType)
    {
        if (!contentType.IsSubsetOf(expectedContentType))
        {
            throw new ArgumentOutOfRangeException($"Expected content type {contentType.MediaType} to be a subset of {expectedContentType.MediaType}");
        }
    }

    internal abstract HttpContent Get();

    internal abstract ValidationContext Validate(ValidationContext validationContext, ValidationLevel validationLevel);
{{{_contentGenerators.AggregateToString(generator => 
    generator.GenerateContentClass()).Indent(4)}}}
}
""";
    }
}