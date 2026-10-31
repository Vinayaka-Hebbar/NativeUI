using System.Reflection.Metadata;
using NativeUI.Core;

[assembly: MetadataUpdateHandler(typeof(HotReloadManager))]

namespace NativeUI.Core;

/// <summary>
/// Receives .NET Hot Reload notifications (<see cref="MetadataUpdateHandlerAttribute"/>) during debugging
/// and notifies active desktop windows and Web hosts to re-evaluate styles and repaint without restarting.
/// </summary>
public static class HotReloadManager
{
    /// <summary>
    /// Occurs when the .NET runtime applies a Hot Reload metadata update.
    /// </summary>
    public static event Action<Type[]?>? HotReloaded;

    /// <summary>
    /// Invoked by the .NET runtime when types are updated via Hot Reload. Used to clear cached metadata.
    /// </summary>
    public static void ClearCache(Type[]? updatedTypes)
    {
    }

    /// <summary>
    /// Invoked by the .NET runtime after a Hot Reload update is applied.
    /// </summary>
    public static void UpdateApplication(Type[]? updatedTypes)
    {
        HotReloaded?.Invoke(updatedTypes);
    }
}
