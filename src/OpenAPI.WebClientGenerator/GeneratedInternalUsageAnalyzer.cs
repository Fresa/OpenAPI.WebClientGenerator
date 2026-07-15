using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace OpenAPI.WebClientGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GeneratedInternalUsageAnalyzer : DiagnosticAnalyzer
{
    private static readonly string SourceGeneratorName = typeof(WebClientGenerator).FullName!;
    private static readonly DiagnosticDescriptor Rule = new(
        id: "AF0002",
        title: "Access to internal generated symbol",
        messageFormat: "Cannot access internal generated {0} '{1}'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: $"Internal symbols produced by {SourceGeneratorName} should not be accessed as they can introduce breaking changes without notice.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Include analyze code that is identified as generated
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, 
            SyntaxKind.IdentifierName,
            SyntaxKind.GenericName);
        context.RegisterSymbolAction(AnalyzeSymbol, 
            SymbolKind.NamedType);
    }

    private static void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
    {
        if (IsFromSourceGenerator(context.Node.SyntaxTree))
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol;
        if (symbol is not null && ReferencesGeneratedInternalSymbols(symbol))
        {
            context.ReportDiagnostic(
                AccessToGeneratedInternalSymbol(symbol, 
                    context.Node.GetLocation()));
        }
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        var symbol = context.Symbol;
        if (!ReferencesGeneratedInternalSymbols(symbol))
        {
            return;
        }

        foreach (var location in symbol.Locations.Where(location => 
                     !IsFromSourceGenerator(location.SourceTree)))
        {
            context.ReportDiagnostic(
                AccessToGeneratedInternalSymbol(symbol, location));
        }
    }

    private static Diagnostic AccessToGeneratedInternalSymbol(ISymbol symbol, Location location) =>
        Diagnostic.Create(
            Rule,
            location,
            DescribeKind(symbol),
            symbol.ToDisplayString());

    private static string DescribeKind(ISymbol symbol) =>
        symbol switch
        {
            IMethodSymbol { MethodKind: MethodKind.Constructor } => "constructor",
            IMethodSymbol => "method",
            IPropertySymbol => "property",
            IFieldSymbol => "field",
            IEventSymbol => "event",
            INamedTypeSymbol => "type",
            _ => "member",
        };

    private static bool ReferencesGeneratedInternalSymbols(ISymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current is
                {
                    DeclaredAccessibility: Accessibility.Internal,
                    DeclaringSyntaxReferences.Length: > 0
                } &&
                current.DeclaringSyntaxReferences.Any(reference =>
                    IsFromSourceGenerator(reference.SyntaxTree)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFromSourceGenerator(SyntaxTree? tree) =>
        tree is not null &&
        tree.FilePath.Replace('\\', '/').Contains($"/{SourceGeneratorName}/");
}