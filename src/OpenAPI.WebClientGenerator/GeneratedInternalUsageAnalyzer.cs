using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace OpenAPI.WebClientGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GeneratedInternalUsageAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: "AF0002",
        title: "Access to internal generated member",
        messageFormat: "Cannot access internal generated {0} '{1}'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Internal types and members produced by the web client generator should not be access as they can introduce breaking changes without notice.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Ignore generated code
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterOperationAction(
            Analyze,
            OperationKind.ObjectCreation,
            OperationKind.Invocation,
            OperationKind.FieldReference,
            OperationKind.PropertyReference,
            OperationKind.EventReference,
            OperationKind.MethodReference,
            OperationKind.TypeOf);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        var symbol = context.Operation switch
        {
            IObjectCreationOperation operation => operation.Constructor,
            IInvocationOperation operation => operation.TargetMethod,
            IFieldReferenceOperation operation => (ISymbol?)operation.Field,
            IPropertyReferenceOperation operation => operation.Property,
            IEventReferenceOperation operation => operation.Event,
            IMethodReferenceOperation operation => operation.Method,
            ITypeOfOperation operation => operation.TypeOperand,
            _ => null,
        };

        if (symbol is not null && IsGeneratedInternal(symbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule, 
                context.Operation.Syntax.GetLocation(),
                DescribeKind(symbol), 
                symbol.ToDisplayString()));
        }
    }

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

    private static bool IsGeneratedInternal(ISymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility == Accessibility.Internal && 
                IsGeneratedFromWebClientGenerator(current))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGeneratedFromWebClientGenerator(ISymbol symbol) =>
        symbol.DeclaringSyntaxReferences.Length > 0 &&
        symbol.DeclaringSyntaxReferences.All(reference =>
            reference.SyntaxTree.FilePath.Replace('\\', '/')
                .Contains($"/{typeof(WebClientGenerator).FullName!}/"));
}