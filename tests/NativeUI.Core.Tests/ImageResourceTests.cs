using System.Text;
using NativeUI.Media;
using NativeUI.Tests;

namespace NativeUI.Core.Tests;

internal static class ImageResourceTests
{
    internal static void VerifyResources()
    {
        (string assembly, string path) = ImageResourceResolver.ParseResource("resource://MyControls/Assets/logo%20one.svg");
        Assert.Equal("MyControls", assembly);
        Assert.Equal("Assets/logo one.svg", path);
        ImageResourceContext known = new() { Assemblies = [typeof(ImageResourceTests).Assembly] };
        byte[] embedded = ImageResourceResolver.ReadAsync("resource://NativeUI.Core.Tests/Assets/logo.svg", known).AsTask().GetAwaiter().GetResult();
        Assert.Equal("image/svg+xml", ImageResourceResolver.GetContentType(embedded));
        Reject<FileNotFoundException>(() => ImageResourceResolver.ReadAsync("resource://NativeUI.Core.Tests/Assets/missing.svg", known).AsTask().GetAwaiter().GetResult());
        string[] invalid =
        [
            "resource:///x", "resource:// /x", "resource://MyControls.dll/x", "resource://A,Version=1/x",
            "resource://A/x?query", "resource://A/x#fragment", "resource://A/Assets\\logo.svg",
            "resource://A/%2e%2e/x", "resource://A/x%2fy", "resource://A/x%5cy", "resource://A/%252e%252e",
            "resource://A/%00", "resource://A/%0a", "resource://A/%zz", "resource://A/x%", "resource://A//x"
        ];
        foreach (string source in invalid)
        {
            Reject<FormatException>(() => ImageResourceResolver.ParseResource(source));
        }
    }

    internal static void VerifyResolution()
    {
        Uri applicationBase = new("https://example.com/app/");
        Assert.Equal("https://example.com/app/Assets/logo.png", ImageResourceResolver.ResolveRemoteUri("Assets/logo.png", applicationBase)?.AbsoluteUri);
        Assert.Equal("https://example.com/app/Assets/logo.png", ImageResourceResolver.ResolveRemoteUri("/Assets/logo.png", applicationBase)?.AbsoluteUri);
        Assert.Equal("https://cdn.example.com/logo.png", ImageResourceResolver.ResolveRemoteUri("https://cdn.example.com/logo.png", applicationBase)?.AbsoluteUri);
        Reject<NotSupportedException>(() => ImageResourceResolver.ResolveRemoteUri("file:///tmp/a", applicationBase));
        Reject<NotSupportedException>(() => ImageResourceResolver.ResolveRemoteUri("data:image/png,abc", applicationBase));
        Reject<InvalidDataException>(() => ImageResourceResolver.ResolveRemoteUri("../secret.png", applicationBase));
        Reject<FormatException>(() => ImageResourceResolver.ResolveRemoteUri("//other.example/a", applicationBase));

        string parent = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nativeui-images-" + Guid.NewGuid().ToString("N"));
        string root = System.IO.Path.Combine(parent, "app");
        string sibling = root + "-private";
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(sibling);
        try
        {
            File.WriteAllBytes(System.IO.Path.Combine(root, "valid.png"), [1, 2, 3]);
            File.WriteAllBytes(System.IO.Path.Combine(sibling, "secret.png"), [9]);
            Assert.Equal((byte)1, File.ReadAllBytes(ImageResourceResolver.ResolveAssetPath("/valid.png", root))[0]);
            Reject<InvalidDataException>(() => ImageResourceResolver.ResolveAssetPath("../app-private/secret.png", root));
            if (!OperatingSystem.IsWindows())
            {
                Directory.CreateSymbolicLink(System.IO.Path.Combine(root, "outside"), sibling);
                Reject<InvalidDataException>(() => ImageResourceResolver.ResolveAssetPath("outside/secret.png", root));
                File.CreateSymbolicLink(System.IO.Path.Combine(root, "linked.png"), System.IO.Path.Combine(sibling, "secret.png"));
                Reject<InvalidDataException>(() => ImageResourceResolver.ResolveAssetPath("linked.png", root));
            }
            ImageResourceContext limited = new() { AssetRoot = root, Options = new ImageLoadOptions { MaximumEncodedBytes = 2 } };
            Reject<InvalidDataException>(() => ImageResourceResolver.ReadAsync("valid.png", limited).AsTask().GetAwaiter().GetResult());
            ImageResourceContext allowed = new() { AssetRoot = root, Options = new ImageLoadOptions { MaximumEncodedBytes = 3 } };
            Assert.Equal(3, ImageResourceResolver.ReadAsync("valid.png", allowed).AsTask().GetAwaiter().GetResult().Length);
            File.WriteAllBytes(System.IO.Path.Combine(root, "large.bin"), new byte[9 * 1024 * 1024]);
            Assert.Equal(9 * 1024 * 1024, ImageResourceResolver.ReadAsync("large.bin", new ImageResourceContext { AssetRoot = root }).AsTask().GetAwaiter().GetResult().Length);
            using CancellationTokenSource cancelled = new();
            cancelled.Cancel();
            Reject<OperationCanceledException>(() => ImageResourceResolver.ReadAsync("valid.png", allowed, cancelled.Token).AsTask().GetAwaiter().GetResult());
        }
        finally
        {
            Directory.Delete(parent, true);
        }
    }

    internal static void VerifySvgDetection()
    {
        Assert.Equal("image/svg+xml", ImageResourceResolver.GetContentType(Encoding.UTF8.GetBytes("<?xml version='1.0'?><svg xmlns='http://www.w3.org/2000/svg'/>")));
        foreach (string document in new[] { "<html/>", "<other/>", "<svg xmlns='urn:other'/>", "<!DOCTYPE svg [<!ENTITY x 'x'>]><svg/>" })
        {
            Reject<InvalidDataException>(() => ImageResourceResolver.GetContentType(Encoding.UTF8.GetBytes(document)));
        }
        // Root detection intentionally leaves security validation to SvgParser.Parse.
        string unsafeSvg = "<svg><script/></svg>";
        Assert.Equal("image/svg+xml", ImageResourceResolver.GetContentType(Encoding.UTF8.GetBytes(unsafeSvg)));
        Reject<FormatException>(() => SvgParser.Parse(unsafeSvg));
    }

    private static void Reject<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }
}
