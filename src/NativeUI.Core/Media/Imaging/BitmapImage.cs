namespace NativeUI.Media;

/// <summary>An image located by an app path, assembly resource, or HTTP(S) URL.</summary>
/// <param name="Uri">The source string, retaining assembly-name casing.</param>
public sealed record BitmapImage(string Uri) : ImageSource;
