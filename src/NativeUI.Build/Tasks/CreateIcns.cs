using Microsoft.Build.Framework;
using BuildTask = Microsoft.Build.Utilities.Task;

namespace NativeUI.Build;

/// <summary>
/// An MSBuild task that writes an <c>.icns</c> file from a PNG image, for the icon of a macOS application bundle.
/// </summary>
public sealed class CreateIcns : BuildTask
{
    /// <summary>
    /// Gets or sets the path of the PNG image. See <see cref="IcnsFile.FromPng"/> for the supported sizes.
    /// </summary>
    [Required]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path of the <c>.icns</c> file to write.
    /// </summary>
    [Required]
    public string Output { get; set; } = string.Empty;

    /// <inheritdoc />
    public override bool Execute()
    {
        try
        {
            byte[] icns = IcnsFile.FromPng(File.ReadAllBytes(Source));
            Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
            File.WriteAllBytes(Output, icns);
            return true;
        }
        catch (ArgumentException exception)
        {
            Log.LogError($"NativeUIApplicationIcon '{Source}': {exception.Message}");
            return false;
        }
        catch (IOException exception)
        {
            Log.LogError($"NativeUIApplicationIcon '{Source}': {exception.Message}");
            return false;
        }
    }
}
