using System.Collections.Generic;
using System.Linq;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class ResponseGenerator(
    List<ResponseContentGenerator> responseContentGenerators)
{
    internal const string ClassName = "Response"; 
    internal bool GeneratesContent { get; } = responseContentGenerators.Any(generator => generator.HasBodies);
    
    public IEnumerable<SourceCode> Generate(
        string @namespace,
        IReadOnlyList<string> nestingClassNames)
    {
        yield return GenerateBaseClass(@namespace, nestingClassNames);
        var responseNestingClassNames = nestingClassNames.Append(ClassName).ToArray();
        yield return GenerateUnknown(@namespace, responseNestingClassNames);
        if (GeneratesContent)
        {
            yield return GenerateAccept(@namespace, responseNestingClassNames);
        }
        foreach (var source in responseContentGenerators.SelectMany(generator => 
                     generator.Generate(@namespace, responseNestingClassNames)))
        {
            yield return source;
        }
    }

    private SourceCode GenerateBaseClass(
        string @namespace,
        IReadOnlyList<string> nestingClassNames) =>
        new($"{string.Join(".", nestingClassNames)}.{ClassName}.g.cs",
$$"""
#nullable enable
using Corvus.Json;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace {{@namespace}};
{{NestedClassGenerator.Wrap(nestingClassNames, () =>
$$"""
/// <summary>
/// Contains the operation's response objects
/// </summary>
public abstract partial class {{ClassName}}
{{{Enumerable.Range(1, 5).AggregateToStringAsIs(i =>
$$"""

    /// <summary>
    /// Check if status code is {{i}}xx
    /// </summary>
    /// <param name="code">Status code to match</param>
    /// <returns>true if code matches</returns>
    protected static bool Matches{{i}}xxStatusCode(int code) =>
        code >= {{i}}00 && code <= {{i}}99;
""")}}
    /// <summary>
    /// Validate the response
    /// </summary>
    /// <param name="validationLevel">Validation level</param>
    /// <returns>The validation result</returns>
    internal abstract ValidationContext Validate(ValidationLevel validationLevel);

    /// <summary>
    /// Read response content as json
    /// </summary>
    /// <param name="response">Response message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Construct response
    /// </summary>
    /// <param name="response">Response message</param>
    /// <param name="configuration">Web client configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    internal static Task<{{ClassName}}> BindAsync(HttpResponseMessage response, WebClientConfiguration configuration, CancellationToken cancellationToken = default) =>
        response.StatusCode switch
        {{{responseContentGenerators.AggregateToString(generator =>
$"""
            _ when {generator.ClassName}.MatchesStatusCode(response.StatusCode) => {generator.ClassName}.BindAsync(response, configuration, cancellationToken),
""")}}
            _ => {{ClassName}}.Unknown.BindAsync(response, cancellationToken)
        };{{(GeneratesContent ? 
"""


    public interface IAcceptContent
    {
        public abstract static MediaTypeWithQualityHeaderValue MediaType { get; }
    }
""" : "")}}
}
""")}}
#nullable restore
""");

    private static SourceCode GenerateAccept(
        string @namespace,
        IReadOnlyList<string> nestingClassNames) =>
        new($"{string.Join(".", nestingClassNames)}.Accept.g.cs",
$$"""
#nullable enable
using System.Collections.Generic;
using System.Net.Http.Headers;

namespace {{@namespace}};
{{NestedClassGenerator.Wrap(nestingClassNames, () =>
$$"""
public sealed class Accept
{
    private Accept() {}
    public static Accept Content<T>()
        where T : {{ClassName}}.IAcceptContent =>
        new Accept().And<T>();

    public Accept And<T>()
        where T : {{ClassName}}.IAcceptContent
    {
        _mediaTypes.Add(T.MediaType);
        return this;
    }

    private readonly List<MediaTypeWithQualityHeaderValue> _mediaTypes = [];
    internal MediaTypeWithQualityHeaderValue[] MediaTypes => _mediaTypes.ToArray();
}
""")}}
#nullable restore
""");

    private static SourceCode GenerateUnknown(
        string @namespace,
        IReadOnlyList<string> nestingClassNames) =>
        new($"{string.Join(".", nestingClassNames)}.Unknown.g.cs",
$$"""
#nullable enable
using Corvus.Json;
using System.Net;

namespace {{@namespace}};
{{NestedClassGenerator.Wrap(nestingClassNames, () =>
$$"""
/// <summary>
/// Unknown response
/// </summary>
public sealed class Unknown : {{ClassName}}
{
    public Stream Content { get; }

    private Unknown(Stream content, HttpResponseMessage response)
    {
        Content = content;
        StatusCode = response.StatusCode;
    }

    /// <summary>
    /// Construct unknown response
    /// </summary>
    /// <param name="response">Response message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    internal static async Task<{{ClassName}}> BindAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return new Unknown(stream, response);
    }

    /// <summary>
    /// Response status code
    /// </summary>
    public HttpStatusCode StatusCode { get; private set; }

    /// <inheritdoc/>
    internal override ValidationContext Validate(ValidationLevel validationLevel) =>
        ValidationContext.ValidContext.UsingStack().UsingResults();
}
""")}}
#nullable restore
""");
}