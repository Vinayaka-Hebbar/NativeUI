using System.Collections.Immutable;

namespace NativeUI.UnicodeGenerator;

/// <summary>
/// Stores a Unicode data record with its source line, default-directive flag, and ordered fields.
/// </summary>
internal readonly record struct UnicodeDataRow(int LineNumber, bool IsDefault, ImmutableArray<string> Fields);
