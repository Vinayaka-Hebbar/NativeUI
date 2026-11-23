namespace NativeUI.Media;

/// <summary>Identifies a region of the anchor positioning grid in horizontal left-to-right layout.</summary>
public readonly record struct PositionArea
{
    private readonly bool _hasArea;

    /// <summary>Initializes the occupied tracks around the anchor.</summary>
    /// <param name="block">The occupied vertical tracks.</param>
    /// <param name="inline">The occupied horizontal tracks.</param>
    public PositionArea(PositionAreaTrack block, PositionAreaTrack inline)
    {
        if (!Enum.IsDefined(block))
        {
            throw new ArgumentOutOfRangeException(nameof(block));
        }

        if (!Enum.IsDefined(inline))
        {
            throw new ArgumentOutOfRangeException(nameof(inline));
        }

        Block = block;
        Inline = inline;
        _hasArea = true;
    }

    /// <summary>Gets the CSS none value, which leaves the containing block unchanged.</summary>
    public static PositionArea None => default;

    /// <summary>Gets whether no anchor positioning area is specified.</summary>
    public bool IsNone => !_hasArea;

    /// <summary>Gets the occupied vertical tracks.</summary>
    public PositionAreaTrack Block { get; }

    /// <summary>Gets the occupied horizontal tracks.</summary>
    public PositionAreaTrack Inline { get; }
}
