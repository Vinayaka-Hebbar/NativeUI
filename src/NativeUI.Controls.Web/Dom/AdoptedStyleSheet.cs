using System.Runtime.Versioning;

namespace NativeUI.Controls.Web;

/// <summary>
/// A counted reference to a stylesheet adopted by the document. References with the same identifier share one
/// stylesheet, which is removed when the last reference is disposed. Operations must run on the browser's UI thread.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class AdoptedStyleSheet : IDisposable
{
    private static readonly Dictionary<string, int> s_referenceCounts = [];

    private readonly string _id;
    private bool _disposed;

    private AdoptedStyleSheet(string id)
    {
        _id = id;
    }

    /// <summary>
    /// Adopts the stylesheet, or replaces its CSS when <paramref name="id"/> is already adopted, and returns a new reference.
    /// </summary>
    public static AdoptedStyleSheet Adopt(string id, string css)
    {
        s_referenceCounts[id] = s_referenceCounts.GetValueOrDefault(id) + 1;
        DomInterop.InjectStyleSheet(id, css);
        return new AdoptedStyleSheet(id);
    }

    /// <summary>
    /// Releases this reference, and removes the stylesheet when it is the last one.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        int remainingReferences = s_referenceCounts[_id] - 1;
        if (remainingReferences > 0)
        {
            s_referenceCounts[_id] = remainingReferences;
            return;
        }

        s_referenceCounts.Remove(_id);
        DomInterop.RemoveStyleSheet(_id);
    }
}
