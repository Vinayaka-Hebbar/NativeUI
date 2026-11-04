namespace NativeUI.Media;

/// <summary>A parsed logical length or fraction of a definite containing dimension.</summary>
/// <param name="Value">Logical units, or a fraction when IsPercentage is true.</param>
/// <param name="IsPercentage">Whether Value is relative to the containing dimension.</param>
public readonly record struct SvgLength(double Value, bool IsPercentage = false)
{
    /// <summary>Resolves a length without reparsing text; indefinite percentages return null.</summary>
    public double? Resolve(double? reference = null) => !IsPercentage ? Value
        : reference is double dimension && double.IsFinite(dimension) && dimension >= 0 ? Value * dimension : null;
}
