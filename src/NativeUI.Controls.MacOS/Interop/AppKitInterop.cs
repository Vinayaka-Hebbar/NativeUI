using System.Runtime.InteropServices;

namespace NativeUI.Controls.MacOS;

/// <summary>
/// Provides Objective-C runtime and macOS AppKit / CoreFoundation P/Invoke bindings for native window hosting.
/// </summary>
internal static partial class AppKitInterop
{
    private const string ObjCLib = "/usr/lib/libobjc.A.dylib";
    private const string AppKitFramework = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const string FoundationFramework = "/System/Library/Frameworks/Foundation.framework/Foundation";

    [StructLayout(LayoutKind.Sequential)]
    public struct CGPoint
    {
        public double X;
        public double Y;

        public CGPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CGSize
    {
        public double Width;
        public double Height;

        public CGSize(double width, double height)
        {
            Width = width;
            Height = height;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CGRect
    {
        public CGPoint Origin;
        public CGSize Size;

        public CGRect(double x, double y, double width, double height)
        {
            Origin = new CGPoint(x, y);
            Size = new CGSize(width, height);
        }
    }

    private static bool s_frameworksLoaded;

    public static void EnsureFrameworksLoaded()
    {
        if (s_frameworksLoaded || !OperatingSystem.IsMacOS())
        {
            return;
        }

        _ = NativeLibrary.Load(FoundationFramework);
        _ = NativeLibrary.Load(AppKitFramework);
        s_frameworksLoaded = true;
    }

    [LibraryImport(ObjCLib, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint GetClass(string name);

    [LibraryImport(ObjCLib, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint GetSelector(string name);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial nint SendMessage(nint receiver, nint selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial nint SendMessage(nint receiver, nint selector, nint arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial nint SendMessage(nint receiver, nint selector, nint arg1, ulong arg2);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void SendMessageVoid(nint receiver, nint selector, [MarshalAs(UnmanagedType.I1)] bool arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void SendMessageVoid(nint receiver, nint selector, nint arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial nint SendMessageWindowInit(
        nint receiver,
        nint selector,
        CGRect contentRect,
        ulong styleMask,
        ulong backing,
        [MarshalAs(UnmanagedType.I1)] bool defer);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial nint SendMessageViewInit(nint receiver, nint selector, CGRect frameRect);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial nint SendMessageImageInitWithData(nint receiver, nint selector, nint nsData);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void SendMessageSetSize(nint receiver, nint selector, CGSize size);

    public static bool IsSystemDarkMode()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        EnsureFrameworksLoaded();
        nint defaultsClass = GetClass("NSUserDefaults");
        nint standardDefaults = SendMessage(defaultsClass, GetSelector("standardUserDefaults"));
        if (standardDefaults == nint.Zero)
        {
            return false;
        }

        nint key = CreateNSString("AppleInterfaceStyle");
        nint valObj = SendMessage(standardDefaults, GetSelector("stringForKey:"), key);
        if (valObj == nint.Zero)
        {
            return false;
        }

        nint utf8Ptr = SendMessage(valObj, GetSelector("UTF8String"));
        string? style = Marshal.PtrToStringUTF8(utf8Ptr);
        return string.Equals(style, "Dark", StringComparison.OrdinalIgnoreCase);
    }

    public static nint CreateNSString(string text)
    {
        nint nsStringClass = GetClass("NSString");
        nint alloc = SendMessage(nsStringClass, GetSelector("alloc"));
        IntPtr utf8Ptr = Marshal.StringToCoTaskMemUTF8(text);
        try
        {
            return SendMessage(alloc, GetSelector("initWithUTF8String:"), utf8Ptr);
        }
        finally
        {
            Marshal.FreeCoTaskMem(utf8Ptr);
        }
    }

    public static unsafe nint CreateNSData(ReadOnlySpan<byte> bytes)
    {
        nint nsDataClass = GetClass("NSData");
        fixed (byte* ptr = bytes)
        {
            return SendMessage(nsDataClass, GetSelector("dataWithBytes:length:"), (nint)ptr, (ulong)bytes.Length);
        }
    }
}
