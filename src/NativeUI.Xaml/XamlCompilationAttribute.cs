namespace NativeUI.Xaml;

/// <summary>
/// Marks a view class as having been compiled from a declarative NativeUI XAML definition at build time.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class XamlCompilationAttribute : Attribute
{
    /// <summary>
    /// Gets the source XAML file name associated with the compiled view.
    /// </summary>
    public string SourceFile { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="XamlCompilationAttribute"/> class.
    /// </summary>
    public XamlCompilationAttribute(string sourceFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        SourceFile = sourceFile;
    }
}
