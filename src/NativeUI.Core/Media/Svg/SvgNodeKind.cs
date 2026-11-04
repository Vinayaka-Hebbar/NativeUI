namespace NativeUI.Media;

/// <summary>Defines supported NodeKind values in an immutable SVG definition.</summary>
public enum SvgNodeKind
{
    /// <summary>Uses Svg semantics.</summary>
    Svg,
    /// <summary>Uses Group semantics.</summary>
    Group,
    /// <summary>Uses Path semantics.</summary>
    Path,
    /// <summary>Uses Definitions semantics.</summary>
    Definitions,
    /// <summary>Uses LinearGradient semantics.</summary>
    LinearGradient,
    /// <summary>Uses RadialGradient semantics.</summary>
    RadialGradient,
    /// <summary>Uses Stop semantics.</summary>
    Stop,
    /// <summary>Uses ClipPath semantics.</summary>
    ClipPath,
    /// <summary>Uses Mask semantics.</summary>
    Mask,
    /// <summary>Uses Use semantics.</summary>
    Use,
    /// <summary>Uses Text semantics.</summary>
    Text,
    /// <summary>Uses TextSpan semantics.</summary>
    TextSpan,
    /// <summary>Uses TextRun semantics.</summary>
    TextRun,
    /// <summary>Uses Title semantics.</summary>
    Title,
    /// <summary>Uses Description semantics.</summary>
    Description
}
