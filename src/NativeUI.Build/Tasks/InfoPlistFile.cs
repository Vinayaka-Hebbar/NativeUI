using System.Xml;
using System.Xml.Linq;

namespace NativeUI.Build;

/// <summary>
/// Creates the <c>Info.plist</c> property list of a macOS application bundle.
/// </summary>
public static class InfoPlistFile
{
    /// <summary>
    /// Returns the property list of an application bundle whose main executable is <paramref name="executable"/>.
    /// </summary>
    /// <param name="name">The name that the Dock and menu bar show.</param>
    /// <param name="identifier">The bundle identifier.</param>
    /// <param name="executable">The file name of the main executable in <c>Contents/MacOS</c>.</param>
    /// <param name="version">The short version string.</param>
    /// <param name="iconFile">The name of the icon file in <c>Contents/Resources</c>, or <see langword="null"/> for none.</param>
    public static string Create(string name, string identifier, string executable, string version, string? iconFile)
    {
        XElement dictionary = new("dict");
        AddString(dictionary, "CFBundleName", name);
        AddString(dictionary, "CFBundleDisplayName", name);
        AddString(dictionary, "CFBundleIdentifier", identifier);
        AddString(dictionary, "CFBundleExecutable", executable);
        AddString(dictionary, "CFBundlePackageType", "APPL");
        AddString(dictionary, "CFBundleShortVersionString", version);
        dictionary.Add(new XElement("key", "NSHighResolutionCapable"), new XElement("true"));
        if (iconFile is not null)
        {
            AddString(dictionary, "CFBundleIconFile", iconFile);
        }

        XDocument document = new(
            new XDeclaration("1.0", "UTF-8", null),
            new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
            new XElement("plist", new XAttribute("version", "1.0"), dictionary));

        using StringWriter text = new();
        using (XmlWriter writer = XmlWriter.Create(text, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true }))
        {
            document.Save(writer);
        }

        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + Environment.NewLine + text;
    }

    private static void AddString(XElement dictionary, string key, string value)
    {
        dictionary.Add(new XElement("key", key), new XElement("string", value));
    }
}
