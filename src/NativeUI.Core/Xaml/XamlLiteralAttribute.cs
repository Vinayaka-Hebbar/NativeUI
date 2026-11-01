namespace NativeUI.Xaml;

/// <summary>
/// Declares the XAML attribute text that a static member of a value type represents. The XAML compiler matches attribute
/// text against this pattern and emits a reference to the member, so generated code parses no text at run time.
/// </summary>
/// <remarks>
/// A property or field uses a pattern without a placeholder. A method uses a pattern with one <c>{0}</c> placeholder and takes
/// one <see cref="string"/>, <see cref="bool"/>, <see cref="int"/>, <see cref="long"/>, or <see cref="double"/> parameter,
/// or a pattern without a placeholder and no parameters. The member must be public, static, and return its declaring type.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class XamlLiteralAttribute : Attribute
{
    /// <summary>
    /// Gets the attribute text the member represents.
    /// </summary>
    public string Pattern { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="XamlLiteralAttribute"/> class.
    /// </summary>
    /// <param name="pattern">The attribute text, such as <c>Auto</c> or <c>{0}%</c>.</param>
    public XamlLiteralAttribute(string pattern)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);
        Pattern = pattern;
    }
}
