using Microsoft.Build.Framework;
using BuildTask = Microsoft.Build.Utilities.Task;

namespace NativeUI.Build;

/// <summary>
/// An MSBuild task that writes the <c>Info.plist</c> of a macOS application bundle.
/// </summary>
public sealed class CreateInfoPlist : BuildTask
{
    /// <summary>
    /// Gets or sets the name that the Dock and menu bar show.
    /// </summary>
    [Required]
    public string BundleName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bundle identifier.
    /// </summary>
    [Required]
    public string BundleIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file name of the main executable in <c>Contents/MacOS</c>.
    /// </summary>
    [Required]
    public string Executable { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the short version string.
    /// </summary>
    [Required]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the icon file in <c>Contents/Resources</c>, or an empty string for none.
    /// </summary>
    public string IconFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path of the <c>Info.plist</c> file to write.
    /// </summary>
    [Required]
    public string Output { get; set; } = string.Empty;

    /// <inheritdoc />
    public override bool Execute()
    {
        try
        {
            string plist = InfoPlistFile.Create(
                BundleName,
                BundleIdentifier,
                Executable,
                Version,
                IconFile.Length == 0 ? null : IconFile);
            Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
            File.WriteAllText(Output, plist);
            return true;
        }
        catch (IOException exception)
        {
            Log.LogError($"Info.plist '{Output}': {exception.Message}");
            return false;
        }
    }
}
