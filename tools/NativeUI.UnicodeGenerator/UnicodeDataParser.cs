using System.Collections.Immutable;

namespace NativeUI.UnicodeGenerator;

/// <summary>
/// Reads semicolon-delimited Unicode records while preserving @missing directives and ignoring comments.
/// </summary>
internal static class UnicodeDataParser
{
    private const string MissingDirective = "# @missing:";

    internal static IEnumerable<UnicodeDataRow> ReadRows(string content)
    {
        using StringReader reader = new(content);
        int lineNumber = 0;
        while (reader.ReadLine() is string line)
        {
            lineNumber++;
            if (TryParseRow(line.AsSpan(), lineNumber, out UnicodeDataRow row))
            {
                yield return row;
            }
        }
    }

    private static bool TryParseRow(ReadOnlySpan<char> line, int lineNumber, out UnicodeDataRow row)
    {
        row = default;
        line = line.Trim();
        bool isDefault = line.StartsWith(MissingDirective, StringComparison.Ordinal);
        if (isDefault)
        {
            line = line[MissingDirective.Length..].TrimStart();
        }

        int commentStart = line.IndexOf('#');
        if (commentStart >= 0)
        {
            line = line[..commentStart].TrimEnd();
        }

        if (line.IsEmpty)
        {
            return false;
        }

        ImmutableArray<string>.Builder fields = ImmutableArray.CreateBuilder<string>();
        while (true)
        {
            int separator = line.IndexOf(';');
            ReadOnlySpan<char> field = (separator >= 0 ? line[..separator] : line).Trim();
            if (field.IsEmpty)
            {
                throw new InvalidDataException($"Empty Unicode field at line {lineNumber}.");
            }

            fields.Add(field.ToString());
            if (separator < 0)
            {
                break;
            }

            line = line[(separator + 1)..];
        }

        row = new UnicodeDataRow(lineNumber, isDefault, fields.ToImmutable());
        return true;
    }
}
