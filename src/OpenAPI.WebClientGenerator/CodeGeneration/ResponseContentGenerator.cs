using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.OpenApi;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class ResponseContentGenerator
{
    private readonly List<ResponseBodyContentGenerator> _contentGenerators = [];
    private readonly List<ResponseHeaderGenerator> _headerGenerators;
    internal string ClassName { get; }
    internal bool HasBodies { get; }
    private readonly string _responseStatusCodePattern;
    private readonly IOpenApiResponse _response;
    private readonly bool _hasExplicitStatusCode;
    private readonly bool _hasDefaultStatusCode;
    private readonly bool _anyHeaders;

    private ResponseContentGenerator(
        KeyValuePair<string, IOpenApiResponse> response,
        List<ResponseHeaderGenerator> headerGenerators)
    {
        var responseStatusCodePattern = response.Key.ToPascalCase();
        var classNamePrefix = Enum.TryParse<HttpStatusCode>(responseStatusCodePattern, out var statusCode)
            ? statusCode.ToString()
            : responseStatusCodePattern.First() switch
            {
                '1' => "Informational",
                '2' => "Successful",
                '3' => "Redirection",
                '4' => "ClientError",
                '5' => "ServerError",
                var chr when char.IsDigit(chr) => "X",
                _ => string.Empty
            };
        var responseClassName = $"{classNamePrefix}{responseStatusCodePattern}";

        _responseStatusCodePattern = responseStatusCodePattern;
        ClassName = responseClassName;
        _response = response.Value;
        _hasExplicitStatusCode = int.TryParse(_responseStatusCodePattern, out _);
        _hasDefaultStatusCode = response.Key == "default";
        Precedence = _hasExplicitStatusCode ? 0 : _hasDefaultStatusCode ? 10 : 5;
        _headerGenerators = headerGenerators;
        _anyHeaders = headerGenerators.Any();
    }

    internal int Precedence { get; }

    public ResponseContentGenerator(
        KeyValuePair<string, IOpenApiResponse> response,
        List<ResponseBodyContentGenerator> contentGenerators,
        List<ResponseHeaderGenerator> headerGenerators
        ) : this(response, headerGenerators)
    {
        _contentGenerators = contentGenerators;
        HasBodies = contentGenerators.Any();
    }

    public IEnumerable<SourceCode> Generate(
        string @namespace,
        IReadOnlyList<string> nestingClassNames,
        string baseClassName)
    {
        yield return GenerateBaseClass(@namespace, nestingClassNames, baseClassName);
        if (_contentGenerators.Any())
        {
            yield return GenerateUnknownContent(@namespace, nestingClassNames, baseClassName);
            foreach (var content in _contentGenerators)
            {
                yield return content.GenerateContent(@namespace, nestingClassNames.Append(baseClassName).ToArray(), ClassName);
            }
        }
        else
        {
            yield return GenerateEmptyContent(@namespace, nestingClassNames, baseClassName);
        }
    }

    private SourceCode GenerateBaseClass(
        string @namespace,
        IReadOnlyList<string> nestingClassNames,
        string baseClassName) =>
        new($"{string.Join(".", nestingClassNames)}.{baseClassName}.{ClassName}.g.cs",
$$"""
#nullable enable
using Corvus.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace {{@namespace}};
{{NestedClassGenerator.Wrap(nestingClassNames.Append(baseClassName).ToArray(), () =>
$$"""
{{_response.Description.AsComment("summary", "para")}}
internal abstract partial class {{ClassName}} : {{baseClassName}}
{
    protected {{ClassName}}(HttpResponseMessage response)
    {
        StatusCode = response.StatusCode;{{(_anyHeaders ? 
$$"""

        Headers = ResponseHeaders.Bind(response);
""" : "")}}
    }

    internal static bool MatchesStatusCode(HttpStatusCode statusCode) =>
        {{(_hasDefaultStatusCode ? "true" : _hasExplicitStatusCode ? $"((int)statusCode) == {_responseStatusCodePattern}" : $"Matches{_responseStatusCodePattern.First()}xxStatusCode((int)statusCode)")}};

    /// <summary>
    /// Response status code
    /// </summary>
    internal HttpStatusCode StatusCode { get; private set; }
{{(_anyHeaders ? 
$$"""

    /// <summary>
    /// Response Headers
    /// </summary> 
    internal ResponseHeaders Headers { get; private set; }

    /// <summary>
    /// Response Headers
    /// </summary> 
    internal sealed class ResponseHeaders 
    {{{
        _headerGenerators.AggregateToString(generator =>
            generator.GenerateField()).Indent(8)}}

        private ResponseHeaders(HttpResponseMessage response)
        {{{
            _headerGenerators.AggregateToString(generator =>
                generator.GenerateBindDirective("response")).Indent(12)}}
        }

        internal static ResponseHeaders Bind(HttpResponseMessage response) =>
            new ResponseHeaders(response);
        {{_headerGenerators.AggregateToString(generator => 
                generator.GenerateProperty())
            .Indent(8)}}
            
        internal ValidationContext Validate(ValidationContext validationContext,
            ValidationLevel validationLevel)
        {{{
            _headerGenerators.AggregateToString(generator =>
$"""
            validationContext = {generator.GenerateValidateDirective()}
""")}}
            return validationContext;
        }
    }

""" : "")}}
    /// <summary>
    /// Bind content from http response
    /// </summary>
    /// <param name="response">Http response message to bind from</param>
    /// <param name="configuration">Web client configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An awaitable task for the response content</returns>
    internal new static Task<{{baseClassName}}> BindAsync(HttpResponseMessage response, WebClientConfiguration configuration, CancellationToken cancellationToken = default)
    {
{{(_contentGenerators.Any() ?
$$"""
        var contentType = response.Content.Headers.ContentType;
        return contentType switch
        {
            null => Unknown.BindAsync(response, cancellationToken),{{_contentGenerators.AggregateToString(generator =>
$"""
            _ when contentType.IsSubsetOf({generator.ClassName}.MediaType) => {generator.ClassName}.BindAsync(response, configuration, cancellationToken),
""")}}
            _ => Unknown.BindAsync(response, cancellationToken)
        };
""" :
"""
        return Empty.BindAsync(response, cancellationToken);
""")}}
    }

    /// <summary>
    /// Create a validation context
    /// </summary>
    /// <returns>Validation context</returns>
    protected ValidationContext CreateValidationContext() =>
        ValidationContext.ValidContext.UsingStack().UsingResults();

    /// <inheritdoc/>
    internal override ValidationContext Validate(ValidationLevel validationLevel)
    {
        var validationContext = CreateValidationContext();{{(_anyHeaders ? 
"""

        validationContext = Headers.Validate(validationContext, validationLevel);
""" : "")}}
        return validationContext;
    }
}
""")}}
#nullable restore
""");

    private SourceCode GenerateEmptyContent(
        string @namespace,
        IReadOnlyList<string> nestingClassNames,
        string baseClassName) =>
        new($"{string.Join(".", nestingClassNames)}.{baseClassName}.{ClassName}.Empty.g.cs",
$$"""
#nullable enable
using Corvus.Json;

namespace {{@namespace}};
{{NestedClassGenerator.Wrap(nestingClassNames.Append(baseClassName).Append(ClassName).ToArray(), () =>
$$"""
/// <summary>
/// Response with empty content
/// </summary>
internal sealed class Empty : {{ClassName}}
{
    private Empty(HttpResponseMessage response) : base(response)
    {
    }

    /// <summary>
    /// Construct response for empty content
    /// </summary>
    /// <param name="response">Response message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    internal static Task<{{baseClassName}}> BindAsync(HttpResponseMessage response, CancellationToken cancellationToken = default) =>
        Task.FromResult<{{baseClassName}}>(new Empty(response));

    /// <inheritdoc/>
    internal override ValidationContext Validate(ValidationLevel validationLevel) =>
        base.Validate(validationLevel);
}
""")}}
#nullable restore
""");

    private SourceCode GenerateUnknownContent(
        string @namespace,
        IReadOnlyList<string> nestingClassNames,
        string baseClassName) =>
        new($"{string.Join(".", nestingClassNames)}.{baseClassName}.{ClassName}.Unknown.g.cs",
$$"""
#nullable enable
using Corvus.Json;

namespace {{@namespace}};
{{NestedClassGenerator.Wrap(nestingClassNames.Append(baseClassName).Append(ClassName).ToArray(), () =>
$$"""
/// <summary>
/// Response for unknown content
/// </summary>
internal new sealed class Unknown : {{ClassName}}
{
    internal Stream Content { get; }

    private Unknown(Stream content, HttpResponseMessage response) : base(response)
    {
        Content = content;
    }

    /// <summary>
    /// Construct response for unknown content
    /// </summary>
    /// <param name="response">Response message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    internal static async Task<{{baseClassName}}> BindAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return new Unknown(stream, response);
    }

    /// <inheritdoc/>
    internal override ValidationContext Validate(ValidationLevel validationLevel) =>
        base.Validate(validationLevel);
}
""")}}
#nullable restore
""");
}