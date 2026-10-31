using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NativeUI.Layout;

namespace NativeUI.Controls.MacOS;

/// <summary>
/// Provides Objective-C runtime and macOS AppKit / CoreFoundation P/Invoke bindings for native window hosting,
/// mouse/keyboard input routing, and pasteboard integration.
/// </summary>
internal static partial class AppKitInterop
{
    private const string ObjCLib = "/usr/lib/libobjc.A.dylib";
    private const string AppKitFramework = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const string FoundationFramework = "/System/Library/Frameworks/Foundation.framework/Foundation";
    private const ulong EventModifierFlagCommand = 1UL << 20;
    private const ulong EventModifierFlagControl = 1UL << 18;

    private static readonly ConcurrentDictionary<nint, Window> s_viewToWindow = new();
    private static bool s_frameworksLoaded;
    private static nint s_interactiveViewClass;

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

    public static void RegisterWindowView(nint nsView, Window window)
    {
        if (nsView != nint.Zero)
        {
            s_viewToWindow[nsView] = window;
        }
    }

    public static void UnregisterWindowView(nint nsView)
    {
        if (nsView != nint.Zero)
        {
            s_viewToWindow.TryRemove(nsView, out _);
        }
    }

    [LibraryImport(ObjCLib, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint GetClass(string name);

    [LibraryImport(ObjCLib, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint GetSelector(string name);

    [LibraryImport(ObjCLib, EntryPoint = "objc_allocateClassPair", StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint AllocateClassPair(nint superclass, string name, nuint extraBytes);

    [LibraryImport(ObjCLib, EntryPoint = "objc_registerClassPair")]
    public static partial void RegisterClassPair(nint cls);

    [LibraryImport(ObjCLib, EntryPoint = "class_addMethod", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool ClassAddMethod(nint cls, nint name, nint imp, string types);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial nint SendMessage(nint receiver, nint selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial nint SendMessage(nint receiver, nint selector, nint arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial nint SendMessage(nint receiver, nint selector, nint arg1, nint arg2);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial nint SendMessage(nint receiver, nint selector, nint arg1, ulong arg2);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial ulong SendMessageUInt64(nint receiver, nint selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial long SendMessageInt64(nint receiver, nint selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial CGPoint SendMessagePoint(nint receiver, nint selector);

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

    public static unsafe nint GetInteractiveImageViewClass()
    {
        if (s_interactiveViewClass != nint.Zero)
        {
            return s_interactiveViewClass;
        }

        EnsureFrameworksLoaded();
        nint baseClass = GetClass("NSImageView");
        nint existing = GetClass("NativeUIInteractiveImageView");
        if (existing != nint.Zero)
        {
            s_interactiveViewClass = existing;
            return existing;
        }

        nint subClass = AllocateClassPair(baseClass, "NativeUIInteractiveImageView", 0);
        if (subClass == nint.Zero)
        {
            return baseClass;
        }

        delegate* unmanaged[Cdecl]<nint, nint, byte> acceptsFirstResponderPtr = &OnAcceptsFirstResponder;
        delegate* unmanaged[Cdecl]<nint, nint, nint, void> mouseDownPtr = &OnMouseDown;
        delegate* unmanaged[Cdecl]<nint, nint, nint, void> mouseDraggedPtr = &OnMouseDragged;
        delegate* unmanaged[Cdecl]<nint, nint, nint, void> mouseUpPtr = &OnMouseUp;
        delegate* unmanaged[Cdecl]<nint, nint, nint, void> keyDownPtr = &OnKeyDown;
        delegate* unmanaged[Cdecl]<nint, nint, nint, byte> performKeyEquivPtr = &OnPerformKeyEquivalent;

        _ = ClassAddMethod(subClass, GetSelector("acceptsFirstResponder"), (nint)acceptsFirstResponderPtr, "c@:");
        _ = ClassAddMethod(subClass, GetSelector("mouseDown:"), (nint)mouseDownPtr, "v@:@");
        _ = ClassAddMethod(subClass, GetSelector("mouseDragged:"), (nint)mouseDraggedPtr, "v@:@");
        _ = ClassAddMethod(subClass, GetSelector("mouseUp:"), (nint)mouseUpPtr, "v@:@");
        _ = ClassAddMethod(subClass, GetSelector("keyDown:"), (nint)keyDownPtr, "v@:@");
        _ = ClassAddMethod(subClass, GetSelector("performKeyEquivalent:"), (nint)performKeyEquivPtr, "c@:@");

        RegisterClassPair(subClass);
        s_interactiveViewClass = subClass;
        return subClass;
    }

    public static void SetPasteboardString(string text)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        EnsureFrameworksLoaded();
        nint pbClass = GetClass("NSPasteboard");
        nint generalPb = SendMessage(pbClass, GetSelector("generalPasteboard"));
        if (generalPb == nint.Zero)
        {
            return;
        }

        _ = SendMessage(generalPb, GetSelector("clearContents"));
        nint nsText = CreateNSString(text);
        nint nsType = CreateNSString("public.utf8-plain-text");
        _ = SendMessage(generalPb, GetSelector("setString:forType:"), nsText, nsType);
    }

    public static string? GetPasteboardString()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        EnsureFrameworksLoaded();
        nint pbClass = GetClass("NSPasteboard");
        nint generalPb = SendMessage(pbClass, GetSelector("generalPasteboard"));
        if (generalPb == nint.Zero)
        {
            return null;
        }

        nint nsType = CreateNSString("public.utf8-plain-text");
        nint valObj = SendMessage(generalPb, GetSelector("stringForType:"), nsType);
        if (valObj == nint.Zero)
        {
            return null;
        }

        nint utf8Ptr = SendMessage(valObj, GetSelector("UTF8String"));
        return Marshal.PtrToStringUTF8(utf8Ptr);
    }

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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte OnAcceptsFirstResponder(nint self, nint cmd)
    {
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnMouseDown(nint self, nint cmd, nint nsEvent)
    {
        if (!s_viewToWindow.TryGetValue(self, out Window? window) || nsEvent == nint.Zero)
        {
            return;
        }

        CGPoint loc = SendMessagePoint(nsEvent, GetSelector("locationInWindow"));
        long clicks = SendMessageInt64(nsEvent, GetSelector("clickCount"));
        Point topDownPoint = new(loc.X, Math.Max(0, window.Height - loc.Y));
        window.HandlePointerPressed(topDownPoint, (int)Math.Max(1, clicks));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnMouseDragged(nint self, nint cmd, nint nsEvent)
    {
        if (!s_viewToWindow.TryGetValue(self, out Window? window) || nsEvent == nint.Zero)
        {
            return;
        }

        CGPoint loc = SendMessagePoint(nsEvent, GetSelector("locationInWindow"));
        Point topDownPoint = new(loc.X, Math.Max(0, window.Height - loc.Y));
        window.HandlePointerMoved(topDownPoint, isPressed: true);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnMouseUp(nint self, nint cmd, nint nsEvent)
    {
        if (!s_viewToWindow.TryGetValue(self, out Window? window) || nsEvent == nint.Zero)
        {
            return;
        }

        CGPoint loc = SendMessagePoint(nsEvent, GetSelector("locationInWindow"));
        Point topDownPoint = new(loc.X, Math.Max(0, window.Height - loc.Y));
        window.HandlePointerReleased(topDownPoint);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnKeyDown(nint self, nint cmd, nint nsEvent)
    {
        _ = TryDispatchKeyEvent(self, nsEvent);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte OnPerformKeyEquivalent(nint self, nint cmd, nint nsEvent)
    {
        return TryDispatchKeyEvent(self, nsEvent) ? (byte)1 : (byte)0;
    }

    private static bool TryDispatchKeyEvent(nint self, nint nsEvent)
    {
        if (!s_viewToWindow.TryGetValue(self, out Window? window) || nsEvent == nint.Zero)
        {
            return false;
        }

        ulong flags = SendMessageUInt64(nsEvent, GetSelector("modifierFlags"));
        bool hasCommandOrControl = (flags & (EventModifierFlagCommand | EventModifierFlagControl)) != 0;

        nint charsObj = SendMessage(nsEvent, GetSelector("charactersIgnoringModifiers"));
        if (charsObj == nint.Zero)
        {
            return false;
        }

        nint utf8Ptr = SendMessage(charsObj, GetSelector("UTF8String"));
        string? key = Marshal.PtrToStringUTF8(utf8Ptr);
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        return window.HandleKeyDownAsync(key, hasCommandOrControl).GetAwaiter().GetResult();
    }
}
