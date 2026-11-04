using NativeUI.Layout;

namespace NativeUI.Graphics;

/// <summary>An owned decoded image supplied by the active graphics backend.</summary>
public interface IDecodedImage : IDisposable
{
    /// <summary>Gets pixel dimensions; one pixel supplies one intrinsic logical unit.</summary>
    Size Size { get; }
}

