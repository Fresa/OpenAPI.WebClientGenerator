extern alias OpenAPIWebClientGenerator;

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using OpenAPI.WebClientGenerator.Tests.Utils;

namespace OpenAPI.WebClientGenerator.Tests;

internal static class WebClientGenerator
{
    internal static Compilation Setup(string openApiSpec,
        string clientName,
        string @namespace,
        out ImmutableArray<Diagnostic> diagnostics,
        CancellationToken cancellationToken) =>
        Run(new TestAdditionalFile($"OpenApiSpecs/{openApiSpec}"),
            clientName, @namespace, out diagnostics, cancellationToken);

    internal static Compilation SetupFromContent(string openApiSpec,
        string clientName,
        string @namespace,
        out ImmutableArray<Diagnostic> diagnostics,
        CancellationToken cancellationToken) =>
        Run(TestAdditionalFile.FromContent(openApiSpec),
            clientName, @namespace, out diagnostics, cancellationToken);

    private static Compilation Run(TestAdditionalFile clientSdkItem,
        string clientName,
        string @namespace,
        out ImmutableArray<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var generator = new OpenAPIWebClientGenerator::OpenAPI.WebClientGenerator.WebClientGenerator();

        var metadata = ImmutableDictionary<string, string>.Empty
            .Add("build_metadata.AdditionalFiles.SourceItemGroup", "WebClientGenerator")
            .Add("build_metadata.AdditionalFiles.ClientName", clientName)
            .Add("build_metadata.AdditionalFiles.Namespace", @namespace);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            additionalTexts: [clientSdkItem],
            optionsProvider: new OptionsProvider(clientSdkItem, metadata));

        const string assemblyName = nameof(WebClientGeneratorTests);
        var compilation = CSharpCompilation.Create(assemblyName,
            options: new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary));

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var newCompilation, out diagnostics, cancellationToken);

        foreach (var tree in newCompilation.SyntaxTrees)
        {
            tree.GetDiagnostics(cancellationToken).Should().NotContain(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error ||
                diagnostic.Severity == DiagnosticSeverity.Warning, 
                because: $"the syntax should be correct: {tree.GetText(cancellationToken)}");
        }
        var errorsCausedByMissingReferences = new[]
        {
            "CS0518", // predefined type is not defined or imported
            "CS0656", // missing compiler-required member
            "CS0012", // type is defined in an assembly that is not referenced
            "CS1069", // type could not be found in a namespace, per the using
            "CS0234", // type or namespace does not exist in the namespace
            "CS0246", // type or namespace could not be found
            "CS0400", // The type or namespace name could not be found in the global namespace (are you missing an assembly reference?)
            "CS8179", // Predefined type System.ValueTuple is not defined or imported
            "CS0103", // name does not exist in the current context
            "CS1061", // no definition for member (type unresolved)
            "CS0538", // explicit interface declaration is not an interface
            "CS1729", // type has no constructor with that many arguments
            "CS0314", // type cannot be a type parameter (constraint unresolved)
            "CS0305", // wrong number of type arguments (generic unresolved)
            "CS0704", // non-virtual member lookup on unresolved type
            "CS9174", // collection-expression init on unresolved type
            "CS8137", // cannot define a member on an unresolved type
            "CS1110", // cannot define an extension on an unresolved type
            "CS0229", // ambiguity between members (unresolved base)
            "CS0121", // ambiguous call (unresolved overloads)
            "CS1955", // non-invocable member used like a method
            "CS0161", // not all code paths return a value (unresolved return type)
            "CS0315", // no boxing conversion for type parameter (constraint unresolved)
            "CS8919"
        };

        var compilationDiagnostics = newCompilation.GetDiagnostics(cancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Where(diagnostic => !errorsCausedByMissingReferences.Contains(diagnostic.Id))
            .ToArray();

        compilationDiagnostics.Should().BeEmpty(because:
            "the generated code should be valid C#, but found:\n" +
            string.Join("\n", compilationDiagnostics.Select(diagnostic => diagnostic.ToString())) +
            "\n\n" +
            string.Join("\n\n", compilationDiagnostics
                .Select(diagnostic => diagnostic.Location.SourceTree)
                .Distinct()
                .Select(tree =>
                    $"""
                     // === {tree?.FilePath} ===
                     {tree?.GetText(cancellationToken)}
                     """)));

        return newCompilation;
    }

    private sealed class OptionsProvider(AdditionalText text, ImmutableDictionary<string, string> metadata)
        : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } =
            new Options(ImmutableDictionary<string, string>.Empty);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
            textFile == text ? new Options(metadata) : GlobalOptions;
    }

    private sealed class Options(ImmutableDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value);
    }
}