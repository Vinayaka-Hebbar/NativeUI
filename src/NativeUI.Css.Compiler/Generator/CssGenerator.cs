using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NativeUI.Css.Compiler;

/// <summary>
/// Incremental Roslyn source generator that orchestrates <see cref="CssParser"/>, <see cref="CssValidator"/>,
/// and <see cref="CssEmitter"/> for <c>.xaml.css</c> files paired with a <c>.xaml</c> view in <c>AdditionalFiles</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class CssGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<ImmutableHashSet<string>> xamlPathsProvider = context
            .AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, _) => NormalizePath(file.Path))
            .Collect()
            .Select(static (paths, _) => paths.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase));

        IncrementalValuesProvider<(string FilePath, string Content, string TargetNamespace, Accessibility ViewAccessibility)> cssInputs = context
            .AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".xaml.css", StringComparison.OrdinalIgnoreCase))
            .Combine(xamlPathsProvider)
            .Where(static pair =>
            {
                // Only process .xaml.css files that have a paired .xaml file in the project.
                string pairedXamlPath = NormalizePath(pair.Left.Path[..^4]);
                return pair.Right.Contains(pairedXamlPath);
            })
            .Select(static (pair, _) => pair.Left)
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Combine(context.CompilationProvider)
            .Select(static (tuple, cancellationToken) =>
            {
                AdditionalText file = tuple.Left.Left;
                var options = tuple.Left.Right.GetOptions(file);
                Compilation compilation = tuple.Right;

                string fallbackNs = tuple.Left.Right.GlobalOptions.TryGetValue("build_property.RootNamespace", out string? rootNs) &&
                                    !string.IsNullOrWhiteSpace(rootNs)
                    ? rootNs
                    : (!string.IsNullOrWhiteSpace(compilation.AssemblyName) ? compilation.AssemblyName! : "NativeUI");

                string viewClassName = DeriveViewClassName(file.Path);
                (string? detectedNs, Accessibility viewAccess) = InspectViewDeclaration(compilation, viewClassName);

                string targetNs = options.TryGetValue("build_metadata.AdditionalFiles.TargetNamespace", out string? itemNs) &&
                                  !string.IsNullOrWhiteSpace(itemNs)
                    ? itemNs
                    : (detectedNs ?? fallbackNs);

                SourceText? sourceText = file.GetText(cancellationToken);
                return (
                    file.Path,
                    Content: sourceText?.ToString() ?? string.Empty,
                    TargetNamespace: targetNs,
                    ViewAccessibility: viewAccess);
            });

        context.RegisterSourceOutput(cssInputs, static (productionContext, item) =>
        {
            CssStylesheetNode ast = CssParser.Parse(item.FilePath, item.Content);
            ValidatedCssStylesheet validated = CssValidator.Validate(ast);

            foreach (Diagnostic diagnostic in validated.Diagnostics)
            {
                productionContext.ReportDiagnostic(diagnostic);
            }

            if (validated.Diagnostics.Any(static d => d.Severity == DiagnosticSeverity.Error))
            {
                return;
            }

            string generatedCSharp = CssEmitter.Emit(validated, item.TargetNamespace, item.ViewAccessibility);
            productionContext.AddSource(
                $"{validated.Id}.Css.g.cs",
                SourceText.From(generatedCSharp, Encoding.UTF8));
        });
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string DeriveViewClassName(string filePath)
    {
        string name = Path.GetFileName(filePath);
        if (name.EndsWith(".xaml.css", StringComparison.OrdinalIgnoreCase))
        {
            return name[..^9];
        }

        return Path.GetFileNameWithoutExtension(name);
    }

    private static (string? Namespace, Accessibility Accessibility) InspectViewDeclaration(
        Compilation compilation,
        string viewClassName)
    {
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            SyntaxNode root = tree.GetRoot();
            foreach (ClassDeclarationSyntax classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!string.Equals(classDecl.Identifier.Text, viewClassName, StringComparison.Ordinal))
                {
                    continue;
                }

                Accessibility access = classDecl.Modifiers.Any(static m => m.IsKind(SyntaxKind.InternalKeyword))
                    ? Accessibility.Internal
                    : Accessibility.Public;

                BaseNamespaceDeclarationSyntax? nsSyntax = classDecl.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
                string? ns = nsSyntax?.Name.ToString();
                return (ns, access);
            }
        }

        return (null, Accessibility.Public);
    }
}
