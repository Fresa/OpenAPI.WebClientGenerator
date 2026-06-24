using System.Linq;

namespace OpenAPI.WebClientGenerator.CodeGeneration.Authentication;

internal static class ArgumentExtensions
{
    internal static string GetMethodArgumentList(this SecurityScheme.Argument[] arguments) =>
        string.Join(", ", arguments.Select(argument => argument.Name));

    internal static string GetMethodParameterList(this SecurityScheme.Argument[] arguments) =>
        string.Join(", ", arguments.Select(argument => $"{argument.Type} {argument.Name}"));
}