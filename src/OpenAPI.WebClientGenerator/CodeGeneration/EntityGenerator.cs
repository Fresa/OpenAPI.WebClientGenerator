using System.Collections.Generic;
using System.Linq;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal sealed class EntityGenerator(string name)
{
    private readonly Dictionary<int, MethodGenerator> _methodSignatures = new();
    private readonly string _className = name.ToPascalCase();

    internal MethodGenerator AddMethod(string pathExpression, params ParameterGenerator[] pathParameterGenerators)
    {
        var id = pathParameterGenerators.Length;
        if (_methodSignatures.TryGetValue(id, out var methodGenerator))
            return methodGenerator;
        methodGenerator = new MethodGenerator(pathExpression, pathParameterGenerators);
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
            var entityClassName = GetEntityClassName(methodGenerator);
            var entityFullyQualifiedName = outerClassNames.Append(entityClassName).ToArray();

            foreach (var operation in methodGenerator.Operations)
            {
                var operationFullyQualifiedName = 
                    entityFullyQualifiedName.Append(operation.OperationClassName)
                        .ToArray(); 
                foreach (var source in operation.ResponseGenerator.Generate(
                             @namespace,
                             nestingClassNames: operationFullyQualifiedName))
                {
                    yield return source;
                }

                if (operation.AuthenticationGenerator is not null)
                {
                    yield return operation.AuthenticationGenerator.Generate(
                        @namespace, operationFullyQualifiedName);
                }

                yield return operation.QueryGenerator.Generate(
                    @namespace, operationFullyQualifiedName);
                yield return operation.HeadersGenerator.Generate(
                    @namespace, operationFullyQualifiedName);
                yield return operation.RequestBodyGenerator.Generate(
                    @namespace, operationFullyQualifiedName);
            }

            foreach (var source in methodGenerator.Children.Values
                         .SelectMany(child =>
                             child.Generate(@namespace, childEntityChain, entityFullyQualifiedName, rootEntity: false)))
            {
                yield return source;
            }
        }
    }

    private string GetEntityClassName(MethodGenerator methodGenerator) => 
        _className + methodGenerator.Parameters.Length;

    private static string GetResponseTypeName(OperationGenerator operation) =>
        $"{operation.OperationClassName}.{ResponseGenerator.ClassName}";
    
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
        var entityClassName = GetEntityClassName(methodGenerator);
        return 
$$"""
internal {{entityClassName}} {{name}}({{GetMethodParameterList(methodGenerator)}})
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

internal partial class {{entityClassName}}(RequestBuilder requestBuilder, WebClientConfiguration configuration)
{{{methodGenerator.Operations.AggregateToString(operation =>
$$"""
    internal async Task<Result<{{GetResponseTypeName(operation)}}>> {{operation.OperationClassName}}Async({{
        GetParameterArgumentExpressions(operation)
            .AggregateToString()
            .Indent(8)
            .TrimStart()
        }}{{(operation.ResponseGenerator.GeneratesContent ? 
$"""

        {GetResponseTypeName(operation)}.Accept? accepts = null,
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

        security.AddTo(requestBuilder);
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
    }

"""
).TrimEnd()}}
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

    private static string GetParameterArgumentExpression(
        ParametersGenerator parametersGenerator,
        OperationGenerator operationGenerator)
    {
        if (parametersGenerator.IsEmpty)
        {
            return string.Empty;
        }

        var terny = parametersGenerator.IsOptional ? "?" : string.Empty;
        var defaultExpression = parametersGenerator.IsOptional ? " = null" : string.Empty;
        return $"{operationGenerator.OperationClassName}.{parametersGenerator.ClassName}{terny} {parametersGenerator.ClassName.ToCamelCase()}{defaultExpression},"; 
    }

    private static IEnumerable<string> GetParameterArgumentExpressions(
        OperationGenerator operationGenerator)
    {
        var expressions = new ParametersGenerator[]
            {
                operationGenerator.QueryGenerator,
                operationGenerator.HeadersGenerator
            }.OrderBy(generator => generator.IsOptional)
            .Select(parametersGenerator => 
                GetParameterArgumentExpression(parametersGenerator, operationGenerator));
        
        if (operationGenerator.RequestBodyGenerator.HasBody)
        {
            expressions = operationGenerator.RequestBodyGenerator.IsRequired
                ? expressions.Prepend($"{operationGenerator.OperationClassName}.Content content,")
                : expressions.Append($"{operationGenerator.OperationClassName}.Content? content = null,");
        }

        if (operationGenerator.AuthenticationGenerator is not null)
        {
            expressions = expressions.Prepend($"{operationGenerator.OperationClassName}.SecurityRequirement security,");
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
