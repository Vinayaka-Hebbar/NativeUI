using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace NativeUI.UnicodeGenerator;

/// <summary>
/// Generates Unicode tables with Roslyn from checksum-pinned official data without network access.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class UnicodeGenerator : IIncrementalGenerator
{
    private const string UnicodeVersion = "17.0.0";
    private const int CodePointCount = 0x110000;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly DiagnosticDescriptor InvalidInput = new(
        "NUIUNICODE001", "Invalid Unicode generation input", "{0}", "NativeUI.UnicodeGenerator", DiagnosticSeverity.Error, true);
    private static readonly InputFile[] Inputs =
    [
        new("DerivedBidiClass", "extracted/DerivedBidiClass.txt", "4867b4b7f0731ed1bfcd34cc6251211ff1542541fce0734b6fbda139ee80b3a4"),
        new("LineBreak", "LineBreak.txt", "e6a18fa91f8f6a6f8e534b1d3f128c21ada45bfe152eb6b1bcc5e15fd8ac92e6"),
        new("WordBreakProperty", "auxiliary/WordBreakProperty.txt", "72274cac1e6b919507db35655c3e175aa27274668a1ece95c28d2069f2ad9852"),
        new("BidiBrackets", "BidiBrackets.txt", "dadbaf38a0d0246e5b805bf8725cb81b7c621f93d030595635f5ba2c2f179428")
    ];

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<ImmutableArray<InputText>> inputs = context.AdditionalTextsProvider
            .Where(static file => Inputs.Any(input => Path.GetFileName(file.Path) == input.Name + ".txt")
                || Path.GetFileName(file.Path) == "LICENSE.txt")
            .Select(static (file, cancellationToken) => new InputText(Path.GetFileName(file.Path), file.GetText(cancellationToken)?.ToString()))
            .Collect();
        context.RegisterSourceOutput(inputs.Combine(context.CompilationProvider),
            static (productionContext, input) => Generate(productionContext, input.Left, input.Right));
    }

    private static void Generate(SourceProductionContext context, ImmutableArray<InputText> suppliedInputs, Compilation compilation)
    {
        try
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            Dictionary<string, string> inputs = ReadInputs(suppliedInputs);
            INamedTypeSymbol tables = compilation.GetSymbolsWithName(static name => name == "UnicodeDataTables", SymbolFilter.Type, context.CancellationToken)
                .OfType<INamedTypeSymbol>().SingleOrDefault()
                ?? throw new InvalidDataException("Cannot find the UnicodeDataTables declaration in the consuming compilation.");
            string targetNamespace = tables.ContainingNamespace.ToDisplayString();
            Dictionary<string, byte> bidiProperties = ReadEnum(compilation, targetNamespace + ".BidiClass");
            Dictionary<string, byte> lineProperties = ReadEnum(compilation, targetNamespace + ".LineBreakEnumerator+LineBreakClass");
            Dictionary<string, byte> wordProperties = ReadEnum(compilation, targetNamespace + ".UnicodeTextSegmentation+WordBreakProperty");
            uint[] bidiRuns = CreatePropertyRuns(inputs["DerivedBidiClass"], bidiProperties);
            uint[] lineRuns = CreatePropertyRuns(inputs["LineBreak"], lineProperties);
            uint[] wordRuns = CreatePropertyRuns(inputs["WordBreakProperty"], wordProperties);
            BracketTables brackets = ReadBrackets(inputs["BidiBrackets"]);
            VerifyPrivateUseDefaults(bidiRuns, lineRuns, wordRuns, bidiProperties["L"], lineProperties["XX"], wordProperties["Other"]);

            CompilationUnitSyntax unit = BuildCompilationUnit(bidiRuns, lineRuns, wordRuns, brackets, targetNamespace);
            CompilationUnitSyntax formatted = unit.NormalizeWhitespace(indentation: "    ", eol: "\n");
            formatted = formatted.ReplaceNodes(formatted.DescendantNodes().OfType<PropertyDeclarationSyntax>(),
                static (property, _) => FormatTable(property));
            string source = formatted.ToFullString() + "\n";
            SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
            Require(!tree.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "Generated Unicode source contains syntax errors.");
            context.CancellationToken.ThrowIfCancellationRequested();
            context.AddSource("UnicodeDataTables.g.cs", SourceText.From(source, Utf8));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or FormatException or OverflowException)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidInput, Location.None, exception.Message));
        }
    }

    private static Dictionary<string, string> ReadInputs(ImmutableArray<InputText> suppliedInputs)
    {
        Dictionary<string, string> contents = new(StringComparer.Ordinal);
        foreach (InputText supplied in suppliedInputs)
        {
            Require(supplied.Content is not null, $"Cannot read AdditionalFile {supplied.Name}.");
            Require(contents.TryAdd(supplied.Name, supplied.Content ?? string.Empty), $"Duplicate Unicode AdditionalFile {supplied.Name}.");
        }

        Dictionary<string, string> files = new(StringComparer.Ordinal);
        foreach (InputFile input in Inputs)
        {
            string name = input.Name + ".txt";
            Require(contents.TryGetValue(name, out string? content) && content is not null, $"Missing Unicode AdditionalFile {name}.");
            string data = content ?? string.Empty;
            Require(Convert.ToHexStringLower(SHA256.HashData(Utf8.GetBytes(data))) == input.Sha256, $"Checksum mismatch for {name}.");
            Require(data.StartsWith($"# {input.Name}-{UnicodeVersion}.txt", StringComparison.Ordinal), $"Unexpected Unicode version in {name}.");
            files.Add(input.Name, data);
        }

        Require(contents.TryGetValue("LICENSE.txt", out string? license) && license is not null, "Missing Unicode license AdditionalFile.");
        Require(Convert.ToHexStringLower(SHA256.HashData(Utf8.GetBytes(license ?? string.Empty))) == "e7a93b009565cfce55919a381437ac4db883e9da2126fa28b91d12732bc53d96", "Unicode license checksum mismatch.");
        return files;
    }

    private static Dictionary<string, byte> ReadEnum(Compilation compilation, string name)
    {
        INamedTypeSymbol declaration = compilation.GetTypeByMetadataName(name)
            ?? throw new InvalidDataException($"Cannot find enum {name} in the consuming compilation.");
        Require(declaration.TypeKind == TypeKind.Enum && declaration.EnumUnderlyingType?.SpecialType == SpecialType.System_Byte, $"Expected byte enum {name}.");
        Dictionary<string, byte> properties = new(StringComparer.Ordinal);
        HashSet<byte> values = [];
        foreach (IFieldSymbol member in declaration.GetMembers().OfType<IFieldSymbol>().Where(static field => field.HasConstantValue))
        {
            byte value = Convert.ToByte(member.ConstantValue, CultureInfo.InvariantCulture);
            Require(values.Add(value), $"Duplicate numeric value in {name}.");
            properties.Add(member.Name, value);
        }

        Require(properties.Count > 0, $"No properties found in {name}.");
        return properties;
    }

    private static uint[] CreatePropertyRuns(string content, Dictionary<string, byte> properties)
    {
        List<PropertyRange> defaults = [];
        List<PropertyRange> explicitRanges = [];
        foreach (UnicodeDataRow row in UnicodeDataParser.ReadRows(content))
        {
            ImmutableArray<string> fields = row.Fields;
            Require(fields.Length == 2, $"Malformed Unicode property row at line {row.LineNumber}.");
            (int start, int end) = ParseRange(fields[0]);
            string property = fields[1] switch
            {
                "Left_To_Right" => "L",
                "Right_To_Left" => "R",
                "Arabic_Letter" => "AL",
                "European_Terminator" => "ET",
                _ => fields[1].Replace("_", string.Empty, StringComparison.Ordinal)
            };
            Require(properties.TryGetValue(property, out byte value), $"Unknown Unicode property: {fields[1]}");
            (row.IsDefault ? defaults : explicitRanges).Add(new PropertyRange(start, end, value));
        }

        Require(defaults.Count > 0 && defaults[0].Start == 0 && defaults[0].End == CodePointCount - 1, "Missing full Unicode default range.");
        Require(explicitRanges.Count > 0, "Unicode input contains no explicit property ranges.");
        byte[] expanded = new byte[CodePointCount];
        // Regional @missing defaults override the global default; explicit assignments override both.
        foreach (PropertyRange range in defaults)
        {
            Array.Fill(expanded, range.Value, range.Start, range.End - range.Start + 1);
        }

        bool[] assigned = new bool[CodePointCount];
        foreach (PropertyRange range in explicitRanges)
        {
            for (int codePoint = range.Start; codePoint <= range.End; codePoint++)
            {
                if (assigned[codePoint])
                {
                    throw new InvalidDataException($"Overlapping explicit Unicode ranges at U+{codePoint:X4}.");
                }
                assigned[codePoint] = true;
                expanded[codePoint] = range.Value;
            }
        }

        List<uint> runs = [];
        for (int codePoint = 0; codePoint < expanded.Length; codePoint++)
        {
            if (codePoint == 0 || expanded[codePoint] != expanded[codePoint - 1])
            {
                runs.Add(((uint)codePoint << 8) | expanded[codePoint]);
            }
        }

        uint[] packed = runs.ToArray();
        for (int codePoint = 0; codePoint < expanded.Length; codePoint++)
        {
            if (Lookup(packed, codePoint) != expanded[codePoint])
            {
                throw new InvalidDataException($"Packed lookup mismatch at U+{codePoint:X4}.");
            }
        }

        return packed;
    }

    private static (int Start, int End) ParseRange(ReadOnlySpan<char> value)
    {
        int separator = value.IndexOf("..", StringComparison.Ordinal);
        ReadOnlySpan<char> leading = separator >= 0 ? value[..separator] : value;
        int start = int.Parse(leading, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        int end = separator >= 0 ? int.Parse(value[(separator + 2)..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture) : start;
        Require(start >= 0 && start <= end && end < CodePointCount, $"Out-of-range Unicode interval: {value}");
        return (start, end);
    }

    private static BracketTables ReadBrackets(string content)
    {
        SortedDictionary<int, (int Partner, bool IsOpen)> pairs = [];
        foreach (UnicodeDataRow row in UnicodeDataParser.ReadRows(content))
        {
            ImmutableArray<string> fields = row.Fields;
            Require(!row.IsDefault && fields.Length == 3 && fields[2] is "o" or "c", $"Malformed bracket row at line {row.LineNumber}.");
            (int codePoint, int end) = ParseRange(fields[0]);
            (int partner, int partnerEnd) = ParseRange(fields[1]);
            Require(codePoint == end && partner == partnerEnd, "Bracket rows must contain single code points.");
            Require(pairs.TryAdd(codePoint, (partner, fields[2] == "o")), $"Duplicate bracket U+{codePoint:X4}.");
        }

        Require(pairs.Count > 0, "No Unicode brackets found.");
        foreach (KeyValuePair<int, (int Partner, bool IsOpen)> pair in pairs)
        {
            Require(pairs.TryGetValue(pair.Value.Partner, out (int Partner, bool IsOpen) reverse)
                && reverse.Partner == pair.Key && reverse.IsOpen != pair.Value.IsOpen, $"Nonreciprocal bracket mapping at U+{pair.Key:X4}.");
        }

        int[] opens = pairs.Where(pair => pair.Value.IsOpen).Select(pair => pair.Key).ToArray();
        int[] partners = opens.Select(codePoint => pairs[codePoint].Partner).ToArray();
        int[] closes = pairs.Where(pair => !pair.Value.IsOpen).Select(pair => pair.Key).ToArray();
        for (int codePoint = 0; codePoint < CodePointCount; codePoint++)
        {
            bool exists = pairs.TryGetValue(codePoint, out (int Partner, bool IsOpen) pair);
            int openIndex = Array.BinarySearch(opens, codePoint);
            if ((openIndex >= 0) != (exists && pair.IsOpen)
                || (Array.BinarySearch(closes, codePoint) >= 0) != (exists && !pair.IsOpen)
                || (openIndex >= 0 && partners[openIndex] != pair.Partner))
            {
                throw new InvalidDataException($"Bracket lookup mismatch at U+{codePoint:X4}.");
            }
        }

        return new BracketTables(opens, partners, closes);
    }

    private static byte Lookup(uint[] runs, int codePoint)
    {
        uint target = ((uint)codePoint << 8) | 0xFFu;
        int index = Array.BinarySearch(runs, target);
        return (byte)(runs[index >= 0 ? index : ~index - 1] & 0xFFu);
    }

    private static void VerifyPrivateUseDefaults(uint[] bidi, uint[] line, uint[] word, byte leftToRight, byte unknown, byte other)
    {
        (int Start, int End)[] ranges = [(0xE000, 0xF8FF), (0xF0000, 0xFFFFD), (0x100000, 0x10FFFD)];
        foreach ((int start, int end) in ranges)
        {
            for (int codePoint = start; codePoint <= end; codePoint++)
            {
                if (Lookup(bidi, codePoint) != leftToRight || Lookup(line, codePoint) != unknown || Lookup(word, codePoint) != other)
                {
                    throw new InvalidDataException($"Incorrect private-use default at U+{codePoint:X4}.");
                }
            }
        }
    }

    private static CompilationUnitSyntax BuildCompilationUnit(uint[] bidi, uint[] line, uint[] word, BracketTables brackets, string targetNamespace)
    {
        MemberDeclarationSyntax[] members =
        [
            BuildTable("OpenBracketCodePoints", SyntaxKind.IntKeyword, brackets.Open.Select(value => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal("0x" + value.ToString("X4", CultureInfo.InvariantCulture), value)))),
            BuildTable("MatchingCloseBracketCodePoints", SyntaxKind.IntKeyword, brackets.Partners.Select(value => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal("0x" + value.ToString("X4", CultureInfo.InvariantCulture), value)))),
            BuildTable("CloseBracketCodePoints", SyntaxKind.IntKeyword, brackets.Close.Select(value => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal("0x" + value.ToString("X4", CultureInfo.InvariantCulture), value)))),
            BuildTable("BidiClassRuns", SyntaxKind.UIntKeyword, bidi.Select(BuildPackedLiteral)),
            BuildTable("LineBreakRuns", SyntaxKind.UIntKeyword, line.Select(BuildPackedLiteral)),
            BuildTable("WordBreakRuns", SyntaxKind.UIntKeyword, word.Select(BuildPackedLiteral))
        ];
        ClassDeclarationSyntax declaration = ClassDeclaration("UnicodeDataTables")
            .WithModifiers(TokenList(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.StaticKeyword), Token(SyntaxKind.PartialKeyword)))
            .WithMembers(List(members));
        FileScopedNamespaceDeclarationSyntax scope = FileScopedNamespaceDeclaration(ParseName(targetNamespace))
            .WithMembers(SingletonList<MemberDeclarationSyntax>(declaration));
        List<SyntaxTrivia> header =
        [
            Comment("// <auto-generated/>"), EndOfLine("\n"),
            Comment($"// Unicode {UnicodeVersion}. Generated automatically by NativeUI.UnicodeGenerator during compilation."), EndOfLine("\n"),
            Comment("// Unicode License V3 is included in NativeUI.Core packages."), EndOfLine("\n")
        ];
        foreach (InputFile input in Inputs)
        {
            header.Add(Comment($"// Source: https://www.unicode.org/Public/{UnicodeVersion}/ucd/{input.RelativeUrl}"));
            header.Add(EndOfLine("\n"));
            header.Add(Comment($"// SHA-256: {input.Sha256}"));
            header.Add(EndOfLine("\n"));
        }

        return CompilationUnit().WithMembers(SingletonList<MemberDeclarationSyntax>(scope)).WithLeadingTrivia(TriviaList(header));
    }

    private static LiteralExpressionSyntax BuildPackedLiteral(uint value)
    {
        return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal("0x" + value.ToString("X8", CultureInfo.InvariantCulture) + "u", value));
    }

    private static PropertyDeclarationSyntax BuildTable(string name, SyntaxKind elementType, IEnumerable<LiteralExpressionSyntax> values)
    {
        GenericNameSyntax spanType = GenericName(Identifier("ReadOnlySpan"))
            .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList<TypeSyntax>(PredefinedType(Token(elementType)))));
        CollectionExpressionSyntax collection = CollectionExpression(SeparatedList<CollectionElementSyntax>(values.Select(value => ExpressionElement(value))));
        return PropertyDeclaration(spanType, name)
            .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithExpressionBody(ArrowExpressionClause(collection))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }

    private static PropertyDeclarationSyntax FormatTable(PropertyDeclarationSyntax property)
    {
        if (property.ExpressionBody?.Expression is not CollectionExpressionSyntax collection)
        {
            throw new InvalidDataException("Expected a generated collection expression.");
        }

        List<SyntaxNodeOrToken> elements = [];
        for (int index = 0; index < collection.Elements.Count; index++)
        {
            elements.Add(collection.Elements[index].WithoutTrivia()
                .WithLeadingTrivia(Whitespace(index % 8 == 0 ? "        " : " ")));
            SyntaxToken comma = Token(SyntaxKind.CommaToken);
            if (index % 8 == 7 || index == collection.Elements.Count - 1)
            {
                comma = comma.WithTrailingTrivia(EndOfLine("\n"));
            }

            elements.Add(comma);
        }

        collection = collection.WithElements(SeparatedList<CollectionElementSyntax>(elements))
            .WithOpenBracketToken(Token(SyntaxKind.OpenBracketToken).WithTrailingTrivia(EndOfLine("\n")))
            .WithCloseBracketToken(Token(SyntaxKind.CloseBracketToken).WithLeadingTrivia(Whitespace("    ")));
        ArrowExpressionClauseSyntax body = property.ExpressionBody
            .WithArrowToken(Token(SyntaxKind.EqualsGreaterThanToken)
                .WithTrailingTrivia(EndOfLine("\n"), Whitespace("    ")))
            .WithExpression(collection);
        return property.WithExpressionBody(body);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }

    private readonly record struct InputFile(string Name, string RelativeUrl, string Sha256);
    private readonly record struct InputText(string Name, string? Content);
    private readonly record struct PropertyRange(int Start, int End, byte Value);
    private readonly record struct BracketTables(int[] Open, int[] Partners, int[] Close);
}
