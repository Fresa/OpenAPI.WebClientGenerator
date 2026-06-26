using System.Collections.Generic;
using System.Linq;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class EntityGenerator(string name)
{
    private readonly Dictionary<string, MethodGenerator> _methodSignatures = new();
    private readonly string _className = name.ToPascalCase();

    internal MethodGenerator AddMethod(string pathExpression, params ParameterGenerator[] parameterGenerators)
    {
        var id = parameterGenerators.Aggregate("", (id, generator) => id + generator.FullyQualifiedTypeName);
        if (_methodSignatures.TryGetValue(id, out var methodGenerator))
            return methodGenerator;
        methodGenerator = new MethodGenerator(pathExpression, parameterGenerators);
        _methodSignatures.Add(id, methodGenerator);
        return methodGenerator;
    }

    internal IEnumerable<SourceCode> Generate(string @namespace, params string[] outerClassNames) =>
        Generate(@namespace, outerEntityNames: outerClassNames, outerClassNames: outerClassNames, rootEntity: true);

    private IEnumerable<SourceCode> Generate(
        string @namespace,
        string[] outerEntityNames,
        string[] outerClassNames,
        bool rootEntity)
    {
        var fileName = string.Join(".", outerEntityNames);

        yield return new SourceCode($"{fileName}.{_className}.g.cs", GenerateClass(@namespace, outerClassNames, rootEntity));

        var childEntityChain = outerEntityNames.Append(_className).ToArray();
        foreach (var methodGenerator in _methodSignatures.Values)
        {
            var entityClassName = _className + methodGenerator.Parameters.Length;
            var entityClassChain = outerClassNames.Append(entityClassName).ToArray();

            foreach (var operation in methodGenerator.Operations)
            {
                foreach (var source in operation.ResponseGenerator.Generate(
                    @namespace,
                    nestingClassNames: entityClassChain,
                    className: $"{operation.OperationClassName}Response"))
                {
                    yield return source;
                }

                if (operation.AuthenticationGenerator is not null)
                {
                    yield return operation.AuthenticationGenerator.Generate(@namespace, entityClassChain.Append(operation.OperationClassName).ToArray());
                }
            }

            foreach (var source in methodGenerator.Children.Values
                         .SelectMany(child =>
                             child.Generate(@namespace, childEntityChain, entityClassChain, rootEntity: false)))
            {
                yield return source;
            }
        }
    }

    private static string GetResponseTypeName(OperationGenerator operation) =>
        $"{operation.OperationClassName}Response";
    
    private string GenerateClass(string @namespace, IReadOnlyList<string> nestedClassNames, bool rootEntity = false)
    {
        return
$$"""
#nullable enable
using Corvus.Json;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using System.Text;

namespace {{@namespace}};
{{NestedClassGenerator.Wrap(nestedClassNames, () =>
$$"""
{{_methodSignatures.Values.AggregateToString(methodGenerator =>
    {
        var className = _className + methodGenerator.Parameters.Length;
        return 
$$"""
internal {{className}} {{name}}({{GetMethodParameterList(methodGenerator)}})
{{{(rootEntity ? 
"""

    var requestBuilder = new RequestBuilder(httpClient, _configuration);
""" : "")}}{{methodGenerator.Parameters.AggregateToString(parameter =>
$$""""
    requestBuilder.AddPathParameter("{{parameter.ParameterName}}",
        {{parameter.ParameterName.ToCamelCase()}},
        "{{parameter.SchemaLocation}}",
        """
        {{parameter.ParameterSpecificationAsJson.Indent(8).Trim()}}
        """);
"""").TrimEnd(',')}}
    return new(requestBuilder, {{(rootEntity ? "_" : "")}}configuration);
}

internal partial class {{className}}(RequestBuilder requestBuilder, WebClientConfiguration configuration)
{{{methodGenerator.Operations.AggregateToString(operation =>
$$"""
    internal async Task<Result<{{operation.OperationClassName}}Response>> {{operation.OperationClassName}}Async({{
        GetParameterArgumentExpressions(operation, operation.OperationClassName)
            .AggregateToString()
            .Indent(8)
            .TrimStart()
        }}{{(operation.ResponseGenerator.GeneratesContent ? 
$"""

        Accept? accepts = null,
""" : "")}}
        CancellationToken cancellation = default)
    {{{
            new ParametersGenerator []
            {
                operation.QueryGenerator,
                operation.HeadersGenerator
            }
            .Select(GetParameterBuilderMethod)
            .AggregateToString()
            .Indent(8)
        }}{{(operation.ResponseGenerator.GeneratesContent ? 
"""

        requestBuilder.AcceptMediaTypes(accepts?.MediaTypes ?? []);
""" : "")}}{{(operation.AuthenticationGenerator is null ? "" : 
"""

        authentication.AddTo(requestBuilder);
""")}}
        if (!requestBuilder.ValidationContext.IsValid)
            return Result<{{GetResponseTypeName(operation)}}>.WithInvalidRequest(requestBuilder.ValidationContext.Results
                .WithLocation(configuration.OpenApiSpecificationUri));
        var responseMessage = await requestBuilder
            .SendAsync(
                "{{methodGenerator.PathExpression}}",
                "{{operation.Operation.Method}}",
                {{GetContentExpression(operation.RequestBodyGenerator)}},
                cancellation)
            .ConfigureAwait(false);
        var response = await {{GetResponseTypeName(operation)}}.BindAsync(responseMessage, configuration, cancellation)
            .ConfigureAwait(false);
        var responseValidationContext = configuration.ValidateResponses ?
            response.Validate(configuration.ValidationLevel) :
            ValidationContext.ValidContext;
        return Result<{{GetResponseTypeName(operation)}}>.WithResponse(response, responseValidationContext.Results
            .WithLocation(configuration.OpenApiSpecificationUri));
    }{{(operation.ResponseGenerator.GeneratesContent ? 
$$"""
    
    internal sealed class Accept
    {
        private Accept() {}
        internal static Accept Content<T>()
            where T : {{GetResponseTypeName(operation)}}.IAcceptContent =>
            new Accept().And<T>();

        internal Accept And<T>()
            where T : {{GetResponseTypeName(operation)}}.IAcceptContent
        {
            _mediaTypes.Add(T.MediaType);
            return this;
        }
        
        private readonly List<MediaTypeWithQualityHeaderValue> _mediaTypes = [];
        internal MediaTypeWithQualityHeaderValue[] MediaTypes => _mediaTypes.ToArray();
    }
""" : "")}}
{{ new[] 
    { 
        operation.RequestBodyGenerator.GenerateClass(),
        operation.QueryGenerator.GenerateClass(),
        operation.HeadersGenerator.GenerateClass()
    }
    .AggregateToString()
    .Trim()
    .PrependNewline()
    .Indent(4)
    .TrimEnd()
}}
"""
)}}
}

""";
    }
)}}
""")}}
#nullable restore
""";
    }

    

    private static string GetMethodParameterList(MethodGenerator methodGenerator) =>
        methodGenerator.Parameters.AggregateToString(parameter =>
            $$"""
                  {{parameter.FullyQualifiedTypeName}} {{parameter.ParameterName.ToCamelCase()}},
              """).TrimEnd(',');

    private static string GetParameterBuilderMethod(ParametersGenerator parametersGenerator) =>
        parametersGenerator.IsEmpty
            ? string.Empty
            : $"{(parametersGenerator.IsOptional ?
                $"({parametersGenerator.ClassName.ToCamelCase()} ?? new())" : parametersGenerator.ClassName.ToCamelCase())}.AddTo(requestBuilder);";

    private static string GetParameterArgumentExpression(ParametersGenerator parametersGenerator)
    {
        if (parametersGenerator.IsEmpty)
        {
            return string.Empty;
        }

        var terny = parametersGenerator.IsOptional ? "?" : string.Empty;
        var defaultExpression = parametersGenerator.IsOptional ? " = null" : string.Empty;
        return $"{parametersGenerator.ClassName}{terny} {parametersGenerator.ClassName.ToCamelCase()}{defaultExpression},"; 
    }

    private static IEnumerable<string> GetParameterArgumentExpressions(
        OperationGenerator operationGenerator,
        string operationClassName)
    {
        var expressions = new ParametersGenerator[]
            {
                operationGenerator.QueryGenerator,
                operationGenerator.HeadersGenerator
            }.OrderBy(generator => generator.IsOptional)
            .Select(GetParameterArgumentExpression);
        
        if (operationGenerator.RequestBodyGenerator.HasBody)
        {
            expressions = operationGenerator.RequestBodyGenerator.IsRequired
                ? expressions.Prepend("Content content,")
                : expressions.Append("Content? content = null,");
        }

        if (operationGenerator.AuthenticationGenerator is not null)
        {
            expressions = expressions.Prepend($"{operationClassName}.Authentication authentication,");
        }

        return expressions;
    }
    
    private static string GetContentExpression(RequestBodyGenerator bodyGenerator)
    {
        if (!bodyGenerator.HasBody)
            return "null";
        return bodyGenerator.IsRequired ? "content.Get()" : "content?.Get()";
    }
}
