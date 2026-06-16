using Microsoft.OpenApi;
using OpenAPI.WebClientGenerator.OpenApi;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class HttpResponseMessageExtensionsGenerator(
    OpenApiSpecVersion openApiVersion,
    string @namespace)
{
    private const string ClassName = "HttpResponseMessageExtensions";

    internal SourceCode GenerateClass() =>
        new($"{ClassName}.g.cs",
        $$$""""
        #nullable enable
        using Corvus.Json;
        using Microsoft.Extensions.Primitives;
        using OpenAPI.ParameterStyleParsers;
        using System.Collections.Concurrent;
        using System.Diagnostics.CodeAnalysis;
        using System.Net.Http.Headers;
        using System.Text.Json;

        namespace {{{@namespace}}};

        /// <summary>
        /// Extension methods for http response messages
        /// </summary>
        internal static class {{{ClassName}}}
        {
            private const string ParameterValueParserVersion = "{{{openApiVersion.GetParameterVersion()}}}";
            
            private static readonly ConcurrentDictionary<IParameter, IParameterValueParser> ParserCache = new();
            private static IParameterValueParser GetParser(IParameter parameter) => 
                ParserCache.GetOrAdd(parameter, _ => 
                    parameter.CreateParameterValueParser());
            
            private static readonly ConcurrentDictionary<string, IParameter> ParameterCache = new();
            private static IParameter GetParameter(string parameterSpecificationAsJson) => 
                ParameterCache.GetOrAdd(parameterSpecificationAsJson, _ => 
                    ParameterFactory.OpenApi(ParameterValueParserVersion, parameterSpecificationAsJson));

            /// <summary>
            /// Binds an http response parameter to a json type
            /// </summary>
            /// <param name="response">Response message to bind from</param>
            /// <param name="parameterSpecificationAsJson">OpenAPI parameter specification formatted as json</param>
            /// <typeparam name="T">The type to bind</typeparam>
            /// <returns>The bind result of the parameter</returns>
            internal static BindResult<T> Bind<T>(this HttpResponseMessage response, 
                string parameterSpecificationAsJson)
                where T : struct, IJsonValue<T>
            {
                var parameter = GetParameter(parameterSpecificationAsJson);
                return parameter switch
                {
                    _ when TryParse<T>(response, parameter, out var value) => value.Value,
                    _ => new BindResult<T>(T.Undefined)
                };
            }
           
            private static bool TryParse<T>(this HttpResponseMessage response, 
                IParameter parameter, 
                [NotNullWhen(true)] out BindResult<T>? value) 
                    where T : struct, IJsonValue<T> =>
                parameter switch
                {
                    _ when parameter.InHeader => TryParseHeader<T>(response.Headers, parameter, out value),
                    _ => throw new InvalidOperationException($"Parameter {parameter.Name} has an unknown location")
                };

            private static bool TryParseHeader<T>(HttpResponseHeaders headers, 
                IParameter parameter, 
                [NotNullWhen(true)] out BindResult<T>? value)
                    where T : struct, IJsonValue<T>
            {
                if (headers.TryGetValues(parameter.Name, out var values))
                {
                    value = Parse<T>(values.ToArray(), parameter);
                    return true;
                } 
                value = null;
                return false;
            }

            private static BindResult<T> Parse<T>(string[] values, 
                IParameter parameter)
                where T : struct, IJsonValue<T>
            {
                if (values.Length == 0)
                {
                    return new BindResult<T>(default(T));
                }
                
                var parser = GetParser(parameter);
                var stringValue = string.Join(parser.Delimiter, 
                    parser.ValueIncludesParameterName 
                        ? values.Select(value => $"{parameter.Name}={value}") 
                        : values);
                
                return Parse<T>(parser, stringValue);
            }
            
            private static BindResult<T> Parse<T>(IParameterValueParser parser, string? value)
                where T : struct, IJsonValue<T>
            {
                if (!parser.TryParse(value, out var instance, out var error))
                {
                    return new BindResult<T>(error);
                }
            
                return new BindResult<T>(instance == null ? 
                    T.Null : 
                    T.Parse(instance.ToJsonString()));
            }
        }
        #nullable restore
        """");
}