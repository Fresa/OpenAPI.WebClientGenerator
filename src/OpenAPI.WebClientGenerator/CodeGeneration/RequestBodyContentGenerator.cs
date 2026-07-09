using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using Microsoft.OpenApi;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class RequestBodyContentGenerator
{
    private readonly string _contentVariableName;
    internal string ClassName { get; }
    private MediaTypeHeaderValue ContentType { get; }
    private readonly TypeDeclaration _typeDeclaration;
    private readonly bool _isContentTypeRange;
    private readonly bool _isSequentialMediaType;
    
    public RequestBodyContentGenerator(KeyValuePair<string, IOpenApiMediaType> contentMediaType, TypeDeclaration typeDeclaration)
    { 
        ContentType = MediaTypeHeaderValue.Parse(contentMediaType.Key);
        _typeDeclaration = typeDeclaration;
        _isSequentialMediaType = contentMediaType.Value.ItemSchema != null;
        _isContentTypeRange = ContentType.MediaType.EndsWith("*");
        _contentVariableName = ContentType.MediaType switch
        {
            "*/*" => "any",
            not null when _isContentTypeRange =>
                $"any{ContentType.MediaType.TrimEnd('*').TrimEnd('/').ToLower().ToPascalCase()}",
            null => throw new InvalidOperationException("Content type is null"),
            _ => ContentType.MediaType.ToLower().ToCamelCase()
        };

        ClassName = _contentVariableName.ToPascalCase();
    }

    private string SchemaLocation => _typeDeclaration.RelativeSchemaLocation;
    public string GenerateContentClass() =>
        _isSequentialMediaType ? 
$$"""
/// <summary>
/// Request body for content {{ContentType}}
/// </summary>
public sealed class {{ClassName}} : Content, IDisposable
{
    private readonly {{ClassName}}Writer<{{_typeDeclaration.FullyQualifiedDotnetTypeName()}}> _content;
    private {{_typeDeclaration.FullyQualifiedDotnetTypeName()}}? _currentItem;
    private readonly Pipe _pipe = new();
    private WebClientConfiguration _configuration;

    private ValidationContext CreateValidationContext() =>
        ValidationContext.ValidContext.UsingStack().UsingResults();
        
    /// <summary>
    /// Construct request for content {{ContentType}}
    /// </summary>{{(_isContentTypeRange ? 
$"""

   /// <param name="contentType">Content type must match range {ContentType.MediaType}</param>
""" : "")}}
    public {{ClassName}}({{(_isContentTypeRange ? "string contentType, " : "")}}WebClientConfiguration? configuration = null)
    {{{(_isContentTypeRange ?
"""
        
        EnsureExpectedContentType(MediaTypeHeaderValue.Parse(contentType), ContentMediaType);
""" : "")}}
        _content = new(_pipe.Writer);
        MediaType = {{(_isContentTypeRange ? "contentType" : $"\"{ContentType.MediaType}\"")}};
        _configuration = configuration ?? new();
    }

    internal override string MediaType { get; }
    
    /// <summary>
    /// Write an item to the sequence
    /// </summary>
    /// <param name="item">Item to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An awaitable validation context for the request. The item is only written if passing validation.</returns>
    public async ValueTask<ImmutableList<ValidationResult>> WriteItemAsync({{_typeDeclaration.FullyQualifiedDotnetTypeName()}} item, CancellationToken cancellationToken = default)
    {
        _currentItem = item;
        var validationContext = _configuration.ValidateRequests ? 
            ValidateCurrentItem(CreateValidationContext(), _configuration.ValidationLevel) : 
            CreateValidationContext();
        if (validationContext.IsValid)
        {
            await _content.WriteItemAsync(item, cancellationToken).ConfigureAwait(false);
        }

        _currentItem = null;
        return validationContext.Results.WithLocation(_configuration.OpenApiSpecificationUri);
    }
    
    private static MediaTypeHeaderValue ContentMediaType { get; } = MediaTypeHeaderValue.Parse("{{ContentType}}");
    
    internal override HttpContent Get() =>
        new StreamContent(_pipe.Reader.AsStream())
        {
            Headers =
            {
                ContentType = ContentMediaType
            }
        };

    private const string ContentSchemaLocation = "{{SchemaLocation}}";
    /// <inheritdoc/>
    internal override ValidationContext Validate(ValidationContext context, ValidationLevel validationLevel)
    {
        return ValidateCurrentItem(context, validationLevel);
    }
    
    private ValidationContext ValidateCurrentItem(
        ValidationContext validationContext, 
        ValidationLevel validationLevel) =>
        _currentItem is null
            ? validationContext
            : _content.Validate(_currentItem.Value, ContentSchemaLocation, validationContext,
                validationLevel);
                
    /// <inheritdoc/>
    public void Dispose()
    {
        _content.Dispose();
        _pipe.Writer.Complete();
    }
}                              
""" :


$$"""
/// <summary>
/// Request for content {{ContentType}}
/// </summary>
public sealed class {{ClassName}} : Content
{
    private {{_typeDeclaration.FullyQualifiedDotnetTypeName()}} _content;
    
    /// <summary>
    /// Construct request for content {{ContentType}}
    /// </summary>
    /// <param name="{{_contentVariableName}}">Content</param>{{(_isContentTypeRange ? 
$"""

    /// <param name="contentType">Content type must match range {ContentType.MediaType}</param>
""" : "")}}
    public {{ClassName}}({{_typeDeclaration.FullyQualifiedDotnetTypeName()}} {{_contentVariableName}}{{(_isContentTypeRange ? ", string contentType" : "")}})
    {{{(_isContentTypeRange ? 
"""
        
        EnsureExpectedContentType(MediaTypeHeaderValue.Parse(contentType), ContentMediaType);
""" : "")}}
        _content = {{_contentVariableName}};
        MediaType = {{(_isContentTypeRange ? "contentType" : $"\"{ContentType}\"")}};
    }
    
    internal override string MediaType { get; }
    
    internal override HttpContent Get() =>
       new StringContent(
           _content.Serialize(),
           encoding: Encoding.UTF8,
           mediaType: MediaType
       );{{(_isContentTypeRange ?
"""    

    private static MediaTypeHeaderValue ContentMediaType { get; } = MediaTypeHeaderValue.Parse("{{ContentType}}");
""" : "")}}
    private const string ContentSchemaLocation = "{{SchemaLocation}}";
    /// <inheritdoc/>
    internal override ValidationContext Validate(ValidationContext validationContext, ValidationLevel validationLevel) =>
        _content.Validate(ContentSchemaLocation, true, validationContext, validationLevel);
}
""";
}