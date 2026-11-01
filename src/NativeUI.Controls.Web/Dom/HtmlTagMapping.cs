namespace NativeUI.Controls.Web;

/// <summary>
/// Defines the HTML element created for each control, so DOM creation and generated CSS selectors cannot disagree.
/// </summary>
internal static class HtmlTagMapping
{
    private const string TextTag = "span";
    private const string ContainerTag = "div";
    private const string ButtonTag = "button";
    private const string CanvasTag = "canvas";

    /// <summary>
    /// Gets the tag created for <paramref name="element"/>.
    /// </summary>
    public static string GetTag(UIElement element)
    {
        if (element is Text)
        {
            return TextTag;
        }

        if (element is Button)
        {
            return ButtonTag;
        }

        if (element is CanvasView)
        {
            return CanvasTag;
        }

        return ContainerTag;
    }

    /// <summary>
    /// Gets a selector with the specificity of one type that matches every tag used by instances of <paramref name="type"/>.
    /// </summary>
    public static string GetTagSelector(Type type)
    {
        if (typeof(Text).IsAssignableFrom(type))
        {
            return TextTag;
        }

        if (typeof(Button).IsAssignableFrom(type))
        {
            return ButtonTag;
        }

        if (typeof(CanvasView).IsAssignableFrom(type))
        {
            return CanvasTag;
        }

        // A base type or interface of Text also covers controls rendered with the container tag.
        return type.IsAssignableFrom(typeof(Text)) ? $":is({TextTag}, {ContainerTag})" : ContainerTag;
    }

    /// <summary>
    /// Gets the tag for a NativeUI element type name such as <c>text</c> or <c>view</c>.
    /// </summary>
    /// <exception cref="NotSupportedException">The name has no known HTML element.</exception>
    public static string GetTagSelector(string elementType)
    {
        return elementType.ToLowerInvariant() switch
        {
            "text" => TextTag,
            "view" => ContainerTag,
            "button" => ButtonTag,
            "canvas" or "canvasview" => CanvasTag,
            _ => throw new NotSupportedException($"The element type '{elementType}' has no HTML element mapping.")
        };
    }
}
