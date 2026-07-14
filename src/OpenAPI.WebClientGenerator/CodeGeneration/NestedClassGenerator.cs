using System;
using System.Collections.Generic;
using System.Linq;
using OpenAPI.WebClientGenerator.Extensions;

namespace OpenAPI.WebClientGenerator.CodeGeneration;

internal static class NestedClassGenerator
{
    internal static string Wrap(IReadOnlyList<string> classNames, Func<string> content)
    {
        if (classNames.Count == 0)
        {
            return content().Trim();
        }

        var className = classNames[0];
        var inner = Wrap(classNames.Skip(1).ToArray(), content);
        return
$$"""
public partial class {{className}}
{
{{inner.Indent(4)}}
}
""";
    }
}