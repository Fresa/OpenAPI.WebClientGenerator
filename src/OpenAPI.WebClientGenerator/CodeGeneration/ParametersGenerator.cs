using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal abstract class ParametersGenerator
{
    protected ParametersGenerator(ParameterGenerator[] parameters)
    {
        Parameters = parameters.Where(generator => 
                generator.Location == Location)
            .ToArray();
        IsEmpty = Parameters.Length == 0;
        IsOptional = Parameters.All(generator => !generator.IsParameterRequired);
    }

    protected abstract ParameterLocation Location { get; }
    private ParameterGenerator[] Parameters { get; }
    
    internal bool IsEmpty { get; }
    internal string ClassName => Location.GetDisplayName().ToPascalCase();
    internal bool IsOptional { get; }

    internal SourceCode Generate(string @namespace, IReadOnlyList<string> nestingClassNames) =>
        new($"{string.Join(".", nestingClassNames)}.{ClassName}.g.cs",
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

    internal string GenerateClass()
    {
        if (Parameters.Length == 0)
        {
            return string.Empty;
        }

        var className = ClassName;

        return
$$""""
public sealed class {{className}}
{{{Parameters.AggregateToString(parameter =>
$$"""
    public {{(parameter.IsParameterRequired ? "required " : "")}}{{parameter.FullyQualifiedTypeName}} {{parameter.ParameterName.ToPascalCase()}} { get; init; }
""")}}

    internal RequestBuilder AddTo(RequestBuilder requestBuilder)
    {{{Parameters.AggregateToString(parameter =>
$$""""
        requestBuilder.Add{{className}}<{{parameter.FullyQualifiedTypeDeclarationIdentifier}}>("{{parameter.ParameterName}}",
            {{parameter.ParameterName.ToPascalCase()}},
            {{parameter.IsParameterRequired.ToString().ToLowerInvariant()}},
            "{{parameter.SchemaLocation}}",
            """
            {{parameter.ParameterSpecificationAsJson.Indent(12).Trim()}}
            """);
"""")}}
        return requestBuilder;
    }
}
"""";
    }
}