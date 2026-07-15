extern alias OpenAPIWebClientGenerator;

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace OpenAPI.WebClientGenerator.Tests;

public class GeneratedInternalUsageAnalyzerTests
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private const string GeneratedFile =
        "OpenAPI.WebClientGenerator/OpenAPI.WebClientGenerator.WebClientGenerator/RequestBuilder.g.cs";

    private const string GeneratedFileByOtherGenerator =
        "Some.Other.Generator/Some.Other.Generator.OtherGenerator/Other.g.cs";

    private const string GeneratedCode =
        """
        namespace Sdk;
        internal sealed class RequestBuilder { internal void Send() { } }
        public sealed class Client { public void Ok() { } }
        """;

    [Fact]
    public async Task HandWrittenUsageOfGeneratedInternal_IsReported()
    {
        var diagnostics = await AnalyzeAsync(
            (GeneratedFile, GeneratedCode),
            ("App.cs",
                """
                namespace App;
                internal static class Bad
                {
                    static void M() => new Sdk.RequestBuilder().Send();
                }
                """));

        diagnostics.Should().Contain(diagnostic => diagnostic.Id == "AF0002");
    }

    [Fact]
    public async Task HandWrittenUsageOfPublicContract_IsNotReported()
    {
        var diagnostics = await AnalyzeAsync(
            (GeneratedFile, GeneratedCode),
            ("App.cs",
                """
                namespace App;
                internal static class Good
                {
                    static void M(Sdk.Client client) => client.Ok();
                }
                """));

        diagnostics.Should().NotContain(diagnostic => diagnostic.Id == "AF0002");
    }

    [Fact]
    public async Task GeneratedCallerUsingInternal_IsNotReported()
    {
        var diagnostics = await AnalyzeAsync(
            (GeneratedFile, GeneratedCode),
            ("OpenAPI.WebClientGenerator/OpenAPI.WebClientGenerator.WebClientGenerator/Caller.g.cs",
                """
                namespace Sdk;
                internal static class GeneratedCaller
                {
                    static void M() => new RequestBuilder().Send();
                }
                """));

        diagnostics.Should().NotContain(diagnostic => diagnostic.Id == "AF0002");
    }

    [Fact]
    public async Task HandWrittenUsageOfADifferentGeneratorsInternal_IsNotReported()
    {
        var diagnostics = await AnalyzeAsync(
            (GeneratedFileByOtherGenerator,
                """
                namespace Other;
                internal sealed class OtherInternal { internal void X() { } }
                """),
            ("App.cs",
                """
                namespace App;
                internal static class Bad
                {
                    static void M() => new Other.OtherInternal().X();
                }
                """));

        diagnostics.Should().NotContain(diagnostic => diagnostic.Id == "AF0002");
    }

    [Fact]
    public async Task HandWrittenFileWithGeneratedName_IsReported()
    {
        var diagnostics = await AnalyzeAsync(
            (GeneratedFile, GeneratedCode),
            ("Bad.g.cs",
                """
                namespace App;
                internal static class Bad
                {
                    static void M() => new Sdk.RequestBuilder().Send();
                }
                """));

        diagnostics.Should().Contain(diagnostic => diagnostic.Id == "AF0002");
    }

    [Fact]
    public async Task OtherGeneratorReferencingOurInternal_IsReported()
    {
        var diagnostics = await AnalyzeAsync(
            (GeneratedFile, GeneratedCode),
            (GeneratedFileByOtherGenerator,
                """
                namespace Other;
                internal static class OtherCaller
                {
                    static void M() => new Sdk.RequestBuilder().Send();
                }
                """));

        diagnostics.Should().Contain(diagnostic => diagnostic.Id == "AF0002");
    }

    [Fact]
    public async Task HandWrittenPartialExtensionOfGeneratedInternal_IsReported()
    {
        var diagnostics = await AnalyzeAsync(
            (GeneratedFile,
                """
                namespace Sdk;
                internal partial class RequestBuilder { internal void Send() { } }
                """),
            ("App.cs",
                """
                namespace Sdk;
                internal partial class RequestBuilder { }
                """));

        diagnostics.Should().Contain(diagnostic => diagnostic.Id == "AF0002");
    }

    private async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(params (string Path, string Source)[] files)
    {
        var trees = files
            .Select(file => CSharpSyntaxTree.ParseText(
                file.Source, path: file.Path, cancellationToken: Cancellation))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "AnalyzerTest",
            trees,
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new OpenAPIWebClientGenerator::OpenAPI.WebClientGenerator.GeneratedInternalUsageAnalyzer();

        var diagnostics = await compilation
            .WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync(Cancellation);

        // Guard against the analyzer throwing (surfaced as AD0001).
        diagnostics.Should().NotContain(diagnostic => diagnostic.Id == "AD0001");
        return diagnostics;
    }
}