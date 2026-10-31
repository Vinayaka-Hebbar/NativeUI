using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NativeUI.Xaml.Compiler;

/// <summary>
/// Incremental Roslyn source generator that orchestrates <see cref="XamlParser"/>, <see cref="XamlValidator"/>,
/// and <see cref="XamlEmitter"/> for <c>.xaml</c> files included in <c>AdditionalFiles</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class XamlGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<(string FilePath, string Content, string DefaultNamespace, Compilation Compilation)> xamlInputs = context
            .AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Combine(context.CompilationProvider)
            .Select(static (tuple, cancellationToken) =>
            {
                AdditionalText file = tuple.Left.Left;
                Compilation compilation = tuple.Right;

                string defaultNs = tuple.Left.Right.GlobalOptions.TryGetValue("build_property.RootNamespace", out string? rootNs) &&
                                   !string.IsNullOrWhiteSpace(rootNs)
                    ? rootNs
                    : (!string.IsNullOrWhiteSpace(compilation.AssemblyName) ? compilation.AssemblyName! : "NativeUI");

                SourceText? sourceText = file.GetText(cancellationToken);
                return (
                    file.Path,
                    Content: sourceText?.ToString() ?? string.Empty,
                    DefaultNamespace: defaultNs,
                    Compilation: compilation);
            });

        context.RegisterSourceOutput(xamlInputs, static (productionContext, item) =>
        {
            XamlDocumentNode ast = XamlParser.Parse(item.FilePath, item.Content);
            ValidatedXamlDocument validated = XamlValidator.Validate(ast, defaultNamespace: item.DefaultNamespace);

            foreach (Diagnostic diagnostic in validated.Diagnostics)
            {
                productionContext.ReportDiagnostic(diagnostic);
            }

            if (validated.Diagnostics.Any(static d => d.Severity == DiagnosticSeverity.Error))
            {
                return;
            }

            Accessibility viewAccess = InspectViewAccessibility(item.Compilation, validated.ClassName);
            string generatedCSharp = XamlEmitter.Emit(validated, viewAccess);
            if (!string.IsNullOrEmpty(generatedCSharp))
            {
                productionContext.AddSource(
                    $"{validated.ClassName}.Xaml.g.cs",
                    SourceText.From(generatedCSharp, Encoding.UTF8));
            }
        });
    }

    private static Accessibility InspectViewAccessibility(Compilation compilation, string className)
    {
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            SyntaxNode root = tree.GetRoot();
            foreach (ClassDeclarationSyntax classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!string.Equals(classDecl.Identifier.Text, className, StringComparison.Ordinal))
                {
                    continue;
                }

                return classDecl.Modifiers.Any(static m => m.IsKind(SyntaxKind.InternalKeyword))
                    ? Accessibility.Internal
                    : Accessibility.Public;
            }
        }

        return Accessibility.Public;
    }
}
