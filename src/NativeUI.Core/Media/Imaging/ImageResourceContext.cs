using System.Reflection;

namespace NativeUI.Media;

/// <summary>Supplies application roots, known assemblies, and encoded-size policy to the loader.</summary>
internal sealed class ImageResourceContext
{
    internal string AssetRoot { get; init; } = AppContext.BaseDirectory;
    internal Uri? ApplicationBaseUri { get; init; }
    internal IReadOnlyCollection<Assembly> Assemblies { get; init; } = AppDomain.CurrentDomain.GetAssemblies();
    internal ImageLoadOptions Options { get; init; } = new();
}
