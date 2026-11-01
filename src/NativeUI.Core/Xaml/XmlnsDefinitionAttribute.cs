namespace NativeUI.Xaml;

/// <summary>
/// Maps an XML namespace to a CLR namespace of the declaring assembly, for use by elements in XAML files.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class XmlnsDefinitionAttribute : Attribute
{
    /// <summary>
    /// Gets the XML namespace.
    /// </summary>
    public string XmlNamespace { get; }

    /// <summary>
    /// Gets the CLR namespace.
    /// </summary>
    public string ClrNamespace { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlnsDefinitionAttribute"/> class.
    /// </summary>
    /// <param name="xmlNamespace">The XML namespace, such as <c>urn:nativeui</c>.</param>
    /// <param name="clrNamespace">The CLR namespace that contains the element types.</param>
    public XmlnsDefinitionAttribute(string xmlNamespace, string clrNamespace)
    {
        ArgumentException.ThrowIfNullOrEmpty(xmlNamespace);
        ArgumentException.ThrowIfNullOrEmpty(clrNamespace);
        XmlNamespace = xmlNamespace;
        ClrNamespace = clrNamespace;
    }
}
